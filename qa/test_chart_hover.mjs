/** Deterministic chart interaction regression; run: node qa/test_chart_hover.mjs.
 * Executes the real Chart implementation. DOM/canvas/RAF are inert test doubles;
 * this verifies event/data flow, not browser layout or native GTK rendering.
 */
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
import {fileURLToPath} from 'node:url';
const sourcePath = fileURLToPath(new URL('../linux/web/charts.js', import.meta.url));
let clock=0, nextId=1;
const frames=new Map();
function context(){return new Proxy({labels:[],fillText(text){this.labels.push(String(text));},measureText(text){return {width:String(text).length*7};}}, {get(target,key){return key in target?target[key]:()=>{};}});}
function element(tag){
 const events=new Map(),ctx=context();
 return {tagName:tag.toUpperCase(),style:{},hidden:true,width:0,height:0,offsetWidth:180,offsetHeight:40,innerHTML:'',
  bounds:{left:10,top:10,width:500,height:220},events,ctx,
  getBoundingClientRect(){return this.bounds;},getContext(){return ctx;},
  addEventListener(name,fn){events.set(name,fn);},remove(){this.removed=true;},
  emit(name,event={}){events.get(name)?.(event);}};
}
const sandbox={console,document:{createElement:element,body:{append(){}}},
 ResizeObserver:class{observe(){} disconnect(){}},
 performance:{now:()=>clock},requestAnimationFrame:fn=>{const id=nextId++;frames.set(id,fn);return id;},cancelAnimationFrame:id=>frames.delete(id),
 getComputedStyle:()=>({getPropertyValue:()=> '#aabbcc'}),devicePixelRatio:1,innerWidth:1200,innerHeight:900,
 matchMedia:()=>({matches:false}),timestamp:value=>'time:'+value,esc:value=>String(value)};
vm.createContext(sandbox);
const loaded=fs.readFileSync(sourcePath,'utf8').replace(/^import[^\n]*\n/m,'').replace(/\bexport\s+(?=(?:class|function|const|let)\b)/g,'');
vm.runInContext(loaded+'\nthis.ChartUnderTest=Chart;this.clearAllUnderTest=typeof clearChartHovers==="function"?clearChartHovers:null;',sandbox,{filename:sourcePath});
const Chart=sandbox.ChartUnderTest;
function flush(){clock+=500;const pending=[...frames.values()];frames.clear();for(const callback of pending)callback(clock);}
function setup(){const canvas=element('canvas'),chart=new Chart(canvas,{windowSeconds:300});chart.set([{timestamp:1120,cpu:11},{timestamp:1240,cpu:42},{timestamp:1300,cpu:60}]);flush();return {canvas,chart};}
function move(canvas,chart,fraction=1){canvas.emit('pointermove',{clientX:canvas.bounds.left+chart.pad.x+fraction*chart.width,clientY:100});}
const checks=[];
function test(name,run){run();checks.push(name);console.log('PASS '+name);}

test('move -> leave -> pending frame cannot reopen tooltip',()=>{
 const {canvas,chart}=setup();move(canvas,chart);const alreadyQueued=frames.get(chart.pointerFrame);
 canvas.emit('pointerleave');assert.equal(chart.tip.hidden,true);flush();assert.equal(chart.tip.hidden,true);
 // Also exercise a callback already handed to the browser before cancellation.
 alreadyQueued?.(clock);assert.equal(chart.tip.hidden,true);assert.equal(chart.pointerInside,false);chart.destroy();
});
test('stationary hover survives live set and displays new sample details',()=>{
 const {canvas,chart}=setup();move(canvas,chart);flush();assert.equal(chart.tip.hidden,false);
 chart.set([{timestamp:1120,cpu:11},{timestamp:1240,cpu:42},{timestamp:1301,cpu:87}]);flush();
 assert.equal(chart.tip.hidden,false);assert.match(chart.tip.innerHTML,/time:1301/);assert.match(chart.tip.innerHTML,/87\.0%/);chart.destroy();
});
test('clearHover cancels queued pointer work and resets hover presence',()=>{
 const {canvas,chart}=setup();assert.equal(typeof chart.clearHover,'function');move(canvas,chart);const id=chart.pointerFrame;
 assert.ok(frames.has(id));chart.clearHover();assert.equal(frames.has(id),false);assert.equal(chart.pointerFrame,0);
 assert.equal(chart.pointerInside,false);assert.equal(chart.hover,-1);assert.equal(chart.tip.hidden,true);flush();assert.equal(chart.tip.hidden,true);chart.destroy();
});
test('navigation registry clears all charts including queued pointer work',()=>{
 assert.equal(typeof sandbox.clearAllUnderTest,'function');const a=setup(),b=setup();move(a.canvas,a.chart);move(b.canvas,b.chart);
 sandbox.clearAllUnderTest();flush();for(const {chart} of [a,b]){assert.equal(chart.tip.hidden,true);assert.equal(chart.pointerInside,false);assert.equal(chart.pointerFrame,0);chart.destroy();}
});
test('300-second axis and hover share the same time window with sparse samples',()=>{
 const {canvas,chart}=setup();assert.equal(chart.from,1000);assert.equal(chart.to,1300);
 const axisStart=new Date(1000*1000).toLocaleTimeString('zh-CN',{hour12:false});assert.ok(canvas.ctx.labels.includes(axisStart));
 move(canvas,chart,.4);flush();assert.equal(chart.hover,0);assert.match(chart.tip.innerHTML,/time:1120/);
 move(canvas,chart,.8);flush();assert.equal(chart.hover,1);assert.match(chart.tip.innerHTML,/time:1240/);chart.destroy();
});
test('hidden canvas cannot display or resurrect a stationary tooltip',()=>{
 const {canvas,chart}=setup();move(canvas,chart);flush();assert.equal(chart.tip.hidden,false);
 canvas.bounds={...canvas.bounds,width:0,height:0};chart.set([{timestamp:1301,cpu:80}]);flush();assert.equal(chart.tip.hidden,true);chart.destroy();
});
assert.equal(frames.size,0,'destroy must cancel outstanding animation callbacks');
console.log('CHECKS='+checks.length+' FAILURES=0');
