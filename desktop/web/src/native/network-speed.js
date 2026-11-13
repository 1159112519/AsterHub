export const nodes=[{id:'cf',name:'Cloudflare'},{id:'la',name:'Clouvider · 洛杉矶',chunk:1048576},{id:'den',name:'Sharktech · 丹佛',chunk:1048576},{id:'ams',name:'Clouvider · 阿姆斯特丹',chunk:1048576}];
export const MIB=1048576,STAGE_SECONDS=10,DEFAULT_TRAFFIC_CAP=0;
export const stageDurations=[5,10,15,20,30];
export const validStageSeconds=value=>stageDurations.includes(value);
export const migrateStageSeconds=value=>validStageSeconds(value)?value:STAGE_SECONDS;
export const trafficCaps=[[20,'20 MiB'],[50,'50 MiB'],[100,'100 MiB'],[500,'500 MiB'],[1024,'1 GiB'],[2048,'2 GiB'],[0,'不限量']];
export const validTrafficCap=cap=>trafficCaps.some(([value])=>value===cap);
export const trafficCapLabel=cap=>trafficCaps.find(([value])=>value===cap)?.[1]??`${cap} MiB`;
export const migrateTrafficCap=(cap,{explicit=false}={})=>cap===1024&&!explicit?DEFAULT_TRAFFIC_CAP:validTrafficCap(cap)?cap:Number.isFinite(cap)&&cap>0?[20,50,100,500,1024,2048].find(v=>v>=cap)??2048:DEFAULT_TRAFFIC_CAP;
const median=a=>{const s=[...a].sort((x,y)=>x-y),i=s.length>>1;return s.length%2?s[i]:(s[i-1]+s[i])/2};
export function nodeQuality(node,times,attempts=3){
 const sorted=[...times].sort((a,b)=>a-b),ping=times.length?median(times):Infinity;
 const jitter=times.length>1?times.slice(1).reduce((n,v,i)=>n+Math.abs(v-times[i]),0)/(times.length-1):null;
 return {...node,failures:attempts-times.length,ping,jitter,spread:times.length?sorted.at(-1)-sorted[0]:Infinity};
}
export const rankNodes=rows=>rows.filter(row=>row.failures<3).sort((a,b)=>a.failures-b.failures||a.spread-b.spread||a.ping-b.ping);
export const rate=(bytes,ms)=>bytes*8/Math.max(ms,.01)/1000;
export function speedUnit(mode,maximum){return mode==='bytes'?(maximum>=8?{unit:'MB/s',factor:1/8}:{unit:'KB/s',factor:125}):{unit:'Mbps',factor:1};}
// The native bridge accepts one transfer at a time, with a maximum 8.5s request.
// Each direction has its own deadline and fallback chain; a successful download
// must never terminate the run before other upload-capable nodes are attempted.
export async function measureSpeed({cap,choice,seconds=STAGE_SECONDS,signal,call,onUpdate,clock=()=>performance.now()}){
 const stageSeconds=migrateStageSeconds(seconds);
 const start=clock(),at=new Date().toISOString(),id=await call('start',{cap});
 const stop=()=>call('cancel',id).catch(()=>{});
 signal.addEventListener('abort',stop,{once:true});
 const query=(action,fields={},timeout=10000)=>call(action,{...id,...fields},timeout,signal);
 const result={id:id.id,at,used:0,cap,stageSeconds,down:null,up:null,ping:null,jitter:null,loss:null,elapsed:0,phaseSeconds:{},samples:{down:[],up:[]},health:[],sources:{down:[],up:[]},failures:[]};
 const publish=phase=>onUpdate(phase,{...result,samples:{down:[...result.samples.down],up:[...result.samples.up]},phaseSeconds:{...result.phaseSeconds},sources:{down:[...result.sources.down],up:[...result.sources.up]},failures:[...result.failures]});
 try{
  signal.throwIfAborted();publish('checking');
  const udp=query('udp',{},6000).catch(()=>null).then(measured=>{result.loss=measured?.available?measured:null;result.udpUnavailable=!result.loss;});
  const request=async fields=>{try{return await query('request',fields)}catch(error){signal.throwIfAborted();return {ok:false,reason:'connection'}}};
  const suitable=nodes.filter(node=>(choice==='auto'||node.id===choice)&&(!node.chunk||cap===0||cap>=2));
  if(!suitable.length)throw new Error('该节点至少需要 2 MiB 流量上限。');
  const health=await Promise.all(suitable.map(async node=>{
   const times=[];let colo;
   for(let i=0;i<4;i++){
    signal.throwIfAborted();const reply=await request({node:node.id,direction:'latency',size:0,timeout:2400});
    if(reply.ok){if(i>0)times.push(reply.headersMs);colo=reply.colo||colo;}
   }
   return {...nodeQuality(node,times),colo};
  }));
  const ranked=rankNodes(health);result.health=ranked.map(x=>({id:x.id,name:x.name,failures:x.failures,ping:x.ping,spread:x.spread}));
  await udp;signal.throwIfAborted();publish('checking');
  if(!ranked.length)throw new Error('测速节点均未应答。可以稍后重试，或在设置中选择其他节点。');
  Object.assign(result,{node:ranked[0].name,colo:ranked[0].colo,ping:ranked[0].ping,jitter:ranked[0].jitter});
  const budget=cap===0?Infinity:cap*MIB-65536,downCeiling=cap===0?Infinity:Math.floor(budget/2);
  for(const direction of ['down','up']){
   const stage=clock(),end=stage+stageSeconds*1000,ceiling=direction==='down'?downCeiling:budget;
   let ms=0,received=0,requests=0;
   publish(direction);
   // Reuse the download node first for a comparable upload. Other GET-healthy
   // nodes remain candidates because POST support cannot be inferred from GET.
   const preferred=result.sources.down[0];
   const candidates=direction==='up'&&preferred?[...ranked].sort((a,b)=>(b.id===preferred)-(a.id===preferred)):ranked;
   for(const node of candidates){
    let next=131072,nodeSamples=0;
    while(clock()<end-80&&ceiling>result.used&&requests++<50){
     signal.throwIfAborted();const size=direction==='down'&&node.chunk?node.chunk:Math.min(next,ceiling-result.used);
     if(size>ceiling-result.used)break;
     result.used+=size;
     const remaining=Math.max(1,Math.ceil(end-clock()));
     const r=await request({node:node.id,direction,size,timeout:Math.min(8500,nodeSamples||node!==candidates[0]?remaining:Math.min(3000,remaining))});
     if(!r.ok){result.failures.push({direction,node:node.id,reason:r.reason||'response',status:r.status??null});break;}
     if(!Number.isFinite(r.ms)||r.ms<=0){result.failures.push({direction,node:node.id,reason:'invalid-timing'});break;}
     if(!received&&(direction==='down'||result.down==null))Object.assign(result,{node:node.name,colo:node.colo,ping:node.ping,jitter:node.jitter});
     nodeSamples++;received+=size;ms+=r.ms;
     if(!result.sources[direction].includes(node.id))result.sources[direction].push(node.id);
     result.samples[direction].push({t:(clock()-stage)/1000,value:rate(size,r.ms),node:node.id});
     result[direction]=rate(received,ms);publish(direction);
     next=Math.max(131072,Math.min(16*MIB,Math.floor(size*850/Math.max(r.ms,1))));
    }
    if(clock()>=end-80||result.used>=ceiling||requests>=50)break;
   }
   result.phaseSeconds[direction]=(clock()-stage)/1000;
   publish(direction);
  }
  if(result.down==null&&result.up==null)throw new Error('节点能应答，但没有收到有效传输样本。');
  result.partial=result.down==null||result.up==null;
  result.elapsed=(clock()-start)/1000;publish('done');return result;
 }finally{signal.removeEventListener('abort',stop);await stop();}
}
