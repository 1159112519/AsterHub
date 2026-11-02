// Timeseries geometry is based on sample identity (timestamp), never array index.
export function orderedSamples(rows){const byTime=new Map();for(const row of rows)if(Number.isFinite(row?.time))byTime.set(row.time,row);return [...byTime.values()].sort((a,b)=>a.time-b.time)}
export function gapLimit(timestamps,expectedMs){
 const deltas=timestamps.slice(1).map((t,i)=>t-timestamps[i]).filter(d=>d>0).sort((a,b)=>a-b);
 const median=deltas[Math.floor((deltas.length-1)/2)]||5000;
 return Math.max(3000,Number.isFinite(expectedMs)&&expectedMs>0?expectedMs*3:0,median*3);
}
export function splitTimedPoints(points,timestamps,maxGap,breakBefore=null){
 const segments=[];let current=[];
 for(let i=0;i<points.length;i++){
  const p=points[i],valid=p&&p.every(Number.isFinite)&&Number.isFinite(timestamps[i]);
  if(!valid){if(current.length)segments.push(current);current=[];continue}
  if(current.length&&(breakBefore?.[i]||timestamps[i]<=timestamps[i-1]||timestamps[i]-timestamps[i-1]>maxGap)){segments.push(current);current=[]}
  current.push(p);
 }
 if(current.length)segments.push(current);return segments;
}
export function timedPaths(segments,baseline){
 let stroke='',area='';
 for(const points of segments){
  if(!points.length)continue;
  const coord=n=>Math.round(n*100)/100;let d=`M${coord(points[0][0])},${coord(points[0][1])}`;
  // Each segment depends only on its endpoints. Appending cannot reshape old data.
  for(let i=1;i<points.length;i++){const a=points[i-1],b=points[i],dx=(b[0]-a[0])/3;d+=` C${coord(a[0]+dx)},${coord(a[1])} ${coord(b[0]-dx)},${coord(b[1])} ${coord(b[0])},${coord(b[1])}`}
  if(points.length===1)d+=` l0.01,0`;
  stroke+=d;area+=`${d} L${coord(points.at(-1)[0])},${baseline} L${coord(points[0][0])},${baseline} Z`;
 }
 return {stroke,area};
}

export function compactTimedSegments(segments,bucketWidth=4){
 return segments.map(points=>{if(points.length<3)return points;const out=[];let bucket=[];const flush=()=>{if(!bucket.length)return;let min=bucket[0],max=bucket[0];for(const p of bucket){if(p[1]<min[1])min=p;if(p[1]>max[1])max=p}const keep=new Set([bucket[0],min,max,bucket.at(-1)]);for(const p of bucket)if(keep.has(p))out.push(p);bucket=[]};let column=null;for(const point of points){const next=Math.floor(point[0]/bucketWidth);if(next!==column){flush();column=next}bucket.push(point)}flush();return out});
}

// Keep real samples beyond the left clip edge until the entire scroll has passed.
// A full extra horizon also covers overlapping nonlinear transitions; hidden
// points never contribute to the visible network scale or fill missing readings.
export function liveSparkWindow(samples){
 const end=samples.at(-1)?.time??0,interval=samples.at(-1)?.interval;
 const spanMs=(Number.isFinite(interval)&&interval>0?Math.max(1,interval):3)*31000;
 const start=liveWindowStart(samples,spanMs,spanMs),recent=samples.slice(start);
 return {start,recent,visible:recent.filter(s=>s.time>=end-spanMs),spanMs};
}

function liveWindowStart(samples,spanMs,overscanMs){
 const end=samples.at(-1)?.time??0,first=samples.findIndex(s=>s.time>=end-spanMs-overscanMs);
 return Math.max(0,(first<0?samples.length:first)-2);
}
// Match the chart's sample-clock domain; preserve predecessors outside its clip.
export function liveRangeSamples(samples,minutes){
 const spanMs=Math.max(1,minutes)*60000;
 return samples.slice(liveWindowStart(samples,spanMs,Math.min(spanMs,60000)));
}
