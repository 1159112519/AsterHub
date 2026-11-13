import test from 'node:test';import assert from 'node:assert/strict';
import {commonStorageDirectory,planStorageNavigation,storageBlockLibrary,storageTiles,storageTileRect} from '../native/storage-layout.js';
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
test('a skewed directory keeps readable small blocks until the final aggregate cell',()=>{
 const items=[{...a,bytes:100000000},...Array.from({length:14},(_,i)=>({...file,id:'small'+i,bytes:100}))];
 const skewed={...home,items,total:items.length,bytes:items.reduce((sum,item)=>sum+item.bytes,0)};
 const tiles=storageTiles(skewed,420,280),tail=tiles.find(t=>t.aggregate);
 assert(tiles.length>2);assert.equal(tail.aggregateCount,items.length-(tiles.length-1));assert.equal(tail.bytes,tail.aggregateCount*100);
 const rect=storageTileRect(tail);
 assert(rect.x>=tail.x&&rect.y>=tail.y&&rect.x+rect.width<=tail.x+tail.w+.00001&&rect.y+rect.height<=tail.y+tail.h+.00001);
});


test('block library fits small independent files before creating an aggregate',()=>{
 const items=Array.from({length:60},(_,i)=>({...file,id:'equal'+i,bytes:100}));
 const tiles=storageTiles({...home,items,total:60,bytes:6000},800,800);
 assert.equal(tiles.length,60);assert.equal(tiles.some(t=>t.aggregate),false);
 assert(tiles.every(t=>Math.min(t.w,t.h)>=90));
});

test('other files occupy only the final minimum unit after every other cell is filled',()=>{
 const items=Array.from({length:200},(_,i)=>({...file,id:'small'+i,bytes:100}));
 const tiles=storageTiles({...home,items,total:500,bytes:50000},800,500),other=tiles.find(t=>t.aggregate);
 assert.equal(tiles.length,40);assert.equal(other.w,100);assert.equal(other.h,100);
 assert.equal(other.aggregateCount,461);assert.equal(other.bytes,46100);
 assert.equal(tiles.reduce((sum,t)=>sum+t.w*t.h,0),800*500);
});

test('predefined grid blocks stay inside the canvas, never overlap, and preserve file size order',()=>{
 const items=Array.from({length:120},(_,i)=>({...file,id:'rank'+i,bytes:100000/(i+1)}));
 for(const [width,height] of [[800,500],[420,280],[1180,650]]){
  const tiles=storageTiles({...home,items,total:120,bytes:items.reduce((sum,t)=>sum+t.bytes,0)},width,height);
  assert(tiles.every(t=>t.blockSize&&t.x>=0&&t.y>=0&&t.x+t.w<=width+.001&&t.y+t.h<=height+.001));
  for(let i=0;i<tiles.length;i++)for(let j=i+1;j<tiles.length;j++){
   const a=tiles[i],b=tiles[j];assert(a.x+a.w<=b.x+.001||b.x+b.w<=a.x+.001||a.y+a.h<=b.y+.001||b.y+b.h<=a.y+.001);
  }
  const files=tiles.filter(t=>!t.aggregate);assert(files.every((t,i)=>!i||t.w*t.h<=files[i-1].w*files[i-1].h+.001));
 }
});


test('three close file sizes fill the canvas without sacrificing size order',()=>{
 const items=[7000,4500,4400].map((bytes,i)=>({...file,id:'three'+i,bytes}));
 const tiles=storageTiles({...home,items,total:3,bytes:15900},800,500);
 assert.equal(tiles.reduce((sum,t)=>sum+t.w*t.h,0),400000);
 assert(tiles.every((t,i)=>!i||t.w*t.h<=tiles[i-1].w*tiles[i-1].h));
 assert.equal(tiles.some(t=>t.aggregate),false);
});

test('seeded mixed file sizes fill bounded library slots with exact totals and stable identities',()=>{
 let seed=69345;const random=()=>((seed=(Math.imul(seed,1664525)+1013904223)>>>0)/4294967296);
 for(let scenario=0;scenario<600;scenario++){
  const count=1+Math.floor(random()*190),width=100+Math.floor(random()*1300),height=100+Math.floor(random()*900),power=1+random()*6;
  const items=Array.from({length:count},(_,i)=>({...file,id:'mixed'+i,bytes:Math.floor(10+Math.pow(random(),power)*1e8)}));
  const data={...home,items,total:count,bytes:items.reduce((sum,t)=>sum+t.bytes,0)},tiles=storageTiles(data,width,height),library=storageBlockLibrary(width,height),independent=tiles.filter(t=>!t.aggregate),other=tiles.find(t=>t.aggregate);
  assert(Math.abs(tiles.reduce((sum,t)=>sum+t.w*t.h,0)-width*height)<.01,'no empty region');
  assert.equal(tiles.reduce((sum,t)=>sum+t.bytes,0),data.bytes,'exact byte count');
  assert.equal(new Set(tiles.map(t=>t.id)).size,tiles.length,'unique file identities');
  assert(tiles.length<=library.columns*library.rows,'bounded DOM count');
  assert(tiles.every(t=>t.x>=0&&t.y>=0&&t.x+t.w<=width+.001&&t.y+t.h<=height+.001),'inside canvas');
  if(other){assert.equal(other.blockSize.columns*other.blockSize.rows,1);assert.equal(other.aggregateCount,count-independent.length);}
  independent.forEach((t,i)=>{assert.equal(t.bytes,items.find(n=>n.id===t.id).bytes);if(i)assert(t.w*t.h<=independent[i-1].w*independent[i-1].h+.001,'largest file gets largest slot');});
  for(let i=0;i<tiles.length;i++)for(let j=i+1;j<tiles.length;j++){
   const a=tiles[i],b=tiles[j];assert(a.x+a.w<=b.x+.001||b.x+b.w<=a.x+.001||a.y+a.h<=b.y+.001||b.y+b.h<=a.y+.001,'no overlap');
  }
 }
});
