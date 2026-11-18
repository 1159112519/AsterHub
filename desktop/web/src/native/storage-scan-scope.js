export function chooseScanRoot(roots,path='') {
 const normalize=v=>String(v||'').replaceAll('\\','/').replace(/\/+$/,'');
 const available=roots.filter(r=>r.available!==false);
 return available.filter(r=>{let root=normalize(r.path),p=normalize(path);if(/^\/?[A-Za-z]:/.test(root)||root.startsWith('//')){root=root.toLowerCase();p=p.toLowerCase()}return !root||p===root||p.startsWith(root+'/')}).sort((a,b)=>b.path.length-a.path.length)[0]?.path||available[0]?.path||'';
}
export function driveUsage(root){const total=root.totalBytes,used=root.usedBytes;return typeof total==='number'&&Number.isFinite(total)&&total>0&&typeof used==='number'&&Number.isFinite(used)&&used>=0?Math.min(100,used/total*100):null}

export function scanRecordLabel(path=''){const normalized=path.replace(/[\\/]+$/,'');const name=normalized.split(/[\\/]/).pop();return name&&normalized.length>name.length?name+' · '+path:path;}

export function selectedScanPaths(roots,selection){
 const selected=new Set(selection);
 return [...new Set(roots.filter(root=>root.available!==false&&root.kind!=='NoRootDirectory'&&selected.has(root.path)).map(root=>root.path))];
}
export function scanSelectionPayload(paths){return paths.length===1?paths[0]:paths.length?{paths:[...paths]}:null;}
