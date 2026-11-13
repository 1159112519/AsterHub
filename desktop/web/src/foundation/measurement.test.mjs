import test from 'node:test';import assert from 'node:assert/strict';
import {measurementState} from './measurement.js';import {mergeReading} from '../native/device-state.js';
test('zero, missing, stale, and unsupported values remain distinct',()=>{assert.equal(measurementState(0),'ready');assert.equal(measurementState(null),'missing');assert.equal(measurementState(12,{stale:true}),'stale');assert.equal(measurementState(12,{supported:false}),'unsupported');assert.equal(measurementState(NaN),'missing')});
test('a failed read preserves confirmed hardware and GPU inventory without inventing zero metrics',()=>{
 const raw={deviceId:'A',time:100,cpu:12,mem:'4 / 8 MiB',gpus:[{Index:'0',Name:'GPU',Util:'7',Used:'1',Total:'2'}],hardware:{CpuModel:'Test CPU'}};
 const old=mergeReading(null,raw,100),lost=mergeReading(old,{deviceId:'A',time:110,error:'closed'},110);
 assert.equal(lost.sample.gpus.length,1);assert.equal(lost.sample.hardware.CpuModel,'Test CPU');assert.equal(lost.stale,true);assert.equal(lost.samples.at(-1).cpu,null);assert.equal(lost.samples.at(-1).gap,true);
 assert.equal(mergeReading(lost,{...raw,time:105},110),lost);
});
