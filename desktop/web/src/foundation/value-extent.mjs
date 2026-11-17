// Capture exact finite bounds once; new samples cannot silently move a locked axis.
export function valueExtent(series,start=0,end=Infinity){
 let low=Infinity,high=-Infinity;
 for(const row of series)for(let i=start;i<row.values.length&&i<=end;i++){const v=row.values[i];if(Number.isFinite(v)){low=Math.min(low,v);high=Math.max(high,v)}}
 if(!Number.isFinite(low))return null;
 if(low===high){const pad=Math.max(Math.abs(low)*.01,.1);return [low>=0?Math.max(0,low-pad):low-pad,high+pad]}
 return [low,high];
}
