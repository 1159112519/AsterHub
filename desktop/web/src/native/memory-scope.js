export const memoryAreas=[
 {id:'standby',name:'低优先级待机缓存',description:'保留较活跃的缓存，按需重新载入',tag:'推荐'},
 {id:'allstandby',name:'全部待机缓存',description:'包含低优先级缓存，之后可能增加磁盘读取',tag:'进阶'},
 {id:'workset',name:'进程工作集',description:'跳过保留程序，收回应用驻留页，进程继续运行',tag:'进阶'},
 {id:'filecache',name:'系统文件缓存',description:'收回文件缓存工作集，之后可能重新读取文件',tag:'管理员'},
 {id:'modified',name:'已修改页面列表',description:'将已修改页面写回磁盘，可能增加磁盘活动',tag:'管理员'},
 {id:'registry',name:'注册表缓存',description:'请求 Windows 写回注册表配置单元',tag:'管理员'},
 {id:'combine',name:'合并相同内存页',description:'合并内容相同的物理页面，由 Windows 判断可合并量',tag:'管理员'}
];
export function toggleMemoryArea(value,id){let next=value.includes(id)?value.filter(v=>v!==id):[...value,id];if(!value.includes(id)&&id==='standby')next=next.filter(v=>v!=='allstandby');if(!value.includes(id)&&id==='allstandby')next=next.filter(v=>v!=='standby');return next}
export function memoryEmphasis(selected,preview,id,canTrim=true){const active=id==='workset'?canTrim&&selected.includes('workset'):selected.includes('allstandby')||id==='low'&&selected.includes('standby');const hover=id==='workset'?canTrim&&preview==='workset':preview==='allstandby'||id==='low'&&preview==='standby';return active?'selected':hover?'preview':'idle'}
export function sortMemoryProcesses(processes,selected,descending=true){const active=p=>selected.includes('workset')&&Boolean(p.canTrim||p.protectedProgram);return [...processes].sort((a,b)=>Number(active(b))-Number(active(a))||(descending?b.workingSetBytes-a.workingSetBytes:a.workingSetBytes-b.workingSetBytes)||a.pid-b.pid)}
export function supportedMemorySelection(value,capabilities){return (value||[]).filter(id=>capabilities?.[id]?.available)}
export function memoryAreaNames(areas){return memoryAreas.filter(a=>areas.includes(a.id)).map(a=>a.name).join('、')}

// A Windows FILETIME exceeds JavaScript safe integers: pass the host string unchanged.
export function memoryProcessIdentity(process){if(typeof process.startIdentity!=='string'||!/^\d+$/.test(process.startIdentity))throw new Error('进程身份不可用，请刷新列表');return {pid:process.pid,path:process.path,startTicks:process.startIdentity}}

const sizeFields={standby:'lowStandbyBytes',allstandby:'standbyBytes',filecache:'fileCacheWorkingSetBytes',modified:'modifiedBytes'};
const validBytes=value=>typeof value==='number'&&Number.isFinite(value)&&value>=0;
export function memoryAreaSize(reading,area){const size=reading?.[sizeFields[area]];return validBytes(size)?size:null}
export function memoryAreaResult(item){
 if(item.status==='failed')return {kind:'failed',message:item.message};
 if(item.area==='workset'&&validBytes(item.observedReductionBytes))return {kind:'workset',size:item.observedReductionBytes};
 if(item.area==='registry')return {kind:'status'};
 if(item.area==='combine'&&validBytes(item.combinedBytes))return {kind:'combined',size:item.combinedBytes};
 if(sizeFields[item.area]&&validBytes(item.beforeBytes)&&validBytes(item.afterBytes))return {kind:'observed',before:item.beforeBytes,after:item.afterBytes,change:item.afterBytes-item.beforeBytes};
 return {kind:'unavailable'};
}
