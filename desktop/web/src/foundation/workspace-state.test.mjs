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

function storageFixture(){let data=null;return {getItem:()=>data,setItem:(_,value)=>{data=value},removeItem:()=>{data=null},text:()=>data};}
test('device navigation and safe pane options survive restart independently',()=>{
 const storage=storageFixture(),first=createWorkspaceState({storage});
 first.open('A','navigation',()=>({page:'overview'})).setState({page:'storage'});
 first.open('B','navigation',()=>({page:'overview'})).setState({page:'files'});
 first.open('A','files',()=>({})).setState({leftId:'B',rightId:'local',ratio:62,showTransfers:true});
 first.open('A','files-pane:local:B',()=>({listing:null,path:''}),{dependencies:['B']}).setState({path:'/project/A',listing:{password:'do-not-save'}});
 first.open('A','history',()=>({minutes:60,metric:'cpu',result:null})).setState({minutes:120,metric:'memory',result:{token:'do-not-save'}});
 const second=createWorkspaceState({storage});
 assert.equal(second.open('B','navigation',()=>({page:'overview'})).getSnapshot().page,'files');
 assert.equal(second.open('A','navigation',()=>({page:'overview'})).getSnapshot().page,'storage');
 assert.deepEqual(second.open('A','files',()=>({})).getSnapshot(),{leftId:'B',rightId:'local',ratio:62,showTransfers:true});
 assert.deepEqual(second.open('A','files-pane:local:B',()=>({listing:null,path:''}),{dependencies:['B']}).getSnapshot(),{listing:null,path:'/project/A'});
 assert.deepEqual(second.open('A','history',()=>({minutes:60,metric:'cpu',result:null})).getSnapshot(),{minutes:120,metric:'memory',result:null});
 assert.equal(storage.text().includes('do-not-save'),false);
});
test('restored defaults remain independent and late callbacks remain bound after persistence',()=>{
 const storage=storageFixture(),first=createWorkspaceState({storage}),a=first.open('A','navigation',()=>({page:'overview'}));
 a.setState({page:'memory'});const b=first.open('B','navigation',()=>({page:'overview'}));a.setState({page:'network'});
 assert.equal(b.getSnapshot().page,'overview');
 const restored=createWorkspaceState({storage});assert.equal(restored.open('A','navigation',()=>({page:'overview'})).getSnapshot().page,'network');
 assert.equal(restored.open('B','navigation',()=>({page:'overview'})).getSnapshot().page,'overview');
});
test('removing an unopened restored device clears dependent persisted panes and endpoint choices',()=>{
 const storage=storageFixture(),first=createWorkspaceState({storage});
 first.open('A','files',()=>({})).setState({leftId:'local',rightId:'B',ratio:60,showTransfers:false});
 first.open('A','files-pane:remote:B',()=>({path:''}),{dependencies:['B']}).setState({path:'/removed'});
 first.open('B','navigation',()=>({page:'overview'})).setState({page:'storage'});
 const second=createWorkspaceState({storage});second.retainDevices(['A','local']);
 const third=createWorkspaceState({storage});
 assert.equal(third.open('A','files-pane:remote:B',()=>({path:''})).getSnapshot().path,'');
 assert.equal(third.open('A','files',()=>({leftId:'local',rightId:'A',ratio:50,showTransfers:false})).getSnapshot().rightId,'A');
 assert.equal(third.open('B','navigation',()=>({page:'overview'})).getSnapshot().page,'overview');
});
test('unknown pages, credentials, action tokens and live status never enter persistent storage',()=>{
 const storage=storageFixture(),cache=createWorkspaceState({storage});
 cache.open('A','secret',()=>({})).setState({password:'secret-password',token:'secret-token'});
 cache.open('A','memory-tools',()=>({})).setState({tab:'cleanup',selected:['standby'],confirmation:{token:'secret-token'},policy:null,dirty:false,busy:true});
 cache.open('A','storage-tools',()=>({})).setState({tool:'analysis',path:'C:/work',layout:'list',query:'model',minimum:100,age:2,treeWidth:240,status:{job:{status:'running'}},transfer:{id:'once'},clipboard:{paths:['secret-password']}});
 assert.ok(storage.text());assert.equal(storage.text().includes('secret-password'),false);assert.equal(storage.text().includes('secret-token'),false);assert.equal(storage.text().includes('running'),false);assert.equal(storage.text().includes('once'),false);
 const restored=createWorkspaceState({storage});assert.deepEqual(restored.open('A','memory-tools',()=>({tab:'monitor',selected:null,policy:null,dirty:false})).getSnapshot(),{tab:'cleanup',selected:['standby'],policy:null,dirty:false});
 assert.equal(restored.open('A','storage-tools',()=>({status:null,transfer:null})).getSnapshot().status,null);
});
test('corrupt, unavailable or invalid persisted options fall back safely',()=>{
 const blocked={getItem(){throw Error('denied')},setItem(){throw Error('full')}};
 const cache=createWorkspaceState({storage:blocked}),handle=cache.open('A','navigation',()=>({page:'overview'}));handle.setState({page:'files'});assert.equal(handle.getSnapshot().page,'files');
 const storage=storageFixture(),bad=createWorkspaceState({storage});bad.open('A','navigation',()=>({page:'overview'})).setState({page:'execute-dangerous-command'});
 assert.equal(createWorkspaceState({storage}).open('A','navigation',()=>({page:'overview'})).getSnapshot().page,'overview');
});

test('inventory pruning removes endpoint choices even when that endpoint never opened its own workspace',()=>{
 const storage=storageFixture(),cache=createWorkspaceState({storage});
 cache.open('A','files',()=>({leftId:'local',rightId:'A'})).setState({leftId:'local',rightId:'never-opened',ratio:55});
 cache.retainDevices(['A','local']);
 assert.equal(createWorkspaceState({storage}).open('A','files',()=>({leftId:'local',rightId:'A'})).getSnapshot().rightId,'A');
});

test('all offered manual memory areas and an unsaved policy remain inert preferences after reload',()=>{
 const storage=storageFixture(),cache=createWorkspaceState({storage}),selected=['allstandby','workset','filecache','modified','registry','combine'];
 const policy={enabled:true,threshold:91,durationMinutes:6,cooldownMinutes:45,areas:['standby','registry']};
 cache.open('local','memory-tools',()=>({})).setState({tab:'policy',selected,policy,dirty:true,token:'not-restorable'});
 const restored=createWorkspaceState({storage}).open('local','memory-tools',()=>({tab:'monitor',selected:null,policy:null,dirty:false})).getSnapshot();
 assert.deepEqual(restored,{tab:'policy',selected,policy,dirty:true});assert.equal(storage.text().includes('not-restorable'),false);
});

test('storage directory navigation is stored as inert metadata rather than a live snapshot',()=>{
 const storage=storageFixture(),cache=createWorkspaceState({storage});
 cache.open('A','storage-tools',()=>null).setState({tool:'analysis',status:{snapshot:{id:'scan-1',root:'/data'},job:{status:'running'}},nodeId:'9',offset:100,expanded:new Set(['0','9']),history:{entries:[{id:'0',directory:true},{id:'9',directory:true}],cursor:1}});
 const view=createWorkspaceState({storage}).open('A','storage-tools',()=>null).getSnapshot();
 assert.equal(view.snapshotId,'scan-1');assert.equal(view.nodeId,'9');assert.deepEqual(view.expanded,['0','9']);assert.equal(view.history.cursor,1);assert.equal(view.status,undefined);assert.equal(storage.text().includes('running'),false);
});
