import test from 'node:test';import assert from 'node:assert/strict';import {toggleMemoryArea,memoryEmphasis,sortMemoryProcesses,supportedMemorySelection,memoryAreas,memoryProcessIdentity,memoryAreaSize,memoryAreaResult} from './memory-scope.js';
test('standby scopes are exclusive and other selections survive',()=>{assert.deepEqual(toggleMemoryArea(['standby','workset'],'allstandby'),['workset','allstandby']);assert.deepEqual(toggleMemoryArea(['workset','allstandby'],'standby'),['workset','standby']);assert.deepEqual(toggleMemoryArea(['standby'],'standby'),[])});
test('hover previews only eligible targets; a selected scope wins over hover',()=>{assert.equal(memoryEmphasis(['standby'],'workset','workset'), 'preview');assert.equal(memoryEmphasis(['workset'],null,'workset'), 'selected');assert.equal(memoryEmphasis(['workset'],'workset','workset',false), 'idle');assert.equal(memoryEmphasis(['standby'],'allstandby','low'),'selected');assert.equal(memoryEmphasis(['standby'],'allstandby','other'),'preview');assert.equal(memoryEmphasis([],null,'other'),'idle')});
test('selection groups eligible processes and restores the same active sort on cancellation',()=>{const source=[{pid:1,workingSetBytes:9,canTrim:false},{pid:2,workingSetBytes:7,canTrim:true},{pid:3,workingSetBytes:3,canTrim:true}],original=sortMemoryProcesses(source,[]).map(p=>p.pid);assert.deepEqual(original,[1,2,3]);assert.deepEqual(sortMemoryProcesses(source,['workset']).map(p=>p.pid),[2,3,1]);assert.deepEqual(sortMemoryProcesses(source,[]).map(p=>p.pid),original);assert.deepEqual(sortMemoryProcesses(source,['workset'],false).map(p=>p.pid),[3,2,1]);assert.deepEqual(source.map(p=>p.pid),[1,2,3])});
test('unsupported selected options cannot be sent for execution',()=>{assert.deepEqual(supportedMemorySelection(['standby','workset'],{standby:{available:false},workset:{available:true}}),['workset']);assert.deepEqual(supportedMemorySelection(['standby'],null),[])});

test('extended areas have distinct native operation identifiers',()=>{assert.deepEqual(memoryAreas.map(x=>x.id),['standby','allstandby','workset','filecache','modified','registry','combine']);assert.deepEqual(supportedMemorySelection(['filecache','modified','registry','combine'],{filecache:{available:true},modified:{available:false},registry:{available:true},combine:{available:false}}),['filecache','registry'])});
test('process identity retains exact FILETIME and path without numeric coercion',()=>{const input={pid:51,path:'C:\\Owned\\Fixture.exe',startIdentity:'134067426645800001'};assert.deepEqual(memoryProcessIdentity(input),{pid:51,path:input.path,startTicks:'134067426645800001'});assert.throws(()=>memoryProcessIdentity({...input,startIdentity:134067426645800001}),/身份不可用/)});

test('protecting a visible process does not send it below the loaded result window',()=>{const rows=[{pid:1,workingSetBytes:99,canTrim:false,protectedProgram:true},{pid:2,workingSetBytes:7,canTrim:true}];assert.deepEqual(sortMemoryProcesses(rows,['workset']).map(p=>p.pid),[1,2])});

test('area size uses the matching counter and keeps zero distinct from unavailable',()=>{
 const reading={lowStandbyBytes:0,standbyBytes:4096,fileCacheWorkingSetBytes:8192,modifiedBytes:12288,cacheBytes:65536,availableBytes:99999};
 assert.deepEqual(memoryAreas.map(({id})=>memoryAreaSize(reading,id)),[0,4096,null,8192,12288,null,null]);
 assert.equal(memoryAreaSize({cacheBytes:65536},'filecache'),null);
});
test('area records keep signed observations, reported counts, and unknown values distinct',()=>{
 assert.deepEqual(memoryAreaResult({area:'modified',beforeBytes:8192,afterBytes:12288}),{kind:'observed',before:8192,after:12288,change:4096});
 assert.deepEqual(memoryAreaResult({area:'standby',beforeBytes:0,afterBytes:0}),{kind:'observed',before:0,after:0,change:0});
 assert.deepEqual(memoryAreaResult({area:'combine',combinedBytes:0,observedReductionBytes:123}),{kind:'combined',size:0});
 assert.deepEqual(memoryAreaResult({area:'modified',beforeBytes:null,afterBytes:0,observedReductionBytes:999}),{kind:'unavailable'});
 assert.deepEqual(memoryAreaResult({area:'registry',observedReductionBytes:123}),{kind:'status'});
 assert.deepEqual(memoryAreaResult({area:'filecache',observedReductionBytes:123}),{kind:'unavailable'});
 assert.deepEqual(memoryAreaResult({area:'allstandby',observedReductionBytes:123}),{kind:'unavailable'});
 assert.deepEqual(memoryAreaResult({area:'workset',observedReductionBytes:123}),{kind:'workset',size:123});
 assert.deepEqual(memoryAreaResult({area:'combine',status:'failed',message:'Windows rejected the operation',combinedBytes:0}),{kind:'failed',message:'Windows rejected the operation'});
});
