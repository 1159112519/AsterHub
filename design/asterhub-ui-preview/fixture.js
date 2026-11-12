import {anchor,trendStart,step,count,historyValues} from './data.mjs';
import {taskbarDefaults} from '@aster/native/taskbar-layout.js';
// In-page transport only. No host objects, command runner, disk or network handlers.
const listeners=new Set();let selected='local';
const g=1024**3,m=1024**2;
const rows=[['local','本机','localhost',12,44,31,'NVIDIA GeForce RTX 4070'],['render','渲染工作站','192.0.2.21',29,67,52,'NVIDIA GeForce RTX 4090'],['train','训练服务器','192.0.2.32',42,86,73,'NVIDIA RTX 6000 Ada']];
const devices=rows.map(([id,name,host])=>({device:{id,name,host,port:22,isLocal:id==='local',username:'demo'},state:'connected'}));
const since=Date.now();
function sample(id='local',n=0,time=Date.now()){
 const [,name,host,cpu,gpu,memory,gpuName]=rows.find(x=>x[0]===id)||rows[0];const local=id==='local',volume=local?'C:\\':'/data';
 return {deviceId:id,time,cpu:Math.max(0,cpu+Math.round(5*Math.sin(n*.21))),memory,mem:`${Math.round(memory*.01*65536)} / 65536`,cpuFrequencyMHz:4130,cpuFrequencySource:local?'windows-performance':'linux-cpufreq',interval:2,deviceName:name,host,
 cores:Array.from({length:16},(_,Index)=>({Index,Util:Math.max(1,cpu+Math.round(10*Math.sin(Index)))})),
 gpus:[{Index:0,Name:gpuName,Util:gpu+Math.round(6*Math.sin(n*.3)),Used:7264+Math.round(256*Math.sin(n*.6)),Total:local?12288:24576,Temp:61,Fan:37,Power:139,Cap:285,Clock:2250,MemoryClock:10501},...(id==="train"?[{Index:1,Name:gpuName,Util:54,Used:11059+Math.round(320*Math.cos(n*.53)),Total:24576,Temp:58,Fan:34,Power:121,Cap:285,Clock:2100}]:[])],
 processes:[{Gpu:0,Pid:1852,Name:local?'C:/Demo/renderer.exe':'/opt/demo/bin/python',Memory:4096,User:'demo'}],
 hardware:{CpuModel:local?'Intel Core i7-13700K':'AMD Ryzen 9 7950X',LogicalProcessors:16,MemoryModules:[{Slot:'DIMM A2',CapacityBytes:32*g,Generation:'DDR5',SpeedMHz:5600},{Slot:'DIMM B2',CapacityBytes:32*g,Generation:'DDR5',SpeedMHz:5600}],OsName:local?'Windows 11':'Ubuntu 24.04 LTS'},
 network:{Interfaces:[{Id:'eth0',Name:'Ethernet',DownloadBytesPerSecond:14.2*m,UploadBytesPerSecond:2.1*m}],DownloadBytesPerSecond:14.2*m,UploadBytesPerSecond:2.1*m},storage:[{Id:"demo-physical",Name:"演示磁盘",ReadBytesPerSecond:14.2*m,WriteBytesPerSecond:2.1*m,QueueDepth:1,ActivePercent:18}],
 volumes:[{Name:volume,Path:volume,Label:local?'演示卷':'项目盘',TotalBytes:local?(960*g):960*g,FreeBytes:local?(142*g):38*g,AvailableBytes:local?(142*g):38*g,UsedBytes:local?(960*g)-(142*g):922*g,UsedPercent:local?100*(1-(142*g)/(960*g)):96,FileSystem:local?'NTFS':'ext4'}]};
}
function listing({endpointId,side,path}){const isLocal=endpointId==='local',root=isLocal?'D:/Demo':'/home/demo',here=path||root,parent=here===root?root:here.slice(0,here.lastIndexOf('/'))||root;return {path:here,parent,isLocal,items:[['checkpoints',true,0],['datasets',true,0],['训练记录.log',false,14382],['config.json',false,803],['readme.txt',false,1824]].map(([name,directory,size])=>({name,path:here+'/'+name,directory,bytes:size,size,modified:new Date(since-120000).toISOString()}))}}
const success=new Set(['app.uiReady','window.appearance','capture.interval','history.cancel','files.cancel','terminal.resize','terminal.write','terminal.close','desktop.save','updates.preferences.save']);
const empty=new Set(['files.transfers','storage.jobs','storage.cleanup.jobs','terminal.list','storage.locations','storage.directories','storage.trash.list']);
const now = Math.floor(since/6000)*6000;
const at = minutes=>new Date(since-minutes*60000).toISOString();
let desktop={taskbar:taskbarDefaults(),scale:100,density:'comfortable',localIntervalMs:2000,iconThemeId:'01',appliedIconId:'01'};
let transfer={id:'demo-transfer',status:'running',names:['训练快照'],sourceDeviceId:'local',targetDeviceId:'render',bytes:2*g,done:1.34*g,created:at(2)};
let update={revision:1,state:'error',available:true,latest:'0.4.3.3',time:at(6),error:'演示：下载连接中断'};
const scans=[{id:'demo-scan',deviceId:'local',root:'D:\示例工程',status:'ready',enumerated:14829,time:at(9)}];
function analysis(id,minutes){
 const from=now-(minutes||30)*60000,to=now,step=6000,n=Math.round((to-from)/step),wave=t=>Math.max(0,Math.sin(t*.028)*.58+Math.sin(t*.011+1)*.42),names=['Code.exe','renderer.exe','python.exe','MsMpEng.exe','msedge.exe','explorer.exe','SearchHost.exe','dwm.exe','node.exe','AsterHub.exe','pwsh.exe','RuntimeBroker.exe'];
 let counter=[0,0,0,0];
 const points=Array.from({length:n+1},(_,i)=>{
  const time=from+i*step,t=(time-now)/6000,w=wave(t),capture=sample(id,t,time),processes=names.map((Name,j)=>({Identity:'demo:'+j,Pid:1400+j*37,Name,Cpu:Math.max(.1,(j===0?15:8)/(1+j*.42)*(1+.55*Math.sin(t*.027+j))),ResidentBytes:Math.round((.18+(2.8/(j+1))*(1+.26*Math.cos(t*.031+j)))*g)}));
  const adapters=['以太网','Wi-Fi'].map((Name,j)=>{const down=(j===0?2.8+20*w:1.1+2.2*wave(t+60))*m,up=(j===0?.6+6*wave(t+14):.28+.7*w)*m;counter[j*2]+=Math.round(down*6);counter[j*2+1]+=Math.round(up*6);return {Id:'demo-net-'+j,Name,ReceivedBytes:counter[j*2],SentBytes:counter[j*2+1],DownloadBytesPerSecond:down,UploadBytesPerSecond:up,ReceiveErrors:0,SendErrors:0,ReceiveDiscards:j===0&&t>=-76?3:0,SendDiscards:0};});
  const gpus=capture.gpus.map((gpu,j)=>({...gpu,Uuid:'demo-gpu-'+j,Used:Math.round(gpu.Total*(.52+.36*w))}));
  return {time,interval:6,gapBefore:t===-113,cpu:15+61*w,memory:30+31*w,mem:`${Math.round(19500+12500*w)} / 65536`,cores:capture.cores.map(c=>c.Index),coreValues:capture.cores.map((_,j)=>Math.min(100,8+78*wave(t+j*7)+13*(1+Math.sin(t*.033+j))/2)),processes,gpus,gpuProcesses:gpus.flatMap((gpu,j)=>names.slice(0,7).map((Name,k)=>({Gpu:j,Name,Pid:1400+k*37,IdentityToken:'gpu:'+j+':'+k,Memory:Math.round(130+(1900/(k+1))*(1+.55*wave(t+k*16)))}))),adapters};
 }).filter(p=>p.time<=now-120*step||p.time>=now-113*step);
 return {from,to,points,thinned:false,skipped:0};
}
function result(method,p){const id=p.deviceId||selected;switch(method){
 case 'app.info':return {version:'0.4.3.2 · UI 演示',stage:'UI 演示',interval:2,dataRoot:'仅演示，不使用正式数据'};
 case 'desktop.settings':return structuredClone(desktop);
 case 'desktop.save':desktop={...desktop,...structuredClone(p),appliedIconId:p.iconThemeId||desktop.appliedIconId};return structuredClone(desktop);
 case 'analysis.query':return analysis(id,p.minutes);
 case 'taskbar.status':{
  const s=sample(id,seq),gpu=s.gpus[0],disk=s.storage[0],timeUtc=new Date(since+seq*2500).toISOString(),speed=1+.12*Math.sin(seq*.7);
  const rows={cpu:[s.cpu,'%'],memory:[s.memory,'%'],storage:[s.volumes[0].UsedPercent,'%'],gpu:[gpu.Util,'%'],cpuFrequency:[s.cpuFrequencyMHz/1000,'GHz',1],gpuFrequency:[gpu.Clock/1000,'GHz',1],gpuMemory:[gpu.Used/gpu.Total*100,'%'],gpuTemperature:[gpu.Temp,'°C'],gpuPower:[gpu.Power,'W'],gpuFan:[gpu.Fan,'%'],memoryUsed:[s.memory*.64,'GB',1],memoryFree:[(100-s.memory)*.64,'GB',1],down:[14.2*speed,'M/s',1],up:[2.1/speed,'M/s',1],storageRead:[disk.ReadBytesPerSecond/m,'M/s',1],storageWrite:[disk.WriteBytesPerSecond/m,'M/s',1],connections:[39,''],ping:[18,'ms'],loss:[0,'%']};
  return {snapshot:{device:s.deviceName,timeUtc:new Date().toISOString(),cpuCores:s.cores.map(c=>c.Util),values:Object.fromEntries(Object.entries(rows).map(([id,[value,unit,decimals=0]])=>[id,{id,value,unit,decimals,timeUtc,animate:true,stale:false}]))}};
 }
 case 'files.transfers':return [transfer];
 case 'files.cancel':transfer={...transfer,status:'cancelled',time:at(0)};return true;
 case 'storage.jobs':return scans;
 case 'updates.status':case 'updates.check':return update;
 case 'updates.download':update={...update,state:'downloading',progress:44,error:null};return update;
 case 'updates.cancel':update={...update,state:'cancelled'};return update;case 'window.telemetry':return true;
 case 'devices.list':return {items:devices,selected};case 'devices.select':if(rows.some(x=>x[0]===p.id))selected=p.id;return null;
 case 'snapshot.latest':return sample(id);case 'snapshot.light':return null;
 case 'history.query':{const end=p.toUtc||Date.now(),start=p.fromUtc||end-(p.minutes||60)*60000;const points=Array.from({length:33},(_,i)=>{const time=Math.floor(start+(end-start)*i/32),r=sample(id,i,time);return {id:String(time),time,cpu:r.cpu,gpu:r.gpus[0].Util,memory:r.memory}}).filter(q=>id!=='train'||q.time<since-52*60000||q.time>since-44*60000);return {total:points.length,points};}
 case 'history.read':return sample(id,0,Number(p.id)||Date.now());
 case 'recorder.status':return {enabled:true,available:true};
 case 'storage.status':return {deviceId:id,job:null,snapshot:null};
 case 'files.list':return listing(p);
 case 'terminal.open':case 'terminal.create':return {id:'demo-'+id,deviceId:id,title:'演示终端',kind:'demo'};
 case 'terminal.read':return {next:1,data:p.offset?'':'AsterHub UI demo\r\nNo shell is connected.\r\n',state:'running'};
 case 'updates.preferences':return {checkOnStartup:false};case 'updates.preferences.save':return {checkOnStartup:!!p.checkOnStartup};case 'updates.startup.state':return null;
 case 'memory.status':return {processes:[],snapshot:{totalBytes:64*g,usedBytes:20*g,availableBytes:44*g},capabilities:{},manualAreas:[],policy:{enabled:false},records:[]};
 default:if(success.has(method))return null;if(empty.has(method))return [];throw new Error('此操作在 UI 演示中不可用');
}}
const emit=data=>listeners.forEach(listener=>listener({data}));
const seeded=new Set();
function seed(id){if(seeded.has(id)||!rows.some(row=>row[0]===id))return;seeded.add(id);const [, , ,cpu, ,memory]=rows.find(row=>row[0]===id);for(let i=0;i<count;i++){const time=trendStart+i*step;const v=historyValues(i,cpu,memory),entry=sample(id,i,time);emit({type:"capture",value:{...entry,...v,mem:`${Math.round(v.memory*.01*65536)} / 65536`}});}}
let seq=0;const timer=setInterval(()=>emit({type:'capture',value:sample(selected,++seq)}),2500);timer?.unref?.();
export const fixtureTransport={addEventListener(name,fn){if(name==='message')listeners.add(fn)},removeEventListener(name,fn){if(name==='message')listeners.delete(fn)},postMessage({id,method,params}){queueMicrotask(()=>{try{if(method==="snapshot.latest")seed(params?.deviceId||selected);emit({id,ok:true,result:result(method,params||{})})}catch(error){emit({id,ok:false,error:error.message})}})}};







