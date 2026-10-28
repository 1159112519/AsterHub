"""Counter, volume and archive contracts; no business process mutation."""
import copy
import tempfile
import time
from pathlib import Path
import unittest
from aqserver.devices import calculate_network, recent_series
from aqserver.storage import Store
from aqserver import probe


def network(t, down, up, name='eth0'):
    return {'sampleSeconds':t, 'interfaces':[{'id':name,'name':name,'receivedBytes':down,'sentBytes':up}]}


class PeripheralTests(unittest.TestCase):
    def test_baseline_direction_device_isolation(self):
        first=network(10,1000,2000); self.assertIsNone(calculate_network(first,None)['receive'])
        second=calculate_network(network(12,7144,4048),first)
        self.assertEqual((second['receive'],second['send']),(3072,1024))
        other=calculate_network(network(104,103000,108000),network(100,99000,100000))
        self.assertEqual(other['receive'],1000); self.assertEqual(second['receive'],3072)
    def test_reset_reappearance_and_duplicate_time(self):
        before=network(10,10000,20000)
        for sample in [network(12,1,1),network(10,20000,30000),network(12,20000,30000,'eth1')]:
            self.assertIsNone(calculate_network(sample,before)['receive'])
        idle=calculate_network(network(12,10000,20000),before)
        self.assertEqual((idle['receive'],idle['send']),(0,0))
    def test_five_minute_window_does_not_depend_on_frequency(self):
        for interval in [1,2,5,60]:
            points=[{'timestamp':10000-i*interval} for i in range(1000)][::-1]
            selected=recent_series(points,10000)
            self.assertEqual(len(selected),300//interval+1)
            self.assertGreaterEqual(selected[0]['timestamp'],9700)
            self.assertEqual(selected[-1]['timestamp'],10000)
        self.assertEqual(recent_series([{'timestamp':11000},{'timestamp':9999}],10000),[{'timestamp':9999}])
    def test_archive_reopen_and_legacy_payload(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); store=Store(root/'config',root/'data')
            sample={'timestamp':time.time(),'cpuPercent':42,'memory':{'percent':50},'gpus':[], 'network':calculate_network(network(12,7144,4048),network(10,1000,2000)), 'volumes':[{'path':'/','total':1000,'used':400,'available':500,'percent':40,'fileSystem':'ext4'}]}
            store.append('fixture',sample)
            legacy={'timestamp':time.time()+1,'cpuPercent':20,'memory':{'percent':25},'gpus':[]};store.append('fixture',legacy)
            reopened=Store(root/'config',root/'data'); rows=reopened.history('fixture',0,time.time()+10)['samples']
            restored=reopened.snapshot(rows[0]['id'],'fixture')
            self.assertEqual(restored['network']['receive'],3072);self.assertEqual(restored['volumes'][0]['percent'],40)
            self.assertNotIn('network',reopened.snapshot(rows[1]['id'],'fixture'))
    def test_real_linux_counters_and_volumes_are_read_only(self):
        first=probe.network_snapshot(); time.sleep(.05); second=calculate_network(probe.network_snapshot(),first)
        self.assertGreater(second['sampleSeconds'],first['sampleSeconds'])
        self.assertTrue(second['interfaces']);self.assertIsNotNone(second['receive'])
        volumes=probe.storage_volumes();self.assertTrue(any(x['path']=='/' for x in volumes))
        self.assertTrue(all(x['total']>0 and 0<=x['percent']<=100 for x in volumes))

if __name__=='__main__': unittest.main()
