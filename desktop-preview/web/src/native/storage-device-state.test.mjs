import test from 'node:test';
import assert from 'node:assert/strict';
import {createStorageDeviceState} from './storage-device-state.js';
const turn=()=>new Promise(resolve=>setTimeout(resolve,0));
test('switching devices retains each snapshot, tool and directory without starting scans',async()=>{
 const calls=[];const store=createStorageDeviceState({request:async(method,{deviceId})=>{calls.push(method);return {deviceId,snapshot:{id:'snapshot-'+deviceId}}}});
 const local=store.resource('local'),off=local.subscribe(()=>{});await turn();
 store.save('local',{tool:'analysis',nodeId:'19',status:local.getSnapshot().data});off();
 store.save('linux',{tool:'large',nodeId:'0'});
 assert.equal(store.read('local').nodeId,'19');assert.equal(store.read('linux').tool,'large');
 assert.equal(store.resource('local'),local);assert.equal(local.getSnapshot().data.snapshot.id,'snapshot-local');
 assert.deepEqual(local.diagnostics(),{subscribers:0,pending:false,scheduled:false,disposed:false});
 assert.equal(calls.includes('storage.scan'),false);
});
test('releasing a device cancels pending waits while preserving the last confirmed snapshot',async()=>{
 let count=0,signal;const store=createStorageDeviceState({request:async(method,p,options)=>{signal=options.signal;if(!count++)return {snapshot:{id:'complete'}};return new Promise(()=>{})}});
 const resource=store.resource('local');let off=resource.subscribe(()=>{});await turn();off();off=resource.subscribe(()=>{});await turn();off();
 assert.equal(signal.aborted,true);assert.equal(resource.getSnapshot().data.snapshot.id,'complete');assert.equal(resource.diagnostics().scheduled,false);
});
test('bounded cache evicts inactive data without disposing subscribed devices',()=>{
 const store=createStorageDeviceState({limit:2,request:async()=>null});const live=store.resource('live'),off=live.subscribe(()=>{});
 store.save('old',{nodeId:'1'});store.save('recent',{nodeId:'2'});
 assert.equal(store.diagnostics().devices,2);assert.equal(live.diagnostics().disposed,false);assert.equal(store.read('recent').nodeId,'2');off();
});
test('saved folder pages remain bounded while retaining root and current ancestors',()=>{
 const store=createStorageDeviceState({request:async()=>null}),folders=Object.fromEntries(Array.from({length:100},(_,id)=>[String(id),{items:[{id}]}]));
 store.save('local',{folders,data:{crumbs:[{id:'0'},{id:'1'},{id:'2'}]}});
 const retained=store.read('local').folders;assert.equal(Object.keys(retained).length,32);assert(retained['0']);assert(retained['1']);assert(retained['2']);assert(retained['99']);
});
test('hundreds of device switches keep only the bounded most recent views',()=>{
 const store=createStorageDeviceState({request:async()=>null});for(let id=0;id<200;id++)store.save('device-'+id,{tool:'analysis',nodeId:'7'});
 assert.equal(store.diagnostics().devices,8);assert.equal(store.read('device-199').nodeId,'7');
});

test('tree width stays within accessible bounds and preserves map space',async()=>{
 const {storageTreeBounds,clampStorageTreeWidth}=await import('./storage-device-state.js');
 assert.deepEqual(storageTreeBounds(1000),{min:160,max:420});
 assert.deepEqual(storageTreeBounds(600),{min:160,max:314});
 assert.equal(clampStorageTreeWidth(999,600),314);
 assert.equal(clampStorageTreeWidth(-1,1000),160);
 assert.equal(clampStorageTreeWidth(undefined,1000),220);
 const store=createStorageDeviceState({request:async()=>null});
 store.save('A',{treeWidth:340});store.save('B',{treeWidth:185});
 assert.equal(store.read('A').treeWidth,340);assert.equal(store.read('B').treeWidth,185);
});
