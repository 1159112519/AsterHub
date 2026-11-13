import test from 'node:test';
import assert from 'node:assert/strict';
import {mergeReading} from './device-state.js';
const full=(time=1000)=>({deviceId:'local',time,cpu:10,memory:25,mem:'4096 / 16384 MiB',gpus:[{Index:'0',Util:'12',Name:'GPU'}],processes:[],cores:[],interval:3});
const light=(time=1500)=>({deviceId:'local',sequence:1,time,source:'windows-light',intervalMs:500,cpu:42,totalBytes:16*1024**3,availableBytes:10*1024**3,durationMs:.1,error:''});
test('a full snapshot keeps its independent light lane without adding light points to the archive lane',()=>{
 const old={light:{good:light()},lightSamples:[{time:1500,cpu:42}]};
 const next=mergeReading(old,full(2000),2000);
 assert.equal(next.light,old.light);assert.equal(next.lightSamples,old.lightSamples);assert.equal(next.samples.length,1);
});
const load=()=>import('./light-telemetry.js');
test('light updates only local CPU and memory while preserving full time and GPU inventory',async()=>{
 const {mergeLightReading,overlayLightReading}=await load();const original=mergeReading(null,full(),1000);
 const next=mergeLightReading(original,light(),1500);const shown=overlayLightReading(next.sample,next.light,1500);
 assert.equal(shown.cpu,42);assert.equal(shown.memory,37.5);assert.equal(shown.memUsed,6);assert.equal(shown.memTotal,16);
 assert.equal(shown.time,1000);assert.equal(shown.cpuTime,1500);assert.equal(shown.memoryTime,1500);
 assert.equal(shown.gpus,original.sample.gpus);assert.equal(next.samples,original.samples);assert.equal(next.lastAttempt,1000);
 assert.equal(next.lightSamples.length,1);assert.equal(next.lightSamples[0].time,1500);assert.equal(next.lightSamples[0].interval,.5);
});
test('light errors and silence preserve values and report light staleness without changing the full connection lane',async()=>{
 const {mergeLightReading,overlayLightReading}=await load();let state=mergeLightReading(mergeReading(null,full(),1000),light(),1500);
 state=mergeLightReading(state,{...light(2000),sequence:2,error:'Read API unavailable',cpu:null,totalBytes:null,availableBytes:null},2000);
 const shown=overlayLightReading(state.sample,state.light,2000);assert.equal(shown.cpu,42);assert.equal(shown.memUsed,6);assert.equal(shown.light.stale,true);assert.equal(shown.cpuTime,1500);assert.equal(state.stale,false);assert.equal(state.samples.length,1);
 assert.equal(overlayLightReading(state.sample,{...state.light,error:''},5001).light.stale,true);
});
test('duplicate, backwards, invalid and remote light events do not replace the local cache',async()=>{
 const {mergeLightReading}=await load();const state=mergeLightReading(null,light(),1500);
 assert.equal(mergeLightReading(state,light(),1500),state);
 assert.equal(mergeLightReading(state,{...light(1400),sequence:2},1500),state);
 assert.equal(mergeLightReading(state,{...light(1600),deviceId:'remote',sequence:2},1600),state);
 const bad=mergeLightReading(state,{...light(1600),sequence:2,availableBytes:100*1024**3},1600);assert.equal(bad.light.good,state.light.good);assert.ok(bad.light.error);
});
test('warmup CPU stays unknown and no old full sample is required for a local memory display',async()=>{
 const {mergeLightReading,overlayLightReading}=await load();const state=mergeLightReading(null,{...light(),cpu:null},1500);
 const shown=overlayLightReading({deviceId:'local',time:null,cpu:null,gpus:[],memory:null},state.light,1500);
 assert.equal(shown.cpu,null);assert.equal(shown.memory,37.5);assert.equal(shown.time,null);assert.equal(state.sample,undefined);
});
test('light ring is bounded by its own time span and never fills full GPU points',async()=>{
 const {mergeLightReading}=await load();let state=null;
 for(let i=0;i<400;i++)state=mergeLightReading(state,{...light(i*500+1),sequence:i+1},i*500+1);
 assert.ok(state.lightSamples.length<=1801);assert.equal(state.lightSamples.at(-1).time,199501);assert.equal(state.samples,undefined);assert.equal(state.lightSamples[0].gpus,undefined);
});

test('CPU baseline recovery keeps the previous known light CPU and its real acquisition time',async()=>{
 const {mergeLightReading,overlayLightReading}=await load();let state=mergeLightReading(mergeReading(null,full(),1000),light(),1500);
 state=mergeLightReading(state,{...light(2000),sequence:2,error:'failed'},2000);
 state=mergeLightReading(state,{...light(2500),sequence:3,cpu:null},2500);
 const shown=overlayLightReading(state.sample,state.light,2500);assert.equal(shown.cpu,42);assert.equal(shown.cpuTime,1500);assert.equal(shown.memoryTime,2500);assert.equal(state.lightSamples.at(-1).cpu,null);
});

test('resource chart helper joins older full samples with real recent light points only for CPU or memory',async()=>{
 const {resourceChartSamples}=await load();const full=[{time:1000,cpu:1,gpus:[{id:0,util:3}]},{time:2000,cpu:2,gpus:[{id:0,util:4}]},{time:3000,cpu:3,gpus:[{id:0,util:5}]}];const fast=[{time:2000,cpu:42,memory:38,interval:.5},{time:2500,cpu:null,memory:null,gap:true,interval:.5},{time:3000,cpu:20,memory:39,gapBefore:true,interval:.5}];
 const cpu=resourceChartSamples(full,fast,'cpu');assert.deepEqual(cpu.map(p=>p.time),[1000,2000,2500,3000]);assert.equal(cpu[1].cpu,42);assert.equal(cpu[2].gap,true);assert.equal(cpu[3].gapBefore,true);assert.equal(cpu[1].gpus,undefined);assert.equal(resourceChartSamples(full,fast,'gpu'),full);assert.equal(resourceChartSamples(full,fast,'network'),full);assert.equal(resourceChartSamples(full,[],'cpu'),full);
});
test('light read failure records a real gap in the fast lane without changing retained values',async()=>{
 const {mergeLightReading}=await load();let state=mergeLightReading(null,light(),1500);state=mergeLightReading(state,{...light(2000),sequence:2,error:'read failed'},2000);assert.equal(state.lightSamples.at(-1).gap,true);assert.equal(state.lightSamples.at(-1).cpu,null);assert.equal(state.light.good.cpu,42);
});


test('light interruptions use their own cadence even when older full samples dominate the chart',async()=>{
 const {resourceChartSamples}=await load();const full=Array.from({length:20},(_,i)=>({time:i*3000,cpu:10,interval:3}));
 const fast=[{time:60000,cpu:20,memory:30,interval:.5},{time:60520,cpu:21,memory:31,interval:.5},{time:62520,cpu:22,memory:32,interval:.5,error:'retained diagnostic'},{time:63020,cpu:23,memory:33,interval:.5,gapBefore:true}];
 const rows=resourceChartSamples(full,fast,'cpu'),recent=rows.slice(-4);
 assert.equal(recent[0].gapBefore,undefined);assert.equal(recent[1].gapBefore,undefined);assert.equal(recent[2].gapBefore,true);assert.equal(recent[2].error,'retained diagnostic');assert.equal(recent[3].gapBefore,true);
 assert.equal(fast[2].gapBefore,undefined);assert.equal(resourceChartSamples(full,fast,'gpu'),full);
});

test('continuous mixed cadence keeps a single segment while outages and explicit failures remain gaps',async()=>{const{resourceChartSamples}=await load();const full=[{time:0,cpu:10,interval:3},{time:3000,cpu:12,interval:3}],fast=[{time:5900,cpu:15,interval:.5},{time:6400,cpu:16,interval:.5}];let rows=resourceChartSamples(full,fast,'cpu');assert(rows.every(p=>!p.gapBefore));rows=resourceChartSamples(full,[{time:20000,cpu:15,interval:.5}],'cpu');assert.equal(rows.at(-1).gapBefore,true);rows=resourceChartSamples(full,[{time:5900,cpu:15,interval:.5,gapBefore:true}],'cpu');assert.equal(rows.at(-1).gapBefore,true);rows=resourceChartSamples([],[{time:1000,cpu:10,interval:.5},{time:2900,cpu:11,interval:2}],'cpu');assert.equal(rows.at(-1).gapBefore,undefined)});

test('old and recent real outages both remain gaps after merging sampling clocks',async()=>{const{resourceChartSamples}=await load(),rows=resourceChartSamples([{time:0,cpu:10,interval:3},{time:30000,cpu:12,interval:3}],[{time:32900,cpu:15,interval:.5},{time:34900,cpu:16,interval:.5}],'cpu');assert.equal(rows[1].gapBefore,true);assert.equal(rows[2].gapBefore,undefined);assert.equal(rows[3].gapBefore,true)});


test('successful slow full collection is not cut at every scheduled-interval boundary',async()=>{
 const {resourceChartSamples}=await load();
 const full=Array.from({length:130},(_,i)=>({time:i*6004,cpu:12+i%35,interval:2}));
 const start=full.at(-1).time+2000,fast=Array.from({length:61},(_,i)=>({time:start+i*2000,cpu:25,interval:2}));
 const rows=resourceChartSamples(full,fast,'cpu');
 assert.equal(rows.filter(p=>p.gapBefore).length,0);
 const outage=full.map((p,i)=>({...p,time:p.time+(i>=80?60000:0)}));
 const afterOutage=resourceChartSamples(outage,fast.map(p=>({...p,time:p.time+60000})),'cpu');
 assert.equal(afterOutage.filter(p=>p.gapBefore).length,1);
 assert.equal(afterOutage[80].gapBefore,true);
});

test('fast samples keep the full fifteen-minute chart instead of being replaced after two minutes',async()=>{
 const {mergeLightReading}=await load();let state=null;
 for(let i=0;i<=3700;i++)state=mergeLightReading(state,{...light(1+i*250),intervalMs:250,sequence:i+1},1+i*250);
 assert(state.lightSamples.length>=3601&&state.lightSamples.length<=3604);
 assert(state.lightSamples.at(-1).time-state.lightSamples[0].time>=900000);
 assert.equal(state.samples,undefined);
});

test('memory-only light counters leave full CPU history available on multi-group Windows',async()=>{
 const {resourceChartSamples}=await load();
 const full=Array.from({length:10},(_,i)=>({time:i*3000,cpu:10+i,memory:30,interval:3}));
 const fast=Array.from({length:20},(_,i)=>({time:500+i*1500,cpu:null,memory:35,interval:1.5}));
 assert.deepEqual(resourceChartSamples(full,fast,'cpu'),full);
 assert.equal(resourceChartSamples(full,fast,'memory').at(-1).memory,35);
});


test('memory-only light failure never takes ownership of a healthy full CPU lane',async()=>{
 const {resourceChartSamples}=await load();
 const full=Array.from({length:10},(_,i)=>({time:i*3000,cpu:10+i,memory:30,interval:3}));
 const fast=Array.from({length:20},(_,i)=>({time:500+i*1500,cpu:null,memory:i===4?null:35,interval:1.5,...(i===4?{gap:true}:{})}));
 assert.deepEqual(resourceChartSamples(full,fast,'cpu'),full);
 assert(resourceChartSamples(full,fast,'memory').some(p=>p.gap));
});

test('one-second healthy batches keep CPU and memory on the shared complete snapshot clock',async()=>{
 const {mergeLightReading,overlayLightReading,resourceChartSamples}=await load();let state=mergeLightReading(mergeReading(null,{...full(),interval:1},1000),{...light(),intervalMs:1000},1500);
 assert.equal(overlayLightReading(state.sample,state.light,1500),state.sample);assert.deepEqual(resourceChartSamples(state.samples,state.lightSamples,'cpu'),state.samples);
 state={...state,sample:{...state.sample,stale:true}};assert.equal(overlayLightReading(state.sample,state.light,1500).cpu,42);
});
