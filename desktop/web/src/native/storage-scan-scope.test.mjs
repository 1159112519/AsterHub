import test from 'node:test';import assert from 'node:assert/strict';import {chooseScanRoot,driveUsage,selectedScanPaths,scanSelectionPayload} from './storage-scan-scope.js';
test('initial path picks containing Windows drive despite slash/case and ignores unavailable volumes',()=>{const roots=[{path:'C:/',available:true},{path:'D:/',available:true},{path:'E:/',available:false}];assert.equal(chooseScanRoot(roots,'d:\\Downloads'), 'D:/');assert.equal(chooseScanRoot(roots,'E:/'), 'C:/');assert.equal(chooseScanRoot([],''),'')});
test('remote roots use longest complete path boundary, not partial names',()=>{const roots=[{path:'/'},{path:'/mnt/data'},{path:'/mnt/datab'}];assert.equal(chooseScanRoot(roots,'/mnt/datab/docs'),'/mnt/datab');assert.equal(chooseScanRoot(roots,'/mnt/database'),'/')});
test('unknown capacity never becomes an empty or full drive',()=>{assert.equal(driveUsage({totalBytes:null,usedBytes:null}),null);assert.equal(driveUsage({totalBytes:0,usedBytes:0}),null);assert.equal(driveUsage({totalBytes:100,usedBytes:30}),30);assert.equal(driveUsage({totalBytes:100,usedBytes:110}),100)});

test('disk multiselection preserves visible root order and excludes unavailable or removed disks',()=>{
 const roots=[{path:'C:/'},{path:'D:/'},{path:'E:/',available:false},{path:'F:/',kind:'NoRootDirectory'}];
 assert.deepEqual(selectedScanPaths(roots,['D:/','C:/','D:/','E:/','F:/','Z:/']),['C:/','D:/']);
 assert.deepEqual(selectedScanPaths(roots,[]),[]);
 assert.equal(scanSelectionPayload(['C:/']),'C:/');
 assert.deepEqual(scanSelectionPayload(['C:/','D:/']),{paths:['C:/','D:/']});
 assert.equal(scanSelectionPayload([]),null);
});
