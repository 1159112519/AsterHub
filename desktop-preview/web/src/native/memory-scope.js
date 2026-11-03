export const memoryAreas=[
 {id:'standby',name:'低优先级待机缓存',description:'保留较活跃的缓存，按需重新载入',tag:'推荐'},
 {id:'allstandby',name:'全部待机缓存',description:'包含低优先级缓存，之后可能增加磁盘读取',tag:'进阶'},
 {id:'workset',name:'进程工作集',description:'收回应用驻留页，进程继续运行',tag:'进阶'}
];
export function toggleMemoryArea(value,id){let next=value.includes(id)?value.filter(v=>v!==id):[...value,id];if(!value.includes(id)&&id==='standby')next=next.filter(v=>v!=='allstandby');if(!value.includes(id)&&id==='allstandby')next=next.filter(v=>v!=='standby');return next}
export function memoryEmphasis(selected,preview,id,canTrim=true){const active=id==='workset'?canTrim&&selected.includes('workset'):selected.includes('allstandby')||id==='low'&&selected.includes('standby');const hover=id==='workset'?canTrim&&preview==='workset':preview==='allstandby'||id==='low'&&preview==='standby';return active?'selected':hover?'preview':'idle'}
export function sortMemoryProcesses(processes,selected,descending=true){const active=p=>selected.includes('workset')&&p.canTrim;return [...processes].sort((a,b)=>Number(active(b))-Number(active(a))||(descending?b.workingSetBytes-a.workingSetBytes:a.workingSetBytes-b.workingSetBytes)||a.pid-b.pid)}
export function supportedMemorySelection(value,capabilities){return (value||[]).filter(id=>capabilities?.[id]?.available)}
export function memoryAreaNames(areas){return memoryAreas.filter(a=>areas.includes(a.id)).map(a=>a.name).join('、')}
