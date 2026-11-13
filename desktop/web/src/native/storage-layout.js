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

// A bounded library of whole-cell blocks replaces proportional slivers.
// Each canvas has 1–8 columns/rows, a readable minimum block and a full-canvas maximum.
export function storageBlockLibrary(width,height){
 const columns=Math.max(1,Math.min(8,Math.floor(width/96))),rows=Math.max(1,Math.min(8,Math.floor(height/96)));
 const cellWidth=width/columns,cellHeight=height/rows,blocks=[];
 for(let c=1;c<=columns;c++)for(let r=1;r<=rows;r++)blocks.push({columns:c,rows:r,cells:c*r,aspect:Math.abs(Math.log(c*cellWidth/(r*cellHeight)))});
 blocks.sort((a,b)=>b.cells-a.cells||a.aspect-b.aspect||b.columns-a.columns);
 return {columns,rows,cellWidth,cellHeight,blocks};
}
const byWeight=(a,b)=>b.bytes-a.bytes||a.id.localeCompare(b.id);
// Keep files independent while another minimum block fits. Only the final cell
// combines the remainder; its reported bytes/count always stay exact.
export function storageTiles(result,width,height,palettes={}){
 if(!result||width<=0||height<=0)return [];
 const available=(result.items||[]).filter(n=>n.bytes>0).sort(byWeight),total=Math.max(available.length,result.total??available.length);
 const totalBytes=Math.max(result.bytes||0,available.reduce((sum,n)=>sum+n.bytes,0));
 if(!totalBytes||!total)return [];
 const library=storageBlockLibrary(width,height),tiles=[],free=[{x:0,y:0,w:library.columns,h:library.rows}];
 let freeCells=library.columns*library.rows,index=0,shownBytes=0,previousCells=Infinity;
 function place(node,desired){
  let choice=null;
  for(const block of library.blocks){
   if(block.cells>desired)continue;
   const at=free.findIndex(box=>box.w>=block.columns&&box.h>=block.rows);
   if(at>=0){choice={block,at};break;}
  }
  if(!choice)return false;
  const {block,at}=choice,box=free.splice(at,1)[0],c=block.columns,r=block.rows,dw=box.w-c,dh=box.h-r;
  if(dw>=dh){if(dw)free.push({x:box.x+c,y:box.y,w:dw,h:box.h});if(dh)free.push({x:box.x,y:box.y+r,w:c,h:dh})}
  else {if(dw)free.push({x:box.x+c,y:box.y,w:dw,h:r});if(dh)free.push({x:box.x,y:box.y+r,w:box.w,h:dh})}
  free.sort((a,b)=>a.y-b.y||a.x-b.x);
  tiles.push({...node,x:box.x*library.cellWidth,y:box.y*library.cellHeight,w:c*library.cellWidth,h:r*library.cellHeight,blockSize:{columns:c,rows:r}});
  freeCells-=block.cells;return true;
 }
 while(freeCells>0&&index<available.length){
  const remaining=total-index;
  if(freeCells===1&&remaining>1)break;
  const node=available[index],bytesLeft=Math.max(node.bytes,totalBytes-shownBytes),budget=freeCells-(remaining>1?1:0);
  const desired=Math.max(1,Math.min(budget,previousCells,Math.round(freeCells*node.bytes/bytesLeft)));
  if(!place(node,desired))break;
  previousCells=tiles.at(-1).blockSize.columns*tiles.at(-1).blockSize.rows;
  shownBytes+=node.bytes;index++;
 }
 if(total>index&&freeCells>0)place({id:'other',name:'其他文件',bytes:Math.max(0,totalBytes-shownBytes),kind:'file',color:'slate',aggregate:true,aggregateCount:total-index},1);
 // Absorb any packing void into an adjacent larger file using another library
 // size. Text stays readable and the aggregate remains exactly one minimum cell.
 const occupied=Array.from({length:library.rows},()=>Array(library.columns).fill(false));
 const geometry=tiles.map(t=>({x:Math.round(t.x/library.cellWidth),y:Math.round(t.y/library.cellHeight),w:t.blockSize.columns,h:t.blockSize.rows}));
 for(const g of geometry)for(let y=g.y;y<g.y+g.h;y++)for(let x=g.x;x<g.x+g.w;x++)occupied[y][x]=true;
 for(let pass=0;pass<library.columns*library.rows;pass++){
  let changed=false;
  for(let i=0;i<tiles.length;i++){
   if(tiles[i].aggregate)continue;
   const g=geometry[i];
   const options=[{x:g.x,y:g.y,w:g.w+1,h:g.h},{x:g.x-1,y:g.y,w:g.w+1,h:g.h},{x:g.x,y:g.y,w:g.w,h:g.h+1},{x:g.x,y:g.y-1,w:g.w,h:g.h+1}].filter(n=>n.x>=0&&n.y>=0&&n.x+n.w<=library.columns&&n.y+n.h<=library.rows).sort((a,b)=>Math.abs(Math.log(a.w*library.cellWidth/(a.h*library.cellHeight)))-Math.abs(Math.log(b.w*library.cellWidth/(b.h*library.cellHeight))));
   for(const next of options){
    let clear=true;const added=[];
    for(let y=next.y;y<next.y+next.h;y++)for(let x=next.x;x<next.x+next.w;x++)if(x<g.x||x>=g.x+g.w||y<g.y||y>=g.y+g.h){if(occupied[y][x])clear=false;added.push([x,y])}
    if(!clear)continue;
    for(const [x,y] of added)occupied[y][x]=true;
    Object.assign(g,next);Object.assign(tiles[i],{x:g.x*library.cellWidth,y:g.y*library.cellHeight,w:g.w*library.cellWidth,h:g.h*library.cellHeight,blockSize:{columns:g.w,rows:g.h}});changed=true;break;
   }
  }
  if(!changed)break;
 }
 // Pack geometry first, then match the largest files to the largest slots.
 // Filling an empty edge may grow a later slot beyond its previous neighbour;
 // reassigning identities keeps both a full canvas and the file-size ordering.
 const slots=tiles.filter(tile=>!tile.aggregate).map(({x,y,w,h,blockSize})=>({x,y,w,h,blockSize})).sort((a,b)=>b.w*b.h-a.w*a.h||a.y-b.y||a.x-b.x);
 const packed=slots.map((slot,i)=>({...available[i],...slot}));
 const aggregate=tiles.find(tile=>tile.aggregate);if(aggregate)packed.push(aggregate);
 const key=result.snapshotId+':'+result.node.id;
 let palette=palettes[key];
 if(!palette){palette=Object.create(null);palettes[key]=palette;if(Object.keys(palettes).length>32)delete palettes[Object.keys(palettes)[0]];}
 const tones=['blue','purple','green','amber','teal','slate','rose'];let used=new Set();
 return packed.map(tile=>{
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
