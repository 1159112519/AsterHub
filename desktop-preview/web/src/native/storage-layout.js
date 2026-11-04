const GB=1e9;
export function formatBytes(bytes){if(bytes>=1e12)return {value:(bytes/1e12).toFixed(2),unit:'TB'};if(bytes>=GB)return {value:(bytes/GB).toFixed(1),unit:'GB'};if(bytes>=1e6)return {value:(bytes/1e6).toFixed(0),unit:'MB'};return {value:(bytes/1e3).toFixed(0),unit:'KB'}}
export const bytesText=b=>{const s=formatBytes(b);return s.value+' '+s.unit};
// Squarified treemap: weighted area, stable sorting, row aspect-ratio minimization.
export function treemap(items,width,height){
 const data=items.filter(n=>n.bytes>0).sort((a,b)=>b.bytes-a.bytes||a.id.localeCompare(b.id));const total=data.reduce((s,n)=>s+n.bytes,0);if(!total||width<=0||height<=0)return [];
 const pending=data.map(n=>({...n,area:n.bytes/total*width*height}));const result=[];let box={x:0,y:0,w:width,h:height};
 const worst=(row,side)=>{if(!row.length)return Infinity;const a=row.map(n=>n.area),sum=a.reduce((s,x)=>s+x,0);return Math.max(side*side*Math.max(...a)/(sum*sum),(sum*sum)/(side*side*Math.min(...a)))};
 const place=row=>{const sum=row.reduce((s,n)=>s+n.area,0),vertical=box.w>=box.h,side=vertical?box.h:box.w,thickness=sum/side;let cursor=0;for(const n of row){const length=n.area/thickness;result.push({...n,x:box.x+(vertical?0:cursor),y:box.y+(vertical?cursor:0),w:vertical?thickness:length,h:vertical?length:thickness});cursor+=length}if(vertical){box.x+=thickness;box.w=Math.max(0,box.w-thickness)}else{box.y+=thickness;box.h=Math.max(0,box.h-thickness)}};
 let row=[];while(pending.length){const side=Math.min(box.w,box.h);const next=pending[0];if(!row.length||worst([...row,next],side)<=worst(row,side)){row.push(pending.shift())}else{place(row);row=[]}}if(row.length)place(row);return result;
}

export function tileColor(node){
 const colors=['blue','purple','green','amber','teal','slate','rose'];
 const identity=String(node.name||node.path||node.id);let hash=0;
 for(let i=0;i<identity.length;i++)hash=(Math.imul(hash,31)+identity.charCodeAt(i))>>>0;
 return colors[hash%colors.length];
}

const MAX_MAP_TILES=64, MIN_MAP_CELL=18;
const byWeight=(a,b)=>b.bytes-a.bytes||a.id.localeCompare(b.id);
// Keep the largest tiles. The remainder retains its byte weight and is reachable in the file list.
export function storageTiles(result,width,height,palettes={}){
 if(!result||width<=0||height<=0)return [];
 const available=(result.items||[]).filter(n=>n.bytes>0).sort(byWeight),total=result.total??available.length;
 const totalBytes=Math.max(result.bytes||0,available.reduce((sum,n)=>sum+n.bytes,0));
 let items=available.slice(0,total>MAX_MAP_TILES?MAX_MAP_TILES-1:MAX_MAP_TILES),tiles;
 do {
  const otherBytes=Math.max(0,totalBytes-items.reduce((sum,n)=>sum+n.bytes,0)),otherCount=Math.max(0,total-items.length);
  tiles=treemap(otherBytes>0&&otherCount>0?[...items,{id:'other',name:`其他 ${otherCount.toLocaleString('zh-CN')} 项`,bytes:otherBytes,kind:'file',color:'slate',aggregate:true,aggregateCount:otherCount}]:items,width,height);
  const small=new Set(tiles.filter(n=>!n.aggregate&&Math.min(n.w,n.h)<MIN_MAP_CELL).map(n=>n.id));
  if(!small.size)break;
  items=items.filter(n=>!small.has(n.id));
 }while(items.length||tiles.some(n=>!n.aggregate));
 const key=result.snapshotId+':'+result.node.id;
 let palette=palettes[key];
 if(!palette){palette=Object.create(null);palettes[key]=palette;if(Object.keys(palettes).length>32)delete palettes[Object.keys(palettes)[0]];}
 const tones=['blue','purple','green','amber','teal','slate','rose'];let used=new Set();
 return tiles.map(tile=>{
  if(tile.aggregate)return tile;
  if(used.size===tones.length)used=new Set();
  let i=tones.indexOf(palette[tile.id]||tileColor(tile));
  if(!palette[tile.id]){while(used.has(tones[i]))i=(i+1)%tones.length;palette[tile.id]=tones[i];}
  used.add(tones[i]);
  return {...tile,color:tones[i]};
 });
}
export function storageTileRect(tile){
 const gap=Math.max(0,Math.min(3,tile.w*.1,tile.h*.1));
 return {x:tile.x+gap,y:tile.y+gap,width:Math.max(0,tile.w-gap*2),height:Math.max(0,tile.h-gap*2)};
}
export function commonStorageDirectory(from,to){const a=from.crumbs||[],b=to.crumbs||[];let common=null;for(let i=0;i<Math.min(a.length,b.length)&&a[i].id===b[i].id;i++)common=a[i].id;return common;}
export function planStorageNavigation({from,to,parent,tiles,actualSource}){
 if(!from||!to||from.node.id===to.node.id||!parent)return null;
 function surface(result){
  if(result.node.id===parent.node.id)return null;
  const chain=result.crumbs||[],index=chain.findIndex(n=>n.id===parent.node.id),source=chain[index+1];if(index<0||!source)return null;
  const tile=tiles.find(n=>n.id===source.id)||tiles.find(n=>n.aggregate);if(!tile)return null;
  return {result,sourceId:tile.id,source,rect:storageTileRect(tile),color:tile.color||tileColor(tile)};
 }
 const before=surface(from),after=surface(to);
 if(after&&parent.node.id===from.node.id&&actualSource)after.rect=actualSource;
 if(!before&&!after)return null;
 return {from:from.node.id,to:to.node.id,parent,before,after,direction:before&&after?'across':after?'in':'out',duration:.52};
}
