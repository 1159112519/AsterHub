#!/usr/bin/env python3
"""Local-only AsterHub recorder. No listener, credentials or filesystem mutations beyond its data directory."""
import argparse,csv,io,json,math,os,signal,sqlite3,subprocess,time,zlib
from pathlib import Path
VERSION="0.1.0"
TICKS=621355968000000000
DEFAULT_ROOT="/var/lib/asterhub-agent"

def text(path):
    try:return Path(path).read_text().strip()
    except (OSError,UnicodeError):return ""

def run(args):
    try:
        p=subprocess.run(args,capture_output=True,text=True,timeout=3,check=True)
        return list(csv.reader(io.StringIO(p.stdout)))
    except (OSError,subprocess.SubprocessError):return []

def number(value):
    try:
        v=float(value)
        return v if math.isfinite(v) else None
    except (ValueError,TypeError):return None

def process_executable(pid,fallback):
    # Resolve only the executable link, never arguments or process environment.
    if not str(pid).isascii() or not str(pid).isdigit():return fallback
    try:return os.readlink('/proc/'+str(pid)+'/exe') or fallback
    except OSError:return fallback

class Sampler:
    def __init__(self,interval=5):
        self.interval=interval;self.previous=None;self.known_gpus=[];self.boot=text('/proc/sys/kernel/random/boot_id')
    def sample(self):
        now=time.monotonic();cpu={};network={};disks={}
        for row in text('/proc/stat').splitlines():
            cols=row.split()
            if cols and cols[0].startswith('cpu'):
                values=list(map(int,cols[1:9]));cpu[cols[0]]=(sum(values),values[3]+(values[4] if len(values)>4 else 0))
        for row in text('/proc/net/dev').splitlines():
            if ':' not in row:continue
            name,values=row.split(':',1);name=name.strip();v=values.split()
            if name!='lo' and len(v)>=16:network[name]=list(map(int,v))
        for row in text('/proc/diskstats').splitlines():
            v=row.split()
            if len(v)<14:continue
            name=v[2]
            if name.startswith(('loop','ram')) or Path('/sys/class/block',name,'partition').exists():continue
            disks[name]=[int(v[5])*512,int(v[9])*512,int(v[12]),int(v[11])]
        prev=self.previous;elapsed=now-prev[0] if prev else None
        def rate(current,old):return max(0,current-old)/elapsed if elapsed and elapsed>0 and current>=old else None
        usage={}
        if prev:
            for name,(total,idle) in cpu.items():
                if name in prev[1]:
                    dt=total-prev[1][name][0];di=idle-prev[1][name][1]
                    usage[name]=max(0,min(100,100*(dt-di)/dt)) if dt>0 else None
        mem={}
        for row in text('/proc/meminfo').splitlines():
            k,_,v=row.partition(':');mem[k]=int(v.strip().split()[0])*1024
        total=mem.get('MemTotal',0);available=mem.get('MemAvailable',mem.get('MemFree',0));used=max(0,total-available)
        interfaces=[]
        for name,v in network.items():
            old=prev[2].get(name) if prev else None
            interfaces.append(dict(Id=name,Name=name,ReceivedBytes=v[0],SentBytes=v[8],ReceivedPackets=v[1],SentPackets=v[9],ReceiveErrors=v[2],SendErrors=v[10],DownloadBytesPerSecond=rate(v[0],old[0]) if old else None,UploadBytesPerSecond=rate(v[8],old[8]) if old else None))
        activity=[]
        for name,v in disks.items():
            old=prev[3].get(name) if prev else None
            activity.append(dict(Id=name,Name=name,ReadBytesPerSecond=rate(v[0],old[0]) if old else None,WriteBytesPerSecond=rate(v[1],old[1]) if old else None,ActivePercent=min(100,rate(v[2],old[2])/10) if old and rate(v[2],old[2]) is not None else None,QueueDepth=v[3]))
        frequencies=[number(line.split(':',1)[1].strip()) for line in text('/proc/cpuinfo').splitlines() if line.startswith('cpu MHz')]
        frequencies=[v for v in frequencies if v is not None]
        gpu_fields=['index','uuid','name','utilization.gpu','memory.used','memory.total','temperature.gpu','fan.speed','power.draw','power.limit','clocks.current.graphics','clocks.current.memory']
        gpu_rows=run(['nvidia-smi','--query-gpu='+','.join(gpu_fields),'--format=csv,noheader,nounits'])
        gpus=[]
        for row in gpu_rows:
            if len(row)!=len(gpu_fields):continue
            values=[x.strip() for x in row]
            gpus.append(dict(zip(['Index','Uuid','Name','Util','Used','Total','Temp','Fan','Power','Cap','Clock','MemoryClock'],values)))
        gpu_error=None
        if gpus:self.known_gpus=[dict(g) for g in gpus]
        elif self.known_gpus:
            gpus=[{**g,**{k:'—' for k in ['Util','Used','Temp','Fan','Power','Clock','MemoryClock']}} for g in self.known_gpus];gpu_error='GPU reading unavailable; inventory retained'
        processes=[];gpu_ids={g['Uuid']:g['Index'] for g in gpus}
        for row in run(['nvidia-smi','--query-compute-apps=gpu_uuid,pid,process_name,used_gpu_memory','--format=csv,noheader,nounits']):
            if len(row)!=4 or row[0].strip() not in gpu_ids:continue
            uuid,pid,name,mb=[x.strip() for x in row]
            processes.append(dict(Gpu=gpu_ids[uuid],Pid=pid,Name=process_executable(pid,name),Memory=mb,User='—'))
            if len(processes)>=256:break
        volumes=[];seen=set()
        for row in text('/proc/mounts').splitlines():
            v=row.split()
            if len(v)<3 or not v[0].startswith('/dev/'):continue
            path=v[1].replace('\\040',' ')
            if path in seen:continue
            seen.add(path)
            try:
                st=os.statvfs(path);t=st.f_blocks*st.f_frsize;a=st.f_bavail*st.f_frsize
                if t:volumes.append(dict(Path=path,Label=v[0],FileSystem=v[2],TotalBytes=t,UsedBytes=t-st.f_bfree*st.f_frsize,AvailableBytes=a))
            except OSError:pass
        core_ids=sorted(int(k[3:]) for k in cpu if k!='cpu' and usage.get(k) is not None)
        def summed(key):
            values=[n[key] for n in interfaces if n[key] is not None]
            return sum(values) if values else None
        record=dict(V=1,T=TICKS+time.time_ns()//100,Interval=self.interval,Cpu=str(round(usage['cpu'],3)) if usage.get('cpu') is not None else '—',CoreIds=core_ids,CoreUsage=[usage.get('cpu'+str(i),0) for i in core_ids],Mem=f'{used/1048576:.3f} / {total/1048576:.3f} MiB',Gpus=gpus,Processes=processes,Volumes=volumes,Storage=activity,Network=dict(SampleSeconds=elapsed or 0,Interfaces=interfaces,DownloadBytesPerSecond=summed('DownloadBytesPerSecond'),UploadBytesPerSecond=summed('UploadBytesPerSecond')),CpuFrequencyMHz=sum(frequencies)/len(frequencies) if frequencies else None,CpuFrequencySource='linux-cpufreq',Error='',Agent=dict(version=VERSION,boot=self.boot,gpuError=gpu_error))
        self.previous=(now,cpu,network,disks)
        return record

class Store:
    def __init__(self,root,readonly=False):
        self.root=Path(root);self.path=self.root/'records.sqlite3'
        if readonly:
            self.db=sqlite3.connect(self.path.as_uri()+'?mode=ro',uri=True,timeout=3)
        else:
            self.root.mkdir(parents=True,exist_ok=True)
            self.db=sqlite3.connect(self.path,timeout=3)
            self.db.execute('pragma auto_vacuum=incremental');self.db.execute('pragma journal_mode=WAL');self.db.execute('pragma synchronous=NORMAL');self.db.execute('pragma journal_size_limit=8388608')
            self.db.execute('create table if not exists samples(t integer primary key,cpu real,memory real,gpu real,interval integer,payload blob not null)')
            self.db.commit()
    def append(self,row):
        mem=[float(x.strip()) for x in row['Mem'].split(' MiB')[0].split('/')]
        gpu=[number(g.get('Util')) for g in row.get('Gpus',[])];gpu=[v for v in gpu if v is not None]
        payload=zlib.compress(json.dumps(row,ensure_ascii=False,separators=(',',':'),allow_nan=False).encode(),3)
        if len(payload)>1048576:raise ValueError('sample payload exceeds limit')
        self.db.execute('insert or ignore into samples values(?,?,?,?,?,?)',(row['T'],number(row['Cpu']),mem[0]/mem[1]*100 if mem[1] else None,sum(gpu)/len(gpu) if gpu else None,row['Interval'],payload));self.db.commit()
    def retain(self,days=30,limit=1073741824):
        cutoff=TICKS+(time.time_ns()//100)-int(days*86400*10000000)
        self.db.execute('delete from samples where t<?',(cutoff,));self.db.commit()
        def pages(pragma):
            return self.db.execute('pragma '+pragma).fetchone()[0]
        def compact():
            # Consume PRAGMA cursors so SQLite completes compaction. A reader may
            # pin the WAL: physical size must never trigger additional deletion.
            self.db.execute('pragma incremental_vacuum').fetchall()
            self.db.execute('pragma wal_checkpoint(TRUNCATE)').fetchall()
        compact()
        page_size=pages('page_size')
        while (pages('page_count')-pages('freelist_count'))*page_size>limit:
            before=self.db.execute('select count(*) from samples').fetchone()[0]
            if before<=1:break
            self.db.execute('delete from samples where t in (select t from samples order by t limit ?)',(max(1,before//10),));self.db.commit()
        compact()
    def read(self,t):
        row=self.db.execute('select payload from samples where t=?',(t,)).fetchone()
        return json.loads(zlib.decompress(row[0])) if row else None
    def summary(self,start,end,limit=2800):
        rows=list(self.db.execute('select t,cpu,memory,gpu,interval from samples where t>=? and t<=? order by t',(start,end)))
        # Preserve first/last plus extrema of every metric and actual acquisition gaps.
        chosen=set();step=max(1,math.ceil(len(rows)/max(1,limit//8)))
        for i in range(0,len(rows),step):
            block=rows[i:i+step];chosen.update([block[0][0],block[-1][0]])
            for metric in [1,2,3]:
                valid=[r for r in block if r[metric] is not None]
                if valid:chosen.update([min(valid,key=lambda r:r[metric])[0],max(valid,key=lambda r:r[metric])[0]])
        result=[];previous=None;gap=False
        for row in rows:
            if previous and row[0]-previous[0]>max(15,row[4]*3,previous[4]*3)*10000000:gap=True
            if row[0] in chosen:result.append(dict(t=str(row[0]),time=(row[0]-TICKS)//10000,cpu=row[1],memory=row[2],gpu=row[3],interval=row[4],gapBefore=gap));gap=False
            previous=row
        return dict(schema=1,total=len(rows),points=result)
    def status(self):
        count,first,last=self.db.execute('select count(*),min(t),max(t) from samples').fetchone()
        return dict(schema=1,installed=True,version=VERSION,count=count,first=str(first) if first else None,last=str(last) if last else None,lastTime=(last-TICKS)//10000 if last else None,healthy=bool(last and TICKS+time.time_ns()//100-last<300000000))

def main():
    p=argparse.ArgumentParser();p.add_argument('action',choices=['run','once','status','query','read']);p.add_argument('--root',default=DEFAULT_ROOT);p.add_argument('--interval',type=int,default=5);p.add_argument('--after',type=int,default=TICKS);p.add_argument('--before',type=int,default=TICKS+time.time_ns()//100);p.add_argument('--ticks',type=int);args=p.parse_args()
    if args.action in ['status','query','read']:
        try:
            store=Store(args.root,True)
            output=store.status() if args.action=='status' else store.summary(args.after,args.before) if args.action=='query' else store.read(args.ticks)
        except (sqlite3.Error,OSError):output=dict(installed=False,healthy=False,error='Recorder data unavailable')
        print(json.dumps(output,ensure_ascii=False,allow_nan=False));return
    interval=max(2,min(60,args.interval));store=Store(args.root);sampler=Sampler(interval);sampler.sample();time.sleep(.25)
    stopped=False
    def stop(*_):
        nonlocal stopped
        stopped=True
    signal.signal(signal.SIGTERM,stop);signal.signal(signal.SIGINT,stop)
    while not stopped:
        started=time.monotonic()
        try:
            store.append(sampler.sample())
            if args.action=='once':break
            if int(time.time())//300!=getattr(store,'cleaned',None):store.retain();store.cleaned=int(time.time())//300
        except (OSError,sqlite3.Error,ValueError) as error:print(type(error).__name__+': collection postponed',flush=True)
        while not stopped and time.monotonic()-started<interval:time.sleep(min(.25,interval))
    store.db.close()
if __name__=='__main__':main()
