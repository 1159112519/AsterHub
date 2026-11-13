import test from 'node:test';
import assert from 'node:assert/strict';
import {createAnalysisQuery} from './analysis-query.mjs';

test('cached analysis reopens without a read, revalidates after TTL, and keeps device/range keys isolated',async()=>{
 let now=10,calls=0;
 const cache=createAnalysisQuery(async params=>({...params,read:++calls}),{ttl:30,limit:2,now:()=>now});
 const first=cache.query('a',{device:'one'});
 assert.equal(cache.query('a',{device:'one'}),first);
 assert.deepEqual(await first,{device:'one',read:1});
 assert.equal((await cache.query('a',{device:'one'})).read,1);
 assert.equal(cache.peek('b'),undefined);
 now+=31;
 assert.equal(cache.peek('a').value.read,1);
 assert.equal((await cache.query('a',{device:'one'})).read,2);
 assert.equal((await cache.query('a',{device:'one'},{force:true})).read,3);
 await cache.query('b',{device:'two'});await cache.query('c',{device:'two',minutes:60});
 assert.equal(cache.peek('a'),undefined);
});
test('a failed refresh keeps the last real result and the next refresh can recover',async()=>{
 let fail=false;
 const cache=createAnalysisQuery(async()=>{if(fail)throw Error('offline');return {points:[1]};});
 await cache.query('a');fail=true;
 await assert.rejects(cache.query('a',null,{force:true}),/offline/);
 assert.deepEqual(cache.peek('a').value,{points:[1]});fail=false;
 assert.deepEqual(await cache.query('a',null,{force:true}),{points:[1]});
});
