import test from 'node:test';
import assert from 'node:assert/strict';
import {createScrollMemory} from './scroll-memory.js';
import {createWorkspaceState} from './workspace-state.js';
function fixture(wanted=900){
 let height=100,top=0,nextId=0;const listeners=new Map(),frames=new Map(),timers=new Map(),observers=[];
 class Observer{constructor(callback){this.callback=callback;this.active=true;observers.push(this)}observe(){}disconnect(){this.active=false}}
 const view={ResizeObserver:Observer,MutationObserver:Observer,requestAnimationFrame:fn=>{frames.set(++nextId,fn);return nextId},cancelAnimationFrame:id=>frames.delete(id),setTimeout:fn=>{timers.set(++nextId,fn);return nextId},clearTimeout:id=>timers.delete(id)};
 const element={ownerDocument:{defaultView:view},children:[],clientHeight:100,get scrollHeight(){return height},get scrollTop(){return top},set scrollTop(value){top=Math.max(0,Math.min(value,height-100))},addEventListener:(name,fn)=>listeners.set(name,fn),removeEventListener:(name,fn)=>{if(listeners.get(name)===fn)listeners.delete(name)}};
 const store=createWorkspaceState(),handle=store.open('A','scroll:history',wanted),memory=createScrollMemory(element,handle);
 return {element,handle,memory,store,frames,timers,listeners,observers,
  input(name='wheel'){listeners.get(name)?.({})},
  grow(value){height=value;for(const observer of observers)if(observer.active)observer.callback();const pending=[...frames.values()];frames.clear();pending.forEach(fn=>fn())},
  expire(){const pending=[...timers.values()];timers.clear();pending.forEach(fn=>fn())}};
}
test('clamped programmatic scroll never overwrites desired position and delayed content restores it',()=>{
 const f=fixture();assert.equal(f.element.scrollTop,0);f.memory.onScroll();assert.equal(f.handle.getSnapshot(),900);
 // No 600ms polling deadline: content can arrive after many deferred layout notifications.
 for(let n=0;n<50;n++){f.grow(100);f.memory.onScroll()}assert.equal(f.handle.getSnapshot(),900);
 f.grow(1500);assert.equal(f.element.scrollTop,900);f.memory.onScroll();assert.equal(f.handle.getSnapshot(),900);
 assert.equal(f.frames.size,0);assert.equal(f.timers.size,0);assert.ok(f.observers.every(o=>!o.active));f.memory.dispose();
});
test('user input cancels restoration and subsequent scroll writes belong only to the original workspace',()=>{
 const f=fixture();const other=f.store.open('B','scroll:history',500);f.input();f.grow(1500);assert.equal(f.element.scrollTop,0);
 f.element.scrollTop=230;f.memory.onScroll();assert.equal(f.handle.getSnapshot(),230);assert.equal(other.getSnapshot(),500);
 f.memory.dispose();f.element.scrollTop=350;f.memory.onScroll();assert.equal(f.handle.getSnapshot(),230);assert.equal(f.listeners.size,0);
});
test('timeout releases observers while preserving unreachable position until user scrolls',()=>{
 const f=fixture();f.expire();assert.ok(f.observers.every(o=>!o.active));f.memory.onScroll();assert.equal(f.handle.getSnapshot(),900);
 f.input('keydown');f.element.scrollTop=0;f.memory.onScroll();assert.equal(f.handle.getSnapshot(),0);f.memory.dispose();
});
test('cleanup never stores browser-clamped old DOM and cancels every pending resource',()=>{
 const f=fixture();f.grow(400);f.memory.dispose();assert.equal(f.handle.getSnapshot(),900);assert.equal(f.listeners.size,0);assert.equal(f.frames.size,0);assert.equal(f.timers.size,0);assert.ok(f.observers.every(o=>!o.active));
});
