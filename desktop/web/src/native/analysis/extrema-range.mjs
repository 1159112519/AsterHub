// Return real sampled extrema inside the inclusive custom time interval.
// Missing values are never treated as zero. Equal extrema use the widest pair.
export function extremaRange(values,start,end){
 let low=Infinity,high=-Infinity,min=-1,max=-1;
 for(let i=Math.max(0,start);i<values.length&&i<=end;i++){
  const v=values[i];if(!Number.isFinite(v))continue;
  if(v<low){low=v;min=i}if(v>=high){high=v;max=i}
 }
 return min<0||min===max?null:[Math.min(min,max),Math.max(min,max)];
}
