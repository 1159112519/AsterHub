"""Linux runtime regressions. Tests use isolated per-user roots and owned fixtures."""
import asyncio
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest
from aiohttp import CookieJar, FormData
from aiohttp.test_utils import TestClient, TestServer
from aqserver.storage import Store
from aqserver.devices import Manager
from aqserver.files import safe_relative
from aqserver.server import create_app
from aqserver import probe


def fixture(timestamp=None):
    return {'timestamp':timestamp or time.time(),'cpuPercent':42,'perCore':[20,64],
      'memory':{'total':1000,'available':400,'used':600,'percent':60,'swapTotal':0,'swapFree':0},
      'gpus':[{'index':0,'uuid':'GPU-test','name':'fixture','utilization':80,'memoryUsedMiB':100,'memoryTotalMiB':200}],
      'gpuProcesses':[{'pid':123,'username':'fixture-user','memoryMiB':100,'gpuUuid':'GPU-test'}],
      'processes':[],'hardware':{'cpuModel':'fixture'},'disk':{'total':100,'used':40}}


class StoreTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.base=Path(self.temp.name)
        self.store=Store(self.base/'config',self.base/'data')
    def tearDown(self):self.temp.cleanup()
    def test_snapshot_restart_device_and_username(self):
        sample=fixture();self.store.append('a',sample);self.store.append('b',fixture(sample['timestamp']+1))
        reopened=Store(self.base/'config',self.base/'data')
        result=reopened.history('a',0,time.time()+10)
        self.assertEqual(result['total'],1)
        self.assertEqual(reopened.snapshot(result['samples'][0]['id'],'a')['gpuProcesses'][0]['username'],'fixture-user')
        self.assertIsNone(reopened.snapshot(result['samples'][0]['id'],'b'))
    def test_secret_not_saved_and_private_permissions(self):
        self.store.settings['devices'][1]['password']='temporary-test-secret';self.store.save()
        self.assertNotIn('temporary-test-secret',self.store.settings_path.read_text())
        self.assertEqual(self.store.settings_path.stat().st_mode&0o777,0o600)
        self.assertEqual(self.store.access_key_path.stat().st_mode&0o777,0o600)
    def test_history_downsampling_and_retention(self):
        for i in range(250):self.store.append('a',fixture(time.time()+i))
        result=self.store.history('a',0,time.time()+1000,20)
        self.assertEqual(result['total'],250);self.assertLessEqual(len(result['samples']),20)
        self.store.append('a',fixture(time.time()-60*86400))
        while self.store.insert_count%150:self.store.append('a',fixture())
        self.assertEqual(self.store.history('a',0,time.time()-31*86400)['total'],0)
    def test_traversal_rejected(self):
        for value in ('../a','/tmp/a','a/../../b','a\\..\\b','\x00bad'):
            with self.assertRaises(ValueError):safe_relative(value)
        self.assertEqual(safe_relative('folder/中文.txt'),'folder/中文.txt')


class RuntimeTests(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp=tempfile.TemporaryDirectory();self.base=Path(self.temp.name)
        self.store=Store(self.base/'config',self.base/'data')
        self.store.settings['devices'][0]['enabled']=False
        self.app=create_app(self.store)
        self.client=TestClient(TestServer(self.app),cookie_jar=CookieJar(unsafe=True));await self.client.start_server()
    async def asyncTearDown(self):
        await self.client.close();self.temp.cleanup()
    async def login(self):
        response=await self.client.get('/?key='+self.store.access_key)
        self.assertEqual(response.status,200);await response.read()
    async def test_private_bootstrap_and_cross_origin_guard(self):
        self.assertEqual((await self.client.get('/')).status,403)
        self.assertEqual((await self.client.get('/api/state')).status,401)
        await self.login();self.assertEqual((await self.client.get('/api/state')).status,200)
        self.assertEqual((await self.client.get('/api/state',headers={'Host':'attacker.invalid'})).status,403)
        self.assertEqual((await self.client.post('/api/settings',json={})).status,403)
        self.assertEqual((await self.client.post('/api/settings',json={},headers={'X-AQ-Request':'1','Origin':'https://attacker.invalid'})).status,403)
    async def test_copy_upload_conflict_undo_and_changed_file(self):
        await self.login();target=self.base/'uploads';target.mkdir()
        url='/api/files/upload?device=local&path='+str(target)+'&relative=sub/a.txt&size=4'
        form=FormData();form.add_field('file',b'data',filename='a.txt')
        response=await self.client.post(url,data=form,headers={'X-AQ-Request':'1'})
        self.assertEqual(response.status,200,await response.text());task=await response.json()
        self.assertEqual((target/'sub/a.txt').read_bytes(),b'data')
        form=FormData();form.add_field('file',b'new!',filename='a.txt')
        response=await self.client.post(url,data=form,headers={'X-AQ-Request':'1'})
        self.assertEqual(response.status,409);self.assertEqual((target/'sub/a.txt').read_bytes(),b'data')
        transfers=(await (await self.client.get('/api/state')).json())['transfers']
        self.assertEqual(len(transfers),2,'Rejected uploads must remain inspectable in the transfer queue')
        self.assertEqual(transfers[-1]['status'],'failed')
        self.assertIn('存在',transfers[-1]['error'])
        (target/'sub/a.txt').write_bytes(b'edit')
        response=await self.client.post('/api/files/undo',json={'id':task['id']},headers={'X-AQ-Request':'1'})
        self.assertEqual(response.status,400);self.assertEqual((target/'sub/a.txt').read_bytes(),b'edit')
        (target/'sub/a.txt').write_bytes(b'data')
        response=await self.client.post('/api/files/undo',json={'id':task['id']},headers={'X-AQ-Request':'1'})
        self.assertEqual(response.status,200);self.assertFalse((target/'sub/a.txt').exists())
    async def test_history_exact_snapshot_and_device_isolation(self):
        await self.login();sample=fixture();self.store.append('local',sample)
        response=await self.client.get('/api/history?device=local');data=await response.json()
        ident=data['samples'][0]['id']
        response=await self.client.get('/api/history/'+str(ident)+'?device=local')
        self.assertEqual(response.status,200);self.assertEqual((await response.json())['gpuProcesses'][0]['username'],'fixture-user')
        self.assertEqual((await self.client.get('/api/history/'+str(ident)+'?device=other')).status,404)
    async def test_owned_process_confirmation_and_pid_identity(self):
        await self.login();process=subprocess.Popen(['sleep','30'])
        try:
            item=probe.process_info(process.pid)
            body={'device':'local','pid':process.pid,'startToken':item['startToken']}
            response=await self.client.post('/api/process/end',json=body,headers={'X-AQ-Request':'1'})
            self.assertEqual(response.status,400);self.assertIsNone(process.poll())
            response=await self.client.post('/api/process/end',json=dict(body,confirm=True,startToken='0'),headers={'X-AQ-Request':'1'})
            self.assertEqual(response.status,400);self.assertIsNone(process.poll())
            response=await self.client.post('/api/process/end',json=dict(body,confirm=True),headers={'X-AQ-Request':'1'})
            self.assertEqual(response.status,200);await asyncio.to_thread(process.wait,3)
        finally:
            if process.poll() is None:process.terminate();process.wait(timeout=3)
    async def test_real_terminal_utf8_prompt_and_lifecycle(self):
        await self.login();ws=await self.client.ws_connect('/api/terminal?device=local')
        output=b''
        await ws.send_json({'type':'resize','cols':100,'rows':30})
        await ws.send_json({'type':'input','data':"printf 'AQ_TERMINAL_中文_%s\\n' 123\r"})
        deadline=time.monotonic()+8
        while time.monotonic()<deadline:
            try:message=await ws.receive(timeout=1)
            except asyncio.TimeoutError:continue
            if message.type.name=='BINARY':output+=message.data
            if 'AQ_TERMINAL_中文_123'.encode() in output:break
        self.assertIn('AQ_TERMINAL_中文_123'.encode(),output)
        self.assertNotIn(b'no job control',output)
        await ws.close()
    async def test_all_enabled_devices_record_without_selection(self):
        store=self.store
        store.settings['interval']=1
        store.settings['devices']=[{'id':'first','name':'first','type':'local','enabled':True},{'id':'second','name':'second','type':'local','enabled':True}]
        manager=Manager(store)
        for device in manager.devices.values():device.collect=lambda:fixture()
        await manager.start();await asyncio.sleep(1.15);await manager.stop()
        self.assertGreaterEqual(store.history('first',0,time.time()+1)['total'],2)
        self.assertGreaterEqual(store.history('second',0,time.time()+1)['total'],2)

if __name__=='__main__':unittest.main(verbosity=2)
