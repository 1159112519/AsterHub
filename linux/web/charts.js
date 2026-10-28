import {timestamp,esc} from './ui.js';
const chartRegistry=new Set();
export function clearChartHovers(){for(const chart of chartRegistry)chart.clearHover();}
export class Chart {
 constructor(canvas,{lines=[{key:'cpu',color:'--accent',label:'CPU'}],tooltip=true,onSelect=null,max=100,unit='%',format=null,dynamic=false,windowSeconds=0}={}) {
  Object.assign(this,{canvas,lines,max,unit,onSelect,dynamic,windowSeconds});this.format=format||((value)=>Number(value).toFixed(1)+unit);
  chartRegistry.add(this);this.pointerInside=false;this.points=[];this.display=[];this.hover=-1;this.selected=-1;this.frame=0;this.pointerFrame=0;this.base=document.createElement('canvas');this.cached=false;
  if(tooltip){this.tip=document.createElement('div');this.tip.className='chart-tooltip';this.tip.hidden=true;document.body.append(this.tip);this.tip.style.position='fixed';
   canvas.addEventListener('pointermove',e=>{this.pointerInside=true;this.pointerX=e.clientX;this.pointerY=e.clientY;if(!this.pointerFrame)this.pointerFrame=requestAnimationFrame(()=>{this.pointerFrame=0;this.pointer();});});
   canvas.addEventListener('pointerleave',()=>this.clearHover());
   canvas.addEventListener('click',()=>{if(this.hover>=0&&this.onSelect){this.selected=this.hover;this.onSelect(this.points[this.hover]);this.paint(true);}});
  }
  this.resize=new ResizeObserver(()=>this.paint());this.resize.observe(canvas);
 }
 set(points){
  if(!points)return;const signature=points.length+':'+points.at(-1)?.timestamp+':'+this.lines.map(l=>points.at(-1)?.[l.key]).join(',');if(signature===this.signature)return;
  this.signature=signature;const old=this.display;this.points=points;this.hover=-1;if(this.tip&&!this.pointerInside)this.tip.hidden=true;cancelAnimationFrame(this.frame);
  if(this.dynamic){let peak=1024;for(const p of points)for(const l of this.lines)if(p[l.key]!=null)peak=Math.max(peak,p[l.key]*1.15);this.max=2**Math.ceil(Math.log2(peak));}
  const start=performance.now();const tick=now=>{const t=matchMedia('(prefers-reduced-motion: reduce)').matches?1:Math.min(1,(now-start)/450),ease=1-(1-t)**3;
   this.display=points.map((p,i)=>Object.fromEntries(this.lines.map(l=>{const value=p[l.key],before=old[i]?.[l.key];return[l.key,value==null?null:(before??value)+(value-(before??value))*ease];})));
   this.paint();if(this.pointerInside)this.pointer();if(t<1)this.frame=requestAnimationFrame(tick);
  };this.frame=requestAnimationFrame(tick);
 }
 paint(hoverOnly=false){
  const c=this.canvas,rect=c.getBoundingClientRect();if(rect.width<1||rect.height<1)return;this.rect=rect;
  const scale=Math.min(2,devicePixelRatio||1),w=Math.round(rect.width*scale),h=Math.round(rect.height*scale);
  if(c.width!==w||c.height!==h){c.width=w;c.height=h;this.cached=false;}
  const ctx=c.getContext('2d'),style=getComputedStyle(document.body),detailed=rect.height>70;
  const pad={x:detailed?(this.dynamic?88:42):0,y:detailed?18:3,bottom:detailed?28:2};this.pad=pad;this.width=Math.max(1,rect.width-pad.x-4);this.height=Math.max(1,rect.height-pad.y-pad.bottom);
  const count=this.points.length,last=this.points.at(-1)?.timestamp,first=this.windowSeconds?last-this.windowSeconds:this.points[0]?.timestamp;this.from=first;this.to=last;
  const xs=i=>pad.x+(last>first?(this.points[i].timestamp-first)/(last-first):1)*this.width;
  const ys=v=>pad.y+(1-Math.max(0,Math.min(this.max,v))/this.max)*this.height;
  ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,w,h);
  if(hoverOnly&&this.cached)ctx.drawImage(this.base,0,0);
  else {
   ctx.setTransform(scale,0,0,scale,0,0);
   if(detailed){ctx.font='12px "Segoe UI",sans-serif';ctx.fillStyle=style.getPropertyValue('--muted');ctx.strokeStyle=style.getPropertyValue('--line');ctx.lineWidth=.7;
    for(let i=0;i<=4;i++){const v=this.max*i/4,y=ys(v);ctx.fillText(this.dynamic?this.format(v):String(Math.round(v)),0,y+4);ctx.beginPath();ctx.moveTo(pad.x,y);ctx.lineTo(rect.width,y);ctx.stroke();}
    if(count){const a=new Date(first*1000).toLocaleTimeString('zh-CN',{hour12:false}),b=new Date(last*1000).toLocaleTimeString('zh-CN',{hour12:false});ctx.fillText(a,pad.x,rect.height-4);ctx.fillText(b,rect.width-ctx.measureText(b).width-2,rect.height-4);}
   }
   for(const line of this.lines){const color=style.getPropertyValue(line.color).trim();ctx.beginPath();let connected=false;
    this.points.forEach((p,i)=>{const v=this.display[i]?.[line.key]??p[line.key];if(v==null||!Number.isFinite(v)){connected=false;return;}const x=xs(i),y=ys(v);connected?ctx.lineTo(x,y):ctx.moveTo(x,y);connected=true;});
    ctx.strokeStyle=color;ctx.lineWidth=detailed?1.8:1.5;ctx.lineJoin='round';ctx.stroke();
   }
   if(this.base.width!==w||this.base.height!==h){this.base.width=w;this.base.height=h;}
   const b=this.base.getContext('2d');b.clearRect(0,0,w,h);b.drawImage(c,0,0);this.cached=true;
  }
  ctx.setTransform(scale,0,0,scale,0,0);const index=this.hover>=0?this.hover:this.selected;
  if(index>=0&&index<count){const x=xs(index);ctx.setLineDash([3,4]);ctx.strokeStyle=style.getPropertyValue('--muted');ctx.lineWidth=.7;ctx.beginPath();ctx.moveTo(x,pad.y);ctx.lineTo(x,pad.y+this.height);ctx.stroke();ctx.setLineDash([]);
   this.lines.forEach(l=>{const value=this.points[index][l.key];if(value==null)return;ctx.fillStyle=style.getPropertyValue(l.color);ctx.beginPath();ctx.arc(x,ys(value),3,0,Math.PI*2);ctx.fill();});
  }
 }
 clearHover(){this.pointerInside=false;cancelAnimationFrame(this.pointerFrame);this.pointerFrame=0;this.hover=-1;if(this.tip)this.tip.hidden=true;this.paint(true);}
 pointer(){
  if(!this.pointerInside||!this.points.length||!this.rect)return;const rect=this.canvas.getBoundingClientRect(),x=this.pointerX-rect.left;if(rect.width<1||rect.height<1){this.tip.hidden=true;return;}
  if(x<this.pad.x||x>this.pad.x+this.width){this.tip.hidden=true;this.hover=-1;this.paint(true);return;}
  const target=this.from+(this.to-this.from)*(x-this.pad.x)/this.width;
  let lo=0,hi=this.points.length-1;while(lo<hi){const mid=(lo+hi)>>1;if(this.points[mid].timestamp<target)lo=mid+1;else hi=mid;}if(lo>0&&Math.abs(this.points[lo-1].timestamp-target)<Math.abs(this.points[lo].timestamp-target))lo--;
  const changed=this.hover!==lo;this.hover=lo;const point=this.points[lo];
  if(changed||this.tip.hidden)this.tip.innerHTML=esc(timestamp(point.timestamp))+'<br>'+this.lines.map(l=>esc(l.label)+': '+(point[l.key]==null?'未提供':esc(this.format(point[l.key])))).join('　');
  this.tip.hidden=false;this.tip.style.left=Math.max(4,Math.min(innerWidth-this.tip.offsetWidth-8,this.pointerX+12))+'px';this.tip.style.top=Math.max(6,Math.min(innerHeight-this.tip.offsetHeight-8,this.pointerY-this.tip.offsetHeight-10))+'px';if(changed)this.paint(true);
 }
 destroy(){chartRegistry.delete(this);cancelAnimationFrame(this.frame);cancelAnimationFrame(this.pointerFrame);this.resize.disconnect();this.tip?.remove();}
}