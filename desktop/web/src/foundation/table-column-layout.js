export function fitColumns(sizes,minima,available){
 const minimum=minima.reduce((s,v)=>s+v,0),space=Math.max(minimum,Number.isFinite(available)?available:minimum)-minimum;
 const weights=minima.map((min,i)=>i===0&&min===44?0:Math.max(0,(Number.isFinite(sizes?.[i])?sizes[i]:min)-min)),total=weights.reduce((s,v)=>s+v,0);
 const elastic=minima.map((min,i)=>i===0&&min===44?0:1),elasticTotal=elastic.reduce((s,v)=>s+v,0)||1;
 const result=minima.map((min,i)=>min+space*(total?weights[i]/total:elastic[i]/elasticTotal));
 if(result.length)result[result.length-1]+=minimum+space-result.reduce((s,v)=>s+v,0);
 return result;
}
