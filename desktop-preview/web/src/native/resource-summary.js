// Presentation values retain missing readings instead of inventing zeroes.
const finite = v => typeof v === 'number' && Number.isFinite(v);
export function storageSummary(sample) {
 const volumes=(sample?.volumes||[]).filter(v=>finite(v.TotalBytes)&&v.TotalBytes>0);
 const total=volumes.length?volumes.reduce((n,v)=>n+v.TotalBytes,0):null;
 const complete=volumes.length>0&&volumes.every(v=>finite(v.UsedBytes));
 const used=complete?volumes.reduce((n,v)=>n+v.UsedBytes,0):null;
 return {total,used,free:used==null?null:Math.max(0,total-used),percent:used==null?null:used/total*100,count:volumes.length};
}
export function gpuSummary(sample) {
 const gpus=sample?.gpus||[],values=gpus.map(g=>g.util).filter(finite);
 const sum=key=>gpus.length&&gpus.every(g=>finite(g[key]))?gpus.reduce((n,g)=>n+g[key],0):null;
 const clocks=gpus.map(g=>g.clock).filter(v=>finite(v)&&v>0);
 return {count:gpus.length,util:values.length?values.reduce((a,b)=>a+b,0)/values.length:null,used:sum('used'),total:sum('total'),clock:clocks.length?Math.max(...clocks):null};
}
export function memorySpec(modules=[]) {
 const valid=v=>v&&!/WMI|原值|未校验|未提供/i.test(v);
 const generations=[...new Set(modules.map(m=>m.Generation).filter(valid))].join(' / ');
 const speeds=[...new Set(modules.map(m=>[m.ConfiguredSpeed,m.RatedSpeed].find(v=>valid(v)&&/^\d+(?:\.\d+)?\s*(?:MT\/s|MHz)$/i.test(String(v).trim()))).filter(Boolean))].join(' / ');
 return [generations,speeds,modules.length?modules.length+' 条内存':null].filter(Boolean).join(' · ')||'物理内存';
}
export function frequency(mhz) { return finite(mhz)&&mhz>0?(mhz>=1000?(mhz/1000).toFixed(2)+' GHz':Math.round(mhz)+' MHz'):'频率未提供'; }
