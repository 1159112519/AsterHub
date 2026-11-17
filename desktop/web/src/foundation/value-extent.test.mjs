import test from 'node:test';
import assert from 'node:assert/strict';
import {valueExtent} from './value-extent.mjs';
test('locked extremes ignore missing readings and retain zero within selected bounds',()=>{const series=[{values:[999,null,0,12,4,1000]},{values:[null,null,3,18,7]}];const locked=valueExtent(series,1,4);assert.deepEqual(locked,[0,18]);series[0].values[3]=100;assert.deepEqual(locked,[0,18]);assert.deepEqual(valueExtent(series,1,4),[0,100]);assert.equal(valueExtent([{values:[null,undefined,NaN]}]),null)});
test('constant readings produce a finite nonzero axis without negative percentage bounds',()=>{assert.deepEqual(valueExtent([{values:[0,0]}]),[0,.1]);assert.deepEqual(valueExtent([{values:[50]}]),[49.5,50.5])});
