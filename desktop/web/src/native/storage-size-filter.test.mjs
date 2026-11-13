import assert from 'node:assert/strict';
import test from 'node:test';
import {parseStorageSize,storageQueryMinimum,storageSizePresets} from './storage-size-filter.js';

test('presets and decimal input produce exact bytes; the bridge minimum excludes a file exactly at the threshold',()=>{
 assert.deepEqual(storageSizePresets.map(x=>x.bytes),[0,100*2**20,500*2**20,2**30,3*2**30,5*2**30,10*2**30]);
 assert.deepEqual(parseStorageSize(' 002.500 ','GiB'),{amount:'2.5',unit:'GiB',bytes:2684354560});
 assert.equal(parseStorageSize('.2','MiB').bytes,209715);
 assert.equal(storageQueryMinimum(2684354560),2684354561);
 assert.equal(storageQueryMinimum(0),0);
});
test('a bad threshold never becomes a numeric filter',()=>{
 for(const raw of ['', '0', '-1', '1e5', '0.000000001', '999999999999', '1.5.0', 'NaN'])assert.equal(parseStorageSize(raw,'MiB'),null,raw);
 assert.equal(parseStorageSize('2','TB'),null);
});
