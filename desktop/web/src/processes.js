// Filter presentation only: telemetry and archived VRAM totals remain intact.
export function isKnownProcessName(name){
 const value=String(name??'').trim().toLowerCase();
 return Boolean(value)&&!value.startsWith('[not found]')&&!/^(?:not[ _-]*found|unknown(?: process)?|other processes?|n\/a|其他进程|未知进程|未知|[-—–])$/.test(value);
}
export function visibleProcesses(processes=[]){return processes.filter(p=>isKnownProcessName(p.name))}
