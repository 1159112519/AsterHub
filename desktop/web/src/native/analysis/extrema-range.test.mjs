import test from 'node:test';
import assert from 'node:assert/strict';
import {extremaRange} from './extrema-range.mjs';
test('extrema endpoints follow time order, not min/max order',()=>{
 assert.deepEqual(extremaRange([5,9,3,7],0,3),[1,2]);
 assert.deepEqual(extremaRange([5,3,9,7],0,3),[1,2]);
});
test('custom bounds exclude peaks outside the interval and preserve real zero',()=>{
 assert.deepEqual(extremaRange([100,4,null,0,7,99],1,4),[3,4]);
});
test('flat regions use their full valid span; missing or single samples cannot lock',()=>{
 assert.deepEqual(extremaRange([null,6,6,null,6],0,4),[1,4]);
 assert.equal(extremaRange([null,undefined,NaN],0,2),null);
 assert.equal(extremaRange([null,6,null],0,2),null);
});
