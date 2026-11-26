import test from 'node:test';import assert from 'node:assert/strict';import {pathCrumbs} from './path-address.mjs';
test('drive ancestors have independent absolute targets',()=>assert.deepEqual(pathCrumbs('C:\\Users\\lenovo').map(x=>x.path),['C:\\','C:\\Users','C:\\Users\\lenovo']));
test('UNC share is a root and never navigates above its share',()=>assert.deepEqual(pathCrumbs('\\\\server\\share\\Docs').map(x=>x.path),['\\\\server\\share','\\\\server\\share\\Docs']));
test('POSIX names preserve backslashes and Unicode',()=>assert.deepEqual(pathCrumbs('/home/a\\b/资料').map(x=>x.path),['/','/home','/home/a\\b','/home/a\\b/资料']));

test('visit history follows resolved directories, not parents or refreshes',async()=>{
 const {visitDirectory}=await import('./path-address.mjs');let h=null;
 for(const p of ['/home/a/nested','/home/b','/home/b'])h=visitDirectory(h,p);
 assert.deepEqual(h,{entries:['/home/a/nested','/home/b'],cursor:1});
 h=visitDirectory(h,'/home/a/nested',0);assert.equal(h.cursor,0);
 h=visitDirectory(h,'/home/a');assert.deepEqual(h,{entries:['/home/a/nested','/home/a'],cursor:1});
});
