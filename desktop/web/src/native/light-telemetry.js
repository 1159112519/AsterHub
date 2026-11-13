// Local CPU/memory has its own acquisition clock. Full snapshots and their history are untouched.
const finite=value=>typeof value==='number'&&Number.isFinite(value);
export function mergeLightReading(old,raw,now=Date.now()){
 if(!raw||raw.deviceId!=='local'||!finite(raw.time)||!finite(raw.sequence))return old;
 const prior=old?.light;
 if(prior&&(raw.sequence<=prior.sequence||raw.time<=prior.lastAttempt))return old;
 const valid=finite(raw.totalBytes)&&raw.totalBytes>0&&finite(raw.availableBytes)&&raw.availableBytes>=0&&raw.availableBytes<=raw.totalBytes;
 const error=raw.error||(!valid?'Memory counters unavailable':'');
 const cpuValid=raw.cpuSupported!==false&&finite(raw.cpu)&&raw.cpu>=0&&raw.cpu<=100;
 const good=error?prior?.good:{...raw,cpu:cpuValid?raw.cpu:raw.cpuSupported===false?null:prior?.good?.cpu??null,cpuTime:cpuValid?raw.time:raw.cpuSupported===false?null:prior?.good?.cpuTime??null};
 const light={good:good||null,sequence:raw.sequence,lastAttempt:raw.time,error,receivedAt:error?prior?.receivedAt??null:now};
 const points=old?.lightSamples||[];
 const point=good&&!error?{time:good.time,interval:good.intervalMs/1000,cpu:cpuValid?raw.cpu:null,memory:(good.totalBytes-good.availableBytes)/good.totalBytes*100}:{time:raw.time,interval:raw.intervalMs/1000,cpu:null,memory:null,gap:true};
 return {...old,light,lightSamples:point?[...points,point].filter(p=>p.time>=now-900000).slice(-3604):points};
}
export function overlayLightReading(sample,light,now=Date.now()){
 if(sample?.deviceId!=='local'||!light?.good)return sample;
 const current=light.good;
 // At one second or longer, publish the complete batch together instead of offset CPU/memory clocks.
 if(current.intervalMs>=1000&&!sample.stale&&!sample.error&&finite(sample.cpu)&&finite(sample.memory))return sample;
 const age=Math.max(0,now-current.time),stale=!!light.error||age>Math.max(3000,current.intervalMs*3);
 const result={...sample,memory:(current.totalBytes-current.availableBytes)/current.totalBytes*100,memUsed:(current.totalBytes-current.availableBytes)/1024**3,memTotal:current.totalBytes/1024**3,memoryTime:current.time,light:{source:current.source,cpuSupported:current.cpuSupported!==false,time:current.time,receivedAt:light.receivedAt,intervalMs:current.intervalMs,durationMs:current.durationMs,ageMs:age,stale,error:light.error}};
 if(finite(current.cpu)){result.cpu=current.cpu;result.cpuTime=current.cpuTime;}
 return result;
}

// Call per chart metric. Never replace the shared full sample array with this result.
export function resourceChartSamples(fullSamples=[],lightSamples=[],metric){
 if(!['cpu','memory'].includes(metric)||!lightSamples.length)return fullSamples;
 const synchronized=lightSamples.at(-1)?.interval>=1&&fullSamples.at(-1)?.interval===lightSamples.at(-1)?.interval&&fullSamples.some(p=>!p.gap&&finite(p[metric]));
 let fast=lightSamples.filter(p=>finite(p.time)).slice().sort((a,b)=>a.time-b.time);
 // A memory-only provider must not erase CPU samples, even if its memory read failed.
 if(!fast.some(p=>finite(p[metric])))return fullSamples;
 const firstMetric=fast.findIndex(p=>finite(p[metric])||p.gap);
 if(firstMetric<0)return fullSamples;
 fast=fast.slice(firstMetric);
 if(!fast.length)return fullSamples;
 // Full collection may take longer than the configured timer (WMI/GPU providers).
 // Infer its established cadence from successful neighbours with the same setting.
 // A single long pause or failed reading must never raise its own gap threshold.
 const fullCadences=new Map();
 const fullOrdered=fullSamples.filter(p=>finite(p.time)).slice().sort((a,b)=>a.time-b.time);
 for(let i=1;i<fullOrdered.length;i++){
  const a=fullOrdered[i-1],b=fullOrdered[i],delta=b.time-a.time;
  if(delta<=0||a.gap||b.gap||b.gapBefore||!finite(a[metric])||!finite(b[metric])||a.interval!==b.interval)continue;
  const key=b.interval,values=fullCadences.get(key)||[];values.push(delta);fullCadences.set(key,values);
 }
 for(const [key,values] of fullCadences){values.sort((a,b)=>a-b);fullCadences.set(key,values.length>=5?values[Math.floor((values.length-1)/2)]:0)}
 const older=fullSamples.filter(p=>p.time<fast[0].time),points=new Map();
 for(const point of older)points.set(point.time,point);
 for(const point of fast)points.set(point.time,point);
 const merged=synchronized?fullOrdered:[...points.values()].sort((a,b)=>a.time-b.time);
 return merged.map((point,index)=>{
  if(!index)return point;const previous=merged[index-1];
  // Each edge uses both acquisition clocks, including older retained full data.
  const cadence=(p,full)=>Math.max(finite(p.interval)&&p.interval>0?p.interval*1000:3000,full?fullCadences.get(p.interval)||0:0);
  const previousFull=previous.time<fast[0].time,pointFull=point.time<fast[0].time;
  return point.time-previous.time>Math.max(cadence(previous,previousFull),cadence(point,pointFull))*3?{...point,gapBefore:true}:point;
 });
}
