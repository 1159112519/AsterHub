import test from 'node:test';import assert from 'node:assert/strict';import {fitColumns} from './table-column-layout.js';
test('long dates keep readable width when a file pane narrows',()=>{const a=fitColumns([44,340,90,100],[44,150,64,172],550);assert.equal(a.reduce((s,v)=>s+v,0),550);assert.ok(a[3]>=172);assert.equal(a[0],44)});
test('undersized panes scroll instead of drawing dates over the next column',()=>{assert.deepEqual(fitColumns([44,340,90,100],[44,150,64,172],300),[44,150,64,172])});
test('user resize proportions survive wider windows and invalid old widths',()=>{const a=fitColumns([44,400,100,200],[44,150,64,172],900);assert.ok(a[1]>a[3]);assert.ok(fitColumns([null,NaN,0,1],[44,150,64,172],900).every(Number.isFinite))});
