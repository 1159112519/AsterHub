import assert from 'node:assert/strict';
import test from 'node:test';
import {createUpdateStore} from './update-task.js';

test('native snapshot survives panel remount, ignores older responses and shares download command',async()=>{
 let receive,statusResolve;let calls=[];const client={subscribe(fn,type){assert.equal(type,'update-status');receive=fn},request(method){calls.push(method);if(method==='updates.status')return new Promise(resolve=>statusResolve=resolve);return Promise.resolve({revision:4,state:'downloading',progress:22,latest:'0.3.0-preview.99'})}};
 const store=createUpdateStore(client);let first;const off=store.subscribe(value=>first=value);
 receive({revision:2,state:'downloading',progress:17,latest:'0.3.0-preview.99'});assert.equal(first.progress,17);off();
 let reopened;const off2=store.subscribe(value=>reopened=value);assert.equal(reopened.progress,17);
 statusResolve({revision:1,state:'idle',progress:0});await Promise.resolve();assert.equal(reopened.progress,17);
 await store.request('download');assert.equal(reopened.progress,22);assert.equal(calls.filter(value=>value==='updates.download').length,1);
 receive({revision:5,state:'cancelled',progress:22});assert.equal(reopened.state,'cancelled');off2();
});

test('a completed native task does not trigger installation',async()=>{
 const calls=[];let receive;const store=createUpdateStore({subscribe(fn){receive=fn},request(method){calls.push(method);return Promise.resolve({revision:0,state:'idle'})}});store.subscribe(()=>{});receive({revision:3,state:'ready',ready:true,progress:100});await Promise.resolve();assert.equal(store.getSnapshot().state,'ready');assert.equal(calls.includes('updates.install'),false);
});
