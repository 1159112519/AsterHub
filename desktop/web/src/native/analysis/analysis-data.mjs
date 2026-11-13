const GiB=1024**3,MiB=1024**2;
export const finite=v=>typeof v==='number'&&Number.isFinite(v);
export function measured(v){
 if(finite(v))return v>=0?v:null;
 if(typeof v!=='string'||!/^\s*\d+(?:\.\d+)?\s*(?:MiB)?\s*$/.test(v))return null;
 return parseFloat(v);
}
export const memoryGiB=p=>{
 const m=String(p?.mem??'').match(/^\s*(\d+(?:\.\d+)?)\s*\/\s*\d+(?:\.\d+)?\s*(?:MiB)?\s*$/i);
 return m?Number(m[1])/1024:null;
};
export const cardKey=g=>g.Uuid?`uuid:${g.Uuid}`:`index:${g.Index}:${g.Name??''}`;
export const selectedGpu=(point,id)=>point?.gpus?.find(g=>cardKey(g)===id);
export const gpuValues=(point,id)=>{
 const g=selectedGpu(point,id),used=measured(g?.Used),total=measured(g?.Total);
 return used!=null&&total>0&&used<=total?{used:used/1024,total:total/1024,free:(total-used)/1024}:null;
};
export function gpuAggregate(point,ids){
 if(!ids.length)return null;
 const values=ids.map(id=>gpuValues(point,id));
 if(values.some(v=>!v))return null;
 return values.reduce((out,v)=>({used:out.used+v.used,total:out.total+v.total,free:out.free+v.free}),{used:0,total:0,free:0});
}
export function processes(metric,point,card){
 if(metric!=='gpu')return point?.processes?.flatMap(p=>{
  const value=metric==='cpu'?p.Cpu:finite(p.ResidentBytes)?p.ResidentBytes/GiB:null;
  return p.Identity&&finite(value)?[{id:p.Identity,pid:p.Pid,name:p.Name,path:p.Path||null,value}]:[];
 })??null;
 if(!point?.gpuProcesses||!point.gpus)return null;
 const cards=new Map(point.gpus.filter(g=>card==='all'||cardKey(g)===card).map(g=>[String(g.Index),g]));
 const rows=new Map();
 for(const p of point.gpuProcesses){
  const g=cards.get(String(p.Gpu)),value=measured(p.Memory);
  if(!g||value==null||!p.IdentityToken)continue;
  const id=JSON.stringify([cardKey(g),String(p.Pid),p.IdentityToken]);
  rows.set(id,{id,pid:p.Pid,name:String(p.Name??p.Pid).split(/[/\\]/).at(-1),path:p.Path||null,gpu:g.Index,cardId:cardKey(g),value:value/1024});
 }
 return [...rows.values()];
}
export function shiftRange(start,end,delta,min,max){
 const offset=Math.max(min-start,Math.min(max-end,delta));
 return [start+offset,end+offset];
}
// Match an independently captured process snapshot, not an interpolated read.
// A gap between the chosen point and its snapshot is a hard boundary. A gap
// elsewhere inside a comparison does not invalidate the two real endpoints.
export function processSampleIndex(points,index,metric='cpu',card){
 if(!Number.isInteger(index)||index<0||index>=points.length)return -1;
 const time=points[index].time;
 const valid=i=>processes(metric,points[i],card)?.length>0;
 if(valid(index))return index;
 let best=-1,distance=12000;
 for(const direction of [-1,1]){
  for(let i=index+direction;i>=0&&i<points.length;i+=direction){
   if(points[direction<0?i+1:i].gapBefore)break;
   const delta=Math.abs(points[i].time-time);
   if(delta>12000)break;
   if(valid(i)&&(delta<distance||delta===distance&&(best<0||points[i].time<points[best].time))){best=i;distance=delta;}
  }
 }
 return best;
}
export function memoryProcessEndpoints(points,start,end){
 if(start<0||end<=start||end>=points.length)return null;
 const a=processSampleIndex(points,start,'memory'),b=processSampleIndex(points,end,'memory');
 return a>=0&&b>a?[a,b]:null;
}
export function trafficScale(max){
 const units=['B','KiB','MiB','GiB','TiB'];let divisor=1,i=0;
 while(max/divisor>=1024&&i<units.length-1){divisor*=1024;i++;}
 return {divisor,unit:units[i]};
}
export function formatTraffic(value,rate=false){
 if(!finite(value))return '—';
 const {divisor,unit}=trafficScale(Math.abs(value));
 return `${Number((value/divisor).toFixed(divisor===1?0:2))} ${unit}${rate?'/s':''}`;
}
export function rankChanges(start,end){
 if(!start||!end)return [];
 const byId=new Map(start.map(p=>[p.id,p.value]));
 return end.filter(p=>byId.has(p.id)).map(p=>({...p,before:byId.get(p.id),change:p.value-byId.get(p.id)})).sort((a,b)=>Math.abs(b.change)-Math.abs(a.change));
}
export function nearestIndex(points,time,accept=()=>true){
 let best=-1,distance=Infinity;
 points.forEach((p,i)=>{const gap=Math.abs(p.time-time);if(gap<distance&&accept(p,i)){best=i;distance=gap}});
 return best;
}
export function axisTicks(from,to,width){
 const count=width>=840?7:width>=570?5:width>=340?4:3;
 return Array.from({length:count},(_,i)=>({at:i/(count-1),time:from+(to-from)*i/(count-1)}));
}
export function coreBins(points,from,to,columns=30){
 const bins=Array.from({length:columns},()=>new Map()),ids=new Set();
 points.forEach(p=>{
  const index=Math.max(0,Math.min(columns-1,Math.floor((p.time-from)/Math.max(1,to-from)*columns)));
  p.cores?.forEach((id,i)=>{const value=p.coreValues?.[i];if(!finite(value))return;ids.add(id);const map=bins[index],old=map.get(id);if(!old||value>=old.value)map.set(id,{time:p.time,value});});
 });
 return {bins,ids:[...ids].sort((a,b)=>a-b)};
}
export function lowMemorySpans(points,values){
 const groups=[];let run=null;
 values.forEach((v,i)=>{
  if(!v||v.free/v.total>=.2){run=null;return;}
  if(!run||points[i].gapBefore){run={start:i,end:i,min:v.free};groups.push(run);}
  else{run.end=i;run.min=Math.min(run.min,v.free);}
 });
 return groups;
}
const delta=(a,b)=>Number.isSafeInteger(a)&&Number.isSafeInteger(b)&&a>=b?a-b:null;
const sumKnown=(a,b)=>a==null||b==null?null:a+b;
export function networkChanges(points,start,end){
 if(start<0||end<=start||end>=points.length)return {complete:false,rows:[]};
 const result=new Map(),seconds=(points[end]?.time-points[start]?.time)/1000;
 let complete=seconds>0;
 for(let i=start+1;i<=end;i++){
  const before=points[i-1],after=points[i];if(!after||!before||after.gapBefore||!before.adapters||!after.adapters){complete=false;continue;}
  const prior=new Map(before.adapters.map(a=>[a.Id,a]));
  if(before.adapters.some(a=>!after.adapters.some(b=>b.Id===a.Id)))complete=false;
  for(const a of after.adapters){
   const b=prior.get(a.Id);if(!b){complete=false;continue;}
   const down=delta(a.ReceivedBytes,b.ReceivedBytes),up=delta(a.SentBytes,b.SentBytes);
   if(down==null||up==null){complete=false;continue;}
   const errors=sumKnown(delta(a.ReceiveErrors,b.ReceiveErrors),delta(a.SendErrors,b.SendErrors));
   const discards=sumKnown(delta(a.ReceiveDiscards,b.ReceiveDiscards),delta(a.SendDiscards,b.SendDiscards));
   const x=result.get(a.Id)??{id:a.Id,name:a.Name||a.Id,down:0,up:0,errors:0,discards:0};
   result.set(a.Id,{...x,down:x.down+down,up:x.up+up,errors:sumKnown(x.errors,errors),discards:sumKnown(x.discards,discards)});
  }
 }
 return {complete,rows:[...result.values()].sort((a,b)=>b.down+b.up-a.down-a.up)};
}
export const bytesToMiB=v=>v/MiB;
