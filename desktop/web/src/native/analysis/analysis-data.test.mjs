import assert from 'node:assert/strict';
import test from 'node:test';
import {axisTicks,coreBins,gpuValues,memoryGiB,networkChanges,processes,rankChanges,lowMemorySpans} from './analysis-data.mjs';

test('time ticks grow with width; heatmap cells link to the real peak sample',()=>{
 for(const [width,count] of [[280,3],[420,4],[600,5],[900,7]]){
  const axis=axisTicks(1000,601000,width);
  assert.equal(axis.length,count);assert.equal(axis[0].time,1000);assert.equal(axis.at(-1).time,601000);
  assert(axis.every((tick,i)=>!i||tick.time>axis[i-1].time));
 }
 const {bins}=coreBins([{time:10,cores:[1,2],coreValues:[0,null]},{time:15,cores:[1],coreValues:[42]}],0,100,5);
 assert.deepEqual(bins[0].get(1),{time:15,value:42});assert.equal(bins[0].has(2),false);assert.equal(bins[1].size,0);
});

test('zero remains a reading, and a recycled PID is not compared as the old process',()=>{
 const a={processes:[{Pid:12,Identity:'12:old',Name:'app',Cpu:0,ResidentBytes:1000},{Pid:13,Identity:'13:stable',Name:'worker',Cpu:null,ResidentBytes:1024**3}]};
 const b={processes:[{Pid:12,Identity:'12:new',Name:'app',Cpu:10,ResidentBytes:1200},{Pid:13,Identity:'13:stable',Name:'worker',Cpu:1,ResidentBytes:2*1024**3}]};
 assert.deepEqual(processes('cpu',a).map(p=>p.value),[0]);
 assert.deepEqual(rankChanges(processes('memory',a),processes('memory',b)).map(p=>[p.pid,p.change]),[[13,1]]);
 assert.equal(processes('memory',{}),null);assert.equal(memoryGiB({mem:'1024 / 8192 MiB'}),1);
});

test('GPU UUIDs follow the same card after indexes reorder and unverified processes stay out',()=>{
 const p={gpus:[{Index:'1',Uuid:'card-a',Used:'0 MiB',Total:'2048 MiB'},{Index:'0',Uuid:'card-b',Used:'512 MiB',Total:'1024 MiB'}],gpuProcesses:[{Gpu:'0',IdentityToken:'other',Pid:'10',Memory:'20 MiB'},{Gpu:'1',Pid:'11',Memory:'20 MiB'},{Gpu:'1',IdentityToken:'match',Pid:'12',Name:'C:\\bin\\worker.exe',Memory:'32 MiB'}]};
 assert.equal(gpuValues(p,'uuid:card-a').used,0);assert.equal(gpuValues(p,'uuid:card-b').used,.5);
 assert.deepEqual(processes('gpu',p,'uuid:card-a').map(r=>r.name),['worker.exe']);
 const points=[{}, {}, {gapBefore:true}];const reading={free:1,total:8};
 assert.deepEqual(lowMemorySpans(points,[reading,reading,reading]),[{start:0,end:1,min:1},{start:2,end:2,min:1}]);
});

test('network counter reset or a disconnect does not count as complete traffic',()=>{
 const p=(time,n,gapBefore=false)=>({time,gapBefore,adapters:[{Id:'lan',ReceivedBytes:n,SentBytes:n,ReceiveErrors:0,SendErrors:0,ReceiveDiscards:0,SendDiscards:0}]});
 const samples=[p(0,10),p(1000,20),p(2000,2),p(3000,5),p(4000,15,true)];
 assert.deepEqual(networkChanges(samples,0,1),{complete:true,rows:[{id:'lan',name:'lan',down:10,up:10,errors:0,discards:0}]});
 assert.deepEqual(networkChanges(samples,0,4),{complete:false,rows:[{id:'lan',name:'lan',down:13,up:13,errors:0,discards:0}]});
 assert.deepEqual(networkChanges(samples,-1,-1),{complete:false,rows:[]});
});

test('whole-band drag preserves the time span at both boundaries and uses elapsed time',async()=>{
 const {shiftRange}=await import('./analysis-data.mjs');
 assert.deepEqual(shiftRange(20,50,11,0,100),[31,61]);
 assert.deepEqual(shiftRange(20,50,200,0,100),[70,100]);
 assert.deepEqual(shiftRange(20,50,-200,0,100),[0,30]);
});
test('GPU total refuses partial coverage and process comparisons keep the same PID separate by GPU',async()=>{
 const {gpuAggregate}=await import('./analysis-data.mjs');
 const a={gpus:[{Index:0,Uuid:'a',Used:'1024 MiB',Total:'4096 MiB'},{Index:1,Uuid:'b',Used:'2048 MiB',Total:'4096 MiB'}],gpuProcesses:[{Gpu:'0',IdentityToken:'same',Pid:'42',Name:'python',Path:'/usr/bin/python',Memory:'1024 MiB'},{Gpu:'1',IdentityToken:'same',Pid:'42',Name:'python',Memory:'2048 MiB'}]};
 const b={...a,gpuProcesses:a.gpuProcesses.map(p=>({...p,Memory:p.Gpu==='0'?'2048 MiB':'1024 MiB'}))};
 assert.deepEqual(gpuAggregate(a,['uuid:a','uuid:b']),{used:3,total:8,free:5});
 assert.equal(gpuAggregate({...a,gpus:a.gpus.slice(0,1)},['uuid:a','uuid:b']),null);
 const rows=rankChanges(processes('gpu',a,'all'),processes('gpu',b,'all'));
 assert.deepEqual(rows.map(r=>[r.gpu,r.change,r.path]),[[0,1,'/usr/bin/python'],[1,-1,null]]);
});
test('memory endpoints may use nearby captured process samples, but never cross a gap or reuse one sample',async()=>{
 const {memoryProcessEndpoints}=await import('./analysis-data.mjs');
 const data=[{time:0,processes:[{}]},{time:3000},{time:10000,processes:[{}]},{time:13000},{time:20000,gapBefore:true},{time:22000}];
 assert.deepEqual(memoryProcessEndpoints(data,1,3),[0,2]);
 assert.deepEqual(memoryProcessEndpoints(data,0,1),null);
 assert.deepEqual(memoryProcessEndpoints(data,1,5),null);
});
test('traffic units follow binary thresholds and rate/volume use independent units',async()=>{
 const {formatTraffic,trafficScale}=await import('./analysis-data.mjs');
 assert.equal(formatTraffic(512),'512 B');assert.equal(formatTraffic(2048),'2 KiB');
 assert.equal(formatTraffic(1024**3),'1 GiB');assert.equal(formatTraffic(1024**3,true),'1 GiB/s');
 assert.deepEqual(trafficScale(2000),{divisor:1024,unit:'KiB'});
});

test('GPU processes born at the same native timestamp remain separate by PID',()=>{
 const point={gpus:[{Index:0,Uuid:'card-a'}],gpuProcesses:[{Gpu:0,Pid:'51',IdentityToken:'win:100',Memory:'128 MiB'},{Gpu:0,Pid:'52',IdentityToken:'win:100',Memory:'256 MiB'}]};
 assert.deepEqual(processes('gpu',point,'all').map(p=>p.pid),['51','52']);
 const later={...point,gpuProcesses:[{Gpu:0,Pid:'51',IdentityToken:'win:200',Memory:'128 MiB'},{Gpu:0,Pid:'52',IdentityToken:'win:100',Memory:'512 MiB'}]};
 assert.deepEqual(rankChanges(processes('gpu',point,'all'),processes('gpu',later,'all')).map(p=>p.pid),['52']);
});
