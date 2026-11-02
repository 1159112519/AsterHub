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
