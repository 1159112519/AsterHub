import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import {gapLimit,splitTimedPoints,timedPaths} from './time-series.js';
import {mergeReading} from './native/device-state.js';

const rows=(times,values)=>times.map((t,i)=>values[i]===null?null:[t,values[i]]);
const segments=(times,values,flags,intervals)=>splitTimedPoints(rows(times,values),times,gapLimit(times,3000),flags,7500,intervals);
test('one missing reading joins real endpoints, repeated missing readings leave a hole',()=>{
 const t=[0,3000,5000,6000,12000,15000,22000,34000];
 const data=segments(t,[0,null,5,6,null,null,12,13]);
 assert.deepEqual(data.map(a=>a.map(p=>p[0])),[[0,5000,6000],[22000],[34000]]);
 assert.match(timedPaths(data,20).stroke,/^M0,0 C/);
 assert.ok(!timedPaths(data,20).stroke.includes('3000,'));
 assert.equal(segments([0,1000,2500,5000],[1,null,null,2]).length,2);
});
test('a marked disconnect splits even when the next valid value returns inside one cadence',()=>{
 let state=mergeReading(null,{time:1000,interval:3,cpu:30},1000);
 state=mergeReading(state,{time:2200,interval:3,error:'Connection reset by peer',hardGap:true},2200);
 state=mergeReading(state,{time:3000,interval:3,cpu:40},3000);
 assert.equal(state.samples[1].gapBefore,true);
 assert.equal(state.samples[1].cpu,null);
 assert.deepEqual(segments(state.samples.map(p=>p.time),state.samples.map(p=>p.cpu),state.samples.map(p=>p.gapBefore)).map(s=>s.length),[1,1]);
});
test('the previous clock survives a mixed cadence, while a later outage still splits',()=>{
 const t=[0,3000,6000,6500,7000,7600,12500,13000];
 const p=splitTimedPoints(rows(t,[1,2,3,4,null,5,6,7]),t,1250,null,1250,[3000,3000,3000,500,500,500,500,500]);
 assert.deepEqual(p.map(a=>a.map(([time])=>time)),[[0,3000,6000,6500,7600],[12500,13000]]);
});

const detailSource=readFileSync(new URL('../public/taskbar-detail/detail.js',import.meta.url),'utf8');
test('taskbar detail draws separate fills and a real zero without crossing a disconnect',()=>{
 const body={};const stub={style:{setProperty(){}},querySelector(){return null;},setAttribute(){},addEventListener(){}};
 const window={};const document={getElementById(id){return id==='detail-content'?body:stub;},body:{dataset:{}},documentElement:stub,querySelector(){return stub;},addEventListener(){},fonts:{load(){return new Promise(()=>{});}}};
 vm.runInNewContext(detailSource,{window,document,ResizeObserver:class{observe(){}}});
 const point=(time,value,breakBefore=false)=>({time,value,breakBefore});
 window.taskbarDetail.render({end:12000,title:'GPU',ceiling:100,series:[[point(1000,0),point(4000,20),point(7000,30,true),point(10000,40)]]});
 assert.equal((body.innerHTML.match(/class="line"/g)||[]).length,2);
 assert.equal((body.innerHTML.match(/class="fill"/g)||[]).length,2);
 assert.match(body.innerHTML,/M240\.10,93\.00/);
});
