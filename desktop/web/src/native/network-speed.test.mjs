import test from 'node:test';
import assert from 'node:assert/strict';
import {measureSpeed,MIB,STAGE_SECONDS,trafficCaps,migrateTrafficCap,migrateStageSeconds} from './network-speed.js';
function fixture(options={}){
 let now=0;const calls=[],updates=[];const controller=new AbortController();
 const call=async(action,p={})=>{
  calls.push({action,...p});
  if(action==='start')return {id:'fixture'};
  if(action==='cancel')return true;
  if(action==='udp')return {available:false};
  if(p.direction==='latency'){now+=10;return {ok:true,headersMs:p.node==='cf'?10:20};}
  if(options.abort){controller.abort();throw new DOMException('cancelled','AbortError');}
  if(options.fail?.(p)){now+=Math.min(300,p.timeout);return {ok:false,reason:'http',status:405};}
  const ms=Math.min(700,p.timeout);now+=ms;
  return ms<700?{ok:false,reason:'timeout'}:{ok:true,ms,headersMs:20};
 };
 return {calls,updates,controller,run:(extra={})=>measureSpeed({cap:0,choice:'auto',signal:controller.signal,call,onUpdate:(phase,value)=>updates.push({phase,value}),clock:()=>now,...extra})};
}
test('default stage is ten seconds and default traffic is unlimited, retaining explicit choices',()=>{
 assert.equal(STAGE_SECONDS,10);assert.equal(migrateStageSeconds(undefined),10);assert.equal(migrateStageSeconds(20),20);assert.equal(migrateStageSeconds(7),10);
 assert.equal(migrateTrafficCap(undefined),0);assert.equal(migrateTrafficCap(1024),0);assert.equal(migrateTrafficCap(1024,{explicit:true}),1024);assert.equal(migrateTrafficCap(100),100);
 assert.deepEqual(trafficCaps.map(([cap])=>cap),[20,50,100,500,1024,2048,0]);assert(trafficCaps.every(([cap,label])=>cap===0||/MiB|GiB/.test(label)));
});
test('GET-healthy node with rejected POST falls back only for upload and retains real download samples',async()=>{
 const f=fixture({fail:p=>p.node==='cf'&&p.direction==='up'}),r=await f.run();
 assert(r.down>0&&r.up>0);assert.equal(r.partial,false);assert.deepEqual(r.sources.down,['cf']);assert.deepEqual(r.sources.up,['la']);
 assert(r.failures.some(x=>x.direction==='up'&&x.node==='cf'&&x.status===405));assert(r.samples.up.every(x=>x.node==='la'));assert(r.samples.down.every(x=>x.node==='cf'));
 assert(f.calls.findIndex(x=>x.direction==='up')>f.calls.findLastIndex(x=>x.direction==='down'));assert.equal(f.calls.at(-1).action,'cancel');
});
test('unavailable POST everywhere keeps upload unknown without inventing zero or reusing download curve',async()=>{
 const f=fixture({fail:p=>p.direction==='up'}),r=await f.run();assert(r.down>0);assert.equal(r.up,null);assert.equal(r.partial,true);assert.deepEqual(r.samples.up,[]);
 assert.equal(new Set(f.calls.filter(x=>x.direction==='up').map(x=>x.node)).size,4);assert.equal(r.loss,null);assert.equal(r.udpUnavailable,true);
});
test('ten-second deadlines respect native request max and both successful phases end at their own deadline',async()=>{
 const f=fixture(),r=await f.run();assert.equal(r.stageSeconds,10);assert.equal(r.phaseSeconds.down,10);assert.equal(r.phaseSeconds.up,10);
 assert(f.calls.filter(x=>x.action==='request').every(x=>x.timeout>0&&x.timeout<=8500));assert(r.samples.down.length>1&&r.samples.up.length>1);
});
test('bounded traffic includes failed requests and preserves a separate upload budget',async()=>{
 const f=fixture({fail:p=>p.node==='cf'&&p.direction==='down'}),r=await f.run({cap:20});const transfers=f.calls.filter(x=>x.size>0);
 assert.equal(r.used,transfers.reduce((n,x)=>n+x.size,0));assert(r.used<=20*MIB-65536);assert(r.down>0&&r.up>0);
 assert(transfers.filter(x=>x.direction==='down').reduce((n,x)=>n+x.size,0)<=Math.floor((20*MIB-65536)/2));
});
test('unlimited cap permits non-Cloudflare nodes and selected phase durations persist in results',async()=>{
 const f=fixture(),r=await f.run({choice:'den',seconds:15});assert.equal(r.node,'Sharktech · 丹佛');assert.equal(r.stageSeconds,15);assert.equal(r.phaseSeconds.down,15);assert.equal(r.phaseSeconds.up,15);
});
test('cancellation is propagated and releases native run without starting upload',async()=>{
 const f=fixture({abort:true});await assert.rejects(f.run(),{name:'AbortError'});assert.equal(f.calls.at(-1).action,'cancel');assert(!f.calls.some(x=>x.direction==='up'));
});
