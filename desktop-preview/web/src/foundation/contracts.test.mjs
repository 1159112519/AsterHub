import test from 'node:test';
import assert from 'node:assert/strict';
import {BoundedCache} from './bounded-cache.js';
import {createRequestScope} from './request-scope.js';
import {createPollingResource} from './polling-resource.js';
import {createDismissStack} from './dismiss-stack.js';
import {createPreferenceStore} from './preference-store.js';
const tick=()=>new Promise(setImmediate);
const deferred=()=>{let resolve;const promise=new Promise(r=>resolve=r);return {promise,resolve}};

test('cache preserves recently inspected items, bounds weight, and clears references',()=>{
 const c=new BoundedCache({maxEntries:3,maxWeight:7,weight:v=>v.length});
 c.set('a','aa');c.set('b','bb');c.get('a');c.set('c','cc');c.set('d','dd');
 assert.equal(c.has('b'),false);assert.equal(c.has('a'),true);assert.equal(c.size,3);
 assert.equal(c.set('huge','12345678'),false);assert.equal(c.has('huge'),false);assert.equal(c.size,3);
 c.clear();assert.equal(c.size,0);assert.equal(c.weight,0);
});
test('cache replacing an entry cannot leave an obsolete large value behind',()=>{
 const c=new BoundedCache({maxEntries:2,maxWeight:4,weight:v=>v.length});c.set('a','aa');c.set('a','12345');
 assert.equal(c.has('a'),false);assert.equal(c.weight,0);assert.throws(()=>new BoundedCache({maxEntries:0}),/capacity/i);
});
test('request scope cancels only superseded work in the same lane',()=>{
 const s=createRequestScope(),a=s.begin('range'),b=s.begin('detail'),next=s.begin('range');
 assert.equal(a.signal.aborted,true);assert.equal(a.isCurrent(),false);assert.equal(b.isCurrent(),true);assert.equal(next.isCurrent(),true);
 b.finish();assert.equal(b.isCurrent(),false);s.cancelAll();assert.equal(next.signal.aborted,true);assert.equal(s.size,0);
});
test('shared preference uses one watcher, ignores unchanged values, and releases it',()=>{
 let value=false,watchers=0,notify,changes=0;
 const s=createPreferenceStore(()=>value,fn=>{watchers++;notify=fn;return()=>watchers--});
 const a=s.subscribe(()=>changes++),b=s.subscribe(()=>changes++);assert.equal(watchers,1);notify();assert.equal(changes,0);
 value=true;notify();assert.equal(changes,2);a();a();assert.equal(watchers,1);b();assert.equal(watchers,0);
 value=false;assert.equal(s.getSnapshot(),false);
});
test('polling shares an in-flight request and drops obsolete late results after unsubscribe',async()=>{
 let calls=0,aborted=false;const wait=deferred();
 const r=createPollingResource({load:signal=>{calls++;signal.addEventListener('abort',()=>aborted=true);return wait.promise},interval:60000});
 const a=r.subscribe(()=>{}),b=r.subscribe(()=>{});await tick();r.refresh();assert.equal(calls,1);a();assert.equal(aborted,false);b();assert.equal(aborted,true);
 wait.resolve(['stale']);await tick();assert.equal(r.getSnapshot().data,null);assert.equal(r.diagnostics().subscribers,0);assert.equal(r.diagnostics().scheduled,false);
});
test('polling keeps the last good value on failure and reports no invented completion',async()=>{
 let fail=false;const r=createPollingResource({load:async()=>{if(fail)throw Error('offline');return [{id:1,status:'running'}]},interval:60000});
 const off=r.subscribe(()=>{});await tick();assert.equal(r.getSnapshot().data[0].status,'running');fail=true;await r.refresh();
 assert.equal(r.getSnapshot().data[0].status,'running');assert.equal(r.getSnapshot().error.message,'offline');off();r.dispose();assert.equal(r.getSnapshot().data,null);
});
test('only the topmost layer dismisses and shared listeners are released',()=>{
 const handlers=new Map();const root={addEventListener:(type,fn)=>handlers.set(type,fn),removeEventListener:type=>handlers.delete(type)};
 const s=createDismissStack(root);const seen=[];const a=s.add({contains:()=>false,onDismiss:()=>seen.push('parent')});
 const b=s.add({contains:target=>target==='inside',onDismiss:()=>seen.push('child')});
 handlers.get('pointerdown')({target:'inside'});assert.deepEqual(seen,[]);handlers.get('pointerdown')({target:'outside'});assert.deepEqual(seen,['child']);
 assert.equal(a.isTop(),false);b.remove();handlers.get('keydown')({key:'Escape',preventDefault(){},stopPropagation(){}});assert.deepEqual(seen,['child','parent']);a.remove();a.remove();assert.equal(handlers.size,0);
});
