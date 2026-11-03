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

// Keep a bounded, stable palette and identical geometry for every spatial layer.
export function storageTiles(result,width,height,palettes={}){
 if(!result)return [];
 let items=result.items||[];const key=result.snapshotId+':'+result.node.id;
 let palette=palettes[key];if(!palette){palette={};const tones=['blue','purple','green','amber','teal','slate','rose'];let used=new Set();[...items].sort((a,b)=>a.name.localeCompare(b.name)||a.id.localeCompare(b.id)).forEach(n=>{if(used.size===tones.length)used=new Set();let i=tones.indexOf(tileColor(n));while(used.has(tones[i]))i=(i+1)%tones.length;palette[n.id]=tones[i];used.add(tones[i])});palettes[key]=palette;if(Object.keys(palettes).length>32)delete palettes[Object.keys(palettes)[0]];}
 items=items.map(n=>({...n,color:palette[n.id]||tileColor(n)}));
 if(result.total>items.length){const visible=items.slice(0,199);items=[...visible,{id:'other',name:`其他 ${result.total-visible.length} 项`,bytes:Math.max(0,result.bytes-visible.reduce((s,n)=>s+n.bytes,0)),kind:'file',color:'slate',aggregate:true}];}
 return treemap(items,width,height);
}
export function storageTileRect(tile){return {x:tile.x+3,y:tile.y+3,width:Math.max(0,tile.w-6),height:Math.max(0,tile.h-6)}}
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
