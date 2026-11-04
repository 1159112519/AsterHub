import test from 'node:test';import assert from 'node:assert/strict';
import {commonStorageDirectory,planStorageNavigation,storageTiles,storageTileRect} from '../native/storage-layout.js';
const dir=(id,parent,name,bytes=100)=>({id,parent,name,path:'D:/'+name,directory:true,bytes}),root=dir('0',null,'Data',400),a=dir('a','0','Models',200),b=dir('b','0','Datasets',150),deep=dir('a1','a','Vision',100),leaf=dir('a2','a','Language',100),file={id:'f',parent:'0',name:'model.bin',directory:false,bytes:50};
const result=(node,crumbs,items)=>({snapshotId:'scan1',node,crumbs,items,total:items.length,bytes:node.bytes});
const home=result(root,[root],[a,b,file]),models=result(a,[root,a],[deep,leaf]),datasets=result(b,[root,b],[]),vision=result(deep,[root,a,deep],[]),language=result(leaf,[root,a,leaf],[]);
test('common storage ancestor uses identities and handles deep branch changes',()=>{assert.equal(commonStorageDirectory(vision,language),'a');assert.equal(commonStorageDirectory(vision,datasets),'0');assert.equal(commonStorageDirectory(home,vision),'0')});
test('direct entry preserves actual clicked bounds and one 520ms action',()=>{const rect={x:37,y:19,width:219,height:353},tiles=storageTiles(home,800,500),plan=planStorageNavigation({from:home,to:models,parent:home,tiles,actualSource:rect});assert.equal(plan.direction,'in');assert.equal(plan.before,null);assert.deepEqual(plan.after.rect,rect);assert.equal(plan.duration,.52);assert.equal(plan.after.sourceId,'a')});
test('return shrinks the outgoing contents into the ancestor branch surface',()=>{const tiles=storageTiles(home,800,500),plan=planStorageNavigation({from:vision,to:home,parent:home,tiles});assert.equal(plan.direction,'out');assert.equal(plan.before.sourceId,'a');assert.equal(plan.before.result.node.id,'a1');assert.equal(plan.after,null);assert.deepEqual(plan.before.rect,storageTileRect(tiles.find(n=>n.id==='a')))});
test('cross-branch handoff has two sources in one stationary common parent',()=>{const plan=planStorageNavigation({from:vision,to:datasets,parent:home,tiles:storageTiles(home,800,500)});assert.equal(plan.direction,'across');assert.equal(plan.before.sourceId,'a');assert.equal(plan.after.sourceId,'b');assert.equal(plan.parent.node.id,'0');assert.equal(plan.duration,.52)});
test('deep sibling navigation does not invent a root-level position',()=>{const plan=planStorageNavigation({from:vision,to:language,parent:models,tiles:storageTiles(models,800,500)});assert.equal(plan.parent.node.id,'a');assert.equal(plan.before.sourceId,'a1');assert.equal(plan.after.sourceId,'a2')});
test('undisplayed branch uses its actual aggregate surface rather than a made-up center',()=>{const tail={...b,id:'tail'},many={...home,items:Array.from({length:200},(_,i)=>({...file,id:String(i),name:'f'+i,bytes:1})),total:300,bytes:300},target=result(tail,[root,tail],[]),tiles=storageTiles(many,800,500);assert(tiles.length<=64);const plan=planStorageNavigation({from:many,to:target,parent:many,tiles});assert.equal(plan.after.sourceId,'other');assert.deepEqual(plan.after.rect,storageTileRect(tiles.find(n=>n.aggregate)))});
test('layout does not mutate result, preserves identities, bounds palette and ignores no-op navigation',()=>{const palette={},before=JSON.stringify(home);storageTiles(home,800,500,palette);assert.equal(JSON.stringify(home),before);for(let i=0;i<60;i++)storageTiles({...home,snapshotId:'s'+i},800,500,palette);assert(Object.keys(palette).length<=32);assert.equal(planStorageNavigation({from:home,to:home,parent:home,tiles:[]}),null)});

test('large directories preserve total byte weight without drawing every item',()=>{
 const items=Array.from({length:200},(_,i)=>({...file,id:'item'+i,name:'item'+i,bytes:300-i}));
 const loaded=items.reduce((sum,item)=>sum+item.bytes,0),many={...home,items,total:9000,bytes:loaded+420};
 const tiles=storageTiles(many,800,500),other=tiles.find(tile=>tile.aggregate);
 assert(tiles.length<=64);
 assert.equal(other.aggregateCount,9000-(tiles.length-1));
 assert.equal(tiles.reduce((sum,tile)=>sum+tile.bytes,0),many.bytes);
 assert(tiles.every(t=>t.w>0&&t.h>0&&t.x>=0&&t.y>=0&&t.x+t.w<=800.001&&t.y+t.h<=500.001));
});
test('a highly skewed directory combines slivers without padding an aggregate outside its cell',()=>{
 const items=[{...a,bytes:100000000},...Array.from({length:14},(_,i)=>({...file,id:'small'+i,bytes:100}))];
 const skewed={...home,items,total:items.length,bytes:items.reduce((sum,item)=>sum+item.bytes,0)};
 const tiles=storageTiles(skewed,420,280),tail=tiles.find(t=>t.aggregate);
 assert.equal(tiles.length,2);assert.equal(tail.aggregateCount,14);assert.equal(tail.bytes,1400);
 const rect=storageTileRect(tail);
 assert(rect.x>=tail.x&&rect.y>=tail.y&&rect.x+rect.width<=tail.x+tail.w+.00001&&rect.y+rect.height<=tail.y+tail.h+.00001);
});
