import test from 'node:test';
import assert from 'node:assert/strict';
import {createWorkspaceState,isWorkspaceResultFresh} from './workspace-state.js';

test('page defaults run once and remembered values survive reopen',()=>{
 const cache=createWorkspaceState();let defaults=0;
 const a=cache.open('A','history',()=>{defaults++;return {metric:'cpu'}});
 a.setState(v=>({...v,metric:'gpu'}));
 const again=cache.open('A','history',()=>{defaults++;return {metric:'memory'}});
 assert.equal(again.getSnapshot().metric,'gpu');assert.equal(defaults,1);
});
test('bound writes and asynchronous old callbacks never move A state into B',()=>{
 const cache=createWorkspaceState(),a=cache.open('A','history',()=>({metric:'cpu'})),b=cache.open('B','history',()=>({metric:'memory'}));
 const late=a.setState;late({metric:'gpu'});
 assert.equal(a.getSnapshot().metric,'gpu');assert.equal(b.getSnapshot().metric,'memory');
});
test('removed or evicted handles cannot resurrect or mutate a newly added same-ID device',()=>{
 const cache=createWorkspaceState(),old=cache.open('A','history',()=>({metric:'cpu'}));let changes=0;
 old.subscribe(()=>changes++);old.setState({metric:'gpu'});cache.removeDevice('A');
 const fresh=cache.open('A','history',()=>({metric:'memory'}));old.setState({metric:'stale'});
 assert.equal(fresh.getSnapshot().metric,'memory');assert.equal(changes,2);
});
test('twenty-device LRU capacity is enforced and reopen marks recent use',()=>{
 const cache=createWorkspaceState();for(let i=0;i<20;i++)cache.open('d'+i,'page',()=>({i}));
 cache.open('d0','page',()=>null);cache.open('d20','page',()=>({i:20}));
 assert.equal(cache.diagnostics().devices,20);assert.equal(cache.has('d0','page'),true);assert.equal(cache.has('d1','page'),false);
});
test('per-device page capacity is bounded at twenty and cannot be resurrected by an evicted setter',()=>{
 const cache=createWorkspaceState(),old=cache.open('A','p0',()=>0);
 for(let i=1;i<=20;i++)cache.open('A','p'+i,()=>i);
 old.setState(100);assert.equal(cache.has('A','p0'),false);assert.equal(cache.diagnostics().pages,20);
});
test('deleting an endpoint also purges dependent panes stored under other workspaces',()=>{
 const cache=createWorkspaceState(),pane=cache.open('A','files:right:B',()=>({path:'/secret-task'}),{dependencies:['B']});
 cache.open('A','history',()=>({metric:'gpu'}));cache.open('B','network',()=>({adapter:'eth0'}));cache.removeDevice('B');
 pane.setState({path:'/late'});
 assert.equal(cache.has('A','files:right:B'),false);assert.equal(cache.has('A','history'),true);assert.equal(cache.has('B','network'),false);
});
test('authoritative inventory pruning clears removed dependencies and leaves known workspaces',()=>{
 const cache=createWorkspaceState();cache.open('A','files:left:C',()=>0,{dependencies:['C']});cache.open('A','history',()=>1);cache.open('C','history',()=>2);
 cache.retainDevices(['A','local']);assert.deepEqual(cache.diagnostics(),{devices:1,pages:1});assert.equal(cache.has('A','history'),true);
});
test('cached history freshness has a finite range-specific TTL while stale results remain readable',()=>{
 const result={key:60,fetchedAt:1000,points:[1,2]};
 assert.equal(isWorkspaceResultFresh(result,60,30000,30999),true);
 assert.equal(isWorkspaceResultFresh(result,60,30000,31000),false);
 assert.equal(isWorkspaceResultFresh(result,120,30000,2000),false);
 assert.equal(isWorkspaceResultFresh(result,60,30000,999),false);
 assert.deepEqual(result.points,[1,2]);
});
