import assert from 'node:assert/strict';
import test from 'node:test';
import {defaultHistoryRange,historyRangeKey,historyQueryRange,localMinute,parseLocalMinute,readHistoryRange,saveHistoryRange,validateHistoryRange} from './history-range.js';

test('custom time validation rejects incomplete, invalid calendar dates, reverse, future and oversized ranges',()=>{
 const now=new Date('2026-11-04T12:00:00').getTime();
 assert(validateHistoryRange('2026-11-03T00:00','2026-11-03T23:59',now).fromUtc);
 for(const [from,to] of [['bad','2026-11-03T00:00'],['2026-02-30T00:00','2026-03-01T00:00'],['2026-11-03T10:00','2026-11-03T09:00'],['2026-11-05T00:00','2026-11-06T00:00'],['2024-01-01T00:00','2026-11-03T00:00']])assert(validateHistoryRange(from,to,now).error);
});
test('preset and custom parameters remain distinct and editing drafts does not query until apply',()=>{
 const view=defaultHistoryRange();assert.deepEqual(historyQueryRange(view),{minutes:60});const selected={...view,rangeMode:'custom'};
 assert.deepEqual(historyQueryRange(selected),{fromUtc:view.customFrom,toUtc:view.customTo});assert.equal(historyRangeKey({...selected,customStart:'editing'}),historyRangeKey(selected));assert.notEqual(historyRangeKey({...view,minutes:1440}),historyRangeKey(selected));assert.equal(localMinute(parseLocalMinute(view.customStart)),view.customStart);
});
test('only inert custom range preferences persist independently by device, with bounded capacity',()=>{
 let json='';const storage={getItem:()=>json,setItem:(key,value)=>json=value};const view={...defaultHistoryRange(),rangeMode:'custom',result:{points:['must-not-save']}};saveHistoryRange('A',view,storage);saveHistoryRange('B',{...view,customStart:'2026-01-02T10:00'},storage);
 assert.equal(readHistoryRange('A',storage).customStart,view.customStart);assert.equal(readHistoryRange('B',storage).customStart,'2026-01-02T10:00');assert.equal(json.includes('must-not-save'),false);for(let i=0;i<30;i++)saveHistoryRange(String(i),view,storage);assert.equal(JSON.parse(json).length,20);assert.deepEqual(readHistoryRange('A',storage),{});
});

import {snapHistoryCoordinate} from './history-popover-position.js';
test('snapshot popover aligns to physical pixels at Windows scaling factors',()=>{for(const ratio of [1,1.25,1.5,2])for(const origin of [0,17.5,24.3333]){const aligned=snapHistoryCoordinate(177.392,origin,ratio);assert(Math.abs((origin+aligned)*ratio-Math.round((origin+aligned)*ratio))<1e-8)}});
