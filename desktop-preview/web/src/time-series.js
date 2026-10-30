// Timeseries geometry is based on sample identity (timestamp), never array index.
export function orderedSamples(rows){const byTime=new Map();for(const row of rows)if(Number.isFinite(row?.time))byTime.set(row.time,row);return [...byTime.values()].sort((a,b)=>a.time-b.time)}
export function gapLimit(timestamps,expectedMs){
 if(Number.isFinite(expectedMs)&&expectedMs>0)return Math.max(1000,expectedMs*3);
 const deltas=timestamps.slice(1).map((t,i)=>t-timestamps[i]).filter(d=>d>0).sort((a,b)=>a-b);
 const median=deltas[Math.floor((deltas.length-1)/2)]||5000;
 return Math.max(3000,median*3);
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
  let d=`M${points[0][0]},${points[0][1]}`;
  // Each segment depends only on its endpoints. Appending cannot reshape old data.
  for(let i=1;i<points.length;i++){const a=points[i-1],b=points[i],dx=(b[0]-a[0])/3;d+=` C${a[0]+dx},${a[1]} ${b[0]-dx},${b[1]} ${b[0]},${b[1]}`}
  if(points.length===1)d+=` l0.01,0`;
  stroke+=d;area+=`${d} L${points.at(-1)[0]},${baseline} L${points[0][0]},${baseline} Z`;
 }
 return {stroke,area};
}
