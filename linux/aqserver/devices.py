"""Independent collectors: changing the selected page never stops archiving."""
import asyncio
import base64
import hashlib
import json
from pathlib import Path
import shlex
import time
import uuid
import zlib
import paramiko
from . import probe


class HostKeyRequired(Exception):
    def __init__(self, fingerprint):
        self.fingerprint = fingerprint
        super().__init__('请核对并确认 SSH 主机指纹')


def fingerprint(key):
    return 'SHA256:'+base64.b64encode(hashlib.sha256(key.asbytes()).digest()).decode().rstrip('=')


class ConfirmHostKey(paramiko.MissingHostKeyPolicy):
    def __init__(self, known_hosts, approved):
        self.path, self.approved = known_hosts, approved

    def missing_host_key(self, client, hostname, key):
        value = fingerprint(key)
        if value != self.approved: raise HostKeyRequired(value)
        client.get_host_keys().add(hostname,key.get_name(),key)
        client.save_host_keys(str(self.path))
        self.path.chmod(0o600)


class Device:
    def __init__(self, manager, config):
        self.manager, self.config = manager, config
        self.client = None
        self.password = None
        self.task = None
        self.latest = None
        self.hardware = {}
        self.error = None
        self.status = 'disconnected'
        self.latency = None
        self.previous = None
        self.home = str(Path.home()) if config['type']=='local' else None
        self.history_tail = []
        self.failures = 0

    def connect(self, password=None, approved=None):
        if self.config['type']=='local': return
        if self.client:
            self.client.close(); self.client=None
        if password is not None: self.password=password or None
        known = self.manager.store.config_dir/'known_hosts'
        client=paramiko.SSHClient()
        client.load_system_host_keys()
        if known.exists(): client.load_host_keys(str(known))
        client.set_missing_host_key_policy(ConfirmHostKey(known,approved))
        key_path = self.config.get('keyPath','')
        try:
            client.connect(self.config['host'],port=self.config.get('port',22),username=self.config['username'],
                password=self.password,key_filename=str(Path(key_path).expanduser()) if key_path else None,
                look_for_keys=True,allow_agent=True,timeout=8,auth_timeout=8,banner_timeout=8)
            client.get_transport().set_keepalive(15)
            _,out,_=client.exec_command('printf "%s" "$HOME"',timeout=8)
            self.home=out.read(4096).decode(errors='replace').strip() or '/'
            self.client=client
        except Exception:
            client.close(); raise

    def sftp(self):
        if self.config['type']=='local': return None
        if not self.client or not self.client.get_transport() or not self.client.get_transport().is_active():
            raise ValueError('请先连接该设备')
        return self.client.open_sftp()

    def collect(self):
        start=time.perf_counter()
        if self.config['type']=='local':
            sample=probe.collect(); self.latency=None
        else:
            if not self.client or not self.client.get_transport() or not self.client.get_transport().is_active(): self.connect()
            _,pong,_=self.client.exec_command('printf aq',timeout=8)
            if pong.read(16)!=b'aq': raise ValueError('SSH 响应无效')
            self.latency=round((time.perf_counter()-start)*1000)
            packed=base64.b64encode(zlib.compress(Path(probe.__file__).read_bytes())).decode()
            command='import base64,zlib;exec(compile(zlib.decompress(base64.b64decode('+repr(packed)+')),\"aq_probe\",\"exec\"))'
            _,out,err=self.client.exec_command('python3 -c '+shlex.quote(command),timeout=18)
            raw=out.read(2*1024*1024)
            if out.channel.recv_exit_status()!=0: raise ValueError('采集失败，请确认服务器可运行 Python 3 和 /proc')
            sample=json.loads(raw)
        previous=self.previous
        rates=[]
        for index,counter in enumerate(sample['cpuCounters']):
            before=previous['cpuCounters'][index] if previous and index<len(previous['cpuCounters']) else None
            total=counter['total']-before['total'] if before else 0
            rates.append(max(0,min(100,100*(1-(counter['idle']-before['idle'])/total))) if total>0 else 0)
        sample['cpuPercent']=round(rates[0],2) if rates else 0
        sample['perCore']=[round(v,2) for v in rates[1:]]
        memory=sample['memory']; memory['used']=max(0,memory['total']-memory['available'])
        memory['percent']=100*memory['used']/memory['total'] if memory['total'] else 0
        elapsed=sample['timestamp']-previous['timestamp'] if previous else 0
        sample['network']={'receive':max(0,(sample['networkCounters'][0]-previous['networkCounters'][0])/elapsed) if elapsed>0 else 0,
                           'send':max(0,(sample['networkCounters'][1]-previous['networkCounters'][1])/elapsed) if elapsed>0 else 0}
        self.previous=sample
        self.hardware.update({k:v for k,v in sample.get('hardware',{}).items() if v is not None})
        sample['hardware']=dict(self.hardware)
        sample['latencyMs']=self.latency
        self.latest=sample
        self.status='connected'; self.error=None; self.failures=0
        self.history_tail.append({'timestamp':sample['timestamp'],'cpu':sample['cpuPercent'],'memory':memory['percent'],
                                  'gpu':sum(x.get('utilization') or 0 for x in sample['gpus'])/len(sample['gpus']) if sample['gpus'] else 0})
        self.history_tail=self.history_tail[-120:]
        return sample

    def public(self):
        return dict(self.config,status=self.status,error=self.error,hardware=self.hardware,sample=self.latest,
                    latencyMs=self.latency,series=self.history_tail,failures=self.failures)

    def close(self):
        if self.client: self.client.close(); self.client=None
        self.password=None


class Manager:
    def __init__(self, store):
        self.store=store
        self.devices={x['id']:Device(self,x) for x in store.settings['devices']}
        self.running=True

    async def start(self):
        for device in self.devices.values():
            if device.config.get('enabled'): self.launch(device)

    def launch(self,device):
        if not device.task or device.task.done(): device.task=asyncio.create_task(self.loop(device))

    async def loop(self, device):
        while self.running and device.config.get('enabled'):
            started=time.monotonic()
            try:
                sample=await asyncio.to_thread(device.collect)
                if device.config.get('enabled'): await asyncio.to_thread(self.store.append,device.config['id'],sample)
            except asyncio.CancelledError: raise
            except Exception as error:
                device.failures+=1; device.status='reconnecting'
                if isinstance(error,HostKeyRequired): device.error='需要确认 SSH 主机指纹'
                elif isinstance(error,paramiko.AuthenticationException): device.error='SSH 认证失败，请在设备设置中重新连接'
                else: device.error='连接暂不可用，保留上次数据并重试'
            interval=self.store.settings['interval'] if device.failures==0 else min(30,2**min(device.failures,5))
            await asyncio.sleep(max(.2,interval-(time.monotonic()-started)))

    async def enable(self, device, password=None, approved=None):
        await asyncio.to_thread(device.connect,password,approved)
        device.config['enabled']=True
        self.persist(); self.launch(device)

    async def disable(self, device):
        if device.config['id']=='local': raise ValueError('本机持续记录已开启')
        device.config['enabled']=False; device.status='disconnected'
        if device.task: device.task.cancel()
        await asyncio.to_thread(device.close)
        self.persist()

    def persist(self):
        self.store.settings['devices']=[x.config for x in self.devices.values()]; self.store.save()

    def add(self, data):
        host=str(data.get('host','')).strip(); user=str(data.get('username','')).strip()
        port=int(data.get('port',22))
        name=str(data.get('name','')).strip() or host
        if not host or len(host)>253 or any(c.isspace() for c in host) or not user or len(user)>128 or not 1<=port<=65535 or len(name)>100:
            raise ValueError('请填写有效的主机、用户名和端口')
        config=dict(id=uuid.uuid4().hex[:16],name=name,type='ssh',host=host,username=user,port=port,
                    keyPath=str(data.get('keyPath',''))[:1024],enabled=False)
        device=Device(self,config); self.devices[config['id']]=device; self.persist(); return device

    async def stop(self):
        self.running=False
        for device in self.devices.values():
            if device.task: device.task.cancel()
            device.close()
        await asyncio.gather(*(d.task for d in self.devices.values() if d.task),return_exceptions=True)
