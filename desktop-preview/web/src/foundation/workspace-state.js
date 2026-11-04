import {browserWorkspaceStorage,persistentWorkspaceValue,validWorkspaceId,workspaceStorageKey} from './workspace-persistence.js';

// Runtime results remain in memory. Persistence is opt-in and strictly allowlisted.
export function createWorkspaceState({maxDevices=20,maxPages=20,storage=null,storageKey=workspaceStorageKey}={}){
 if(!Number.isInteger(maxDevices)||maxDevices<1||!Number.isInteger(maxPages)||maxPages<1)throw new RangeError('Invalid workspace cache capacity');
 const devices=new Map(),saved=new Map();let lastWritten=null;
 try{
  const raw=storage?.getItem(storageKey),data=raw?JSON.parse(raw):null;
  if(data?.version===1&&Array.isArray(data.entries))for(const record of data.entries.slice(-maxDevices*maxPages)){
   if(!validWorkspaceId(record?.device)||!validWorkspaceId(record?.page))continue;
   const value=persistentWorkspaceValue(record.page,record.value);if(value===undefined)continue;
   let pages=saved.get(record.device);if(!pages){pages=new Map();saved.set(record.device,pages)}
   pages.set(record.page,{value,dependencies:new Set((Array.isArray(record.dependencies)?record.dependencies:[]).filter(validWorkspaceId).slice(0,maxDevices))});
   while(pages.size>maxPages)pages.delete(pages.keys().next().value);
   while(saved.size>maxDevices)saved.delete(saved.keys().next().value);
  }
 }catch{}
 function persist(){if(!storage)return;try{
  const entries=[...saved].flatMap(([device,pages])=>[...pages].map(([page,entry])=>({device,page,value:entry.value,dependencies:[...entry.dependencies]})));
  const json=JSON.stringify({version:1,entries});if(json!==lastWritten){storage.setItem(storageKey,json);lastWritten=json}
 }catch{}}
 function remember(device,page,entry){
  if(!storage)return;const value=persistentWorkspaceValue(page,entry.value);if(value===undefined)return;
  let pages=saved.get(device);if(!pages){pages=new Map();saved.set(device,pages)}else{saved.delete(device);saved.set(device,pages)}
  pages.delete(page);pages.set(page,{value,dependencies:new Set(entry.dependencies)});
  while(pages.size>maxPages)pages.delete(pages.keys().next().value);
  while(saved.size>maxDevices)saved.delete(saved.keys().next().value);
  persist();
 }
 const notify=entry=>{for(const fn of [...entry.listeners])fn()};
 function revoke(entry){entry.revoked=true;entry.value=entry.initial;notify(entry)}
 function dropPage(pages,page){const entry=pages.get(page);if(!entry)return;pages.delete(page);revoke(entry)}
 function dropDevice(id){const pages=devices.get(id);if(!pages)return;devices.delete(id);for(const entry of pages.values())revoke(entry);pages.clear()}
 function touch(device,page,entry){const pages=devices.get(device);if(!pages||pages.get(page)!==entry)return false;devices.delete(device);devices.set(device,pages);pages.delete(page);pages.set(page,entry);return true}
 function depends(page,entry,id){return entry.dependencies.has(id)||(page==='files'&&(entry.value?.leftId===id||entry.value?.rightId===id))}
 function unavailable(page,entry,allowed){return [...entry.dependencies].some(id=>!allowed.has(id))||(page==='files'&&[entry.value?.leftId,entry.value?.rightId].some(id=>id!=null&&!allowed.has(id)))}
 function removeDevice(id){id=String(id);dropDevice(id);saved.delete(id);
  for(const [owner,pages] of [...devices]){for(const [page,entry] of [...pages])if(depends(page,entry,id))dropPage(pages,page);if(!pages.size)devices.delete(owner)}
  for(const [owner,pages] of [...saved]){for(const [page,entry] of [...pages])if(depends(page,entry,id))pages.delete(page);if(!pages.size)saved.delete(owner)}persist();
 }
 return {
  open(device,page,initial,{dependencies=[]}={}){
   device=String(device);page=String(page);let pages=devices.get(device);
   if(!pages){pages=new Map();devices.set(device,pages)}
   let entry=pages.get(page);
   if(!entry){const fallback=typeof initial==='function'?initial():initial,remembered=saved.get(device)?.get(page)?.value;
    const value=remembered===undefined?fallback:(fallback&&typeof fallback==='object'&&!Array.isArray(fallback)?{...fallback,...remembered}:remembered);
    entry={initial:fallback,value,revoked:false,listeners:new Set(),dependencies:new Set(dependencies.map(String))};
    entry.handle={getSnapshot:()=>entry.value,subscribe:fn=>{entry.listeners.add(fn);return()=>entry.listeners.delete(fn)},setState:update=>{
     if(entry.revoked||!touch(device,page,entry))return;
     const next=typeof update==='function'?update(entry.value):update;if(Object.is(next,entry.value))return;entry.value=next;remember(device,page,entry);notify(entry);
    }};pages.set(page,entry);
   }
   entry.dependencies=new Set(dependencies.map(String));touch(device,page,entry);
   while(pages.size>maxPages)dropPage(pages,pages.keys().next().value);
   while(devices.size>maxDevices)dropDevice(devices.keys().next().value);
   return entry.handle;
  },
  has:(device,page)=>devices.get(String(device))?.has(String(page))??false,
  removeDevice,
  retainDevices(ids){const allowed=new Set(ids.map(String));for(const id of new Set([...devices.keys(),...saved.keys()]))if(!allowed.has(id))removeDevice(id);
   for(const [owner,pages] of [...devices]){for(const [page,entry] of [...pages])if(unavailable(page,entry,allowed))dropPage(pages,page);if(!pages.size)devices.delete(owner)}
   for(const [owner,pages] of [...saved]){for(const [page,entry] of [...pages])if(unavailable(page,entry,allowed))pages.delete(page);if(!pages.size)saved.delete(owner)}persist();
  },
  clear(){for(const id of [...devices.keys()])dropDevice(id);saved.clear();persist()},
  diagnostics:()=>({devices:devices.size,pages:[...devices.values()].reduce((n,pages)=>n+pages.size,0)})
 };
}
export function isWorkspaceResultFresh(result,key,maxAge=30000,now=Date.now()){
 return result?.key===key&&Number.isFinite(result.fetchedAt)&&now>=result.fetchedAt&&now-result.fetchedAt<maxAge;
}
export const workspaceState=createWorkspaceState({storage:browserWorkspaceStorage()});
