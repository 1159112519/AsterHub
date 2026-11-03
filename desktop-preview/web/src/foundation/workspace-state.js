// Session memory only. This cache owns no timers, requests, credentials or disk storage.
export function createWorkspaceState({maxDevices=20,maxPages=20}={}){
 if(!Number.isInteger(maxDevices)||maxDevices<1||!Number.isInteger(maxPages)||maxPages<1)throw new RangeError('Invalid workspace cache capacity');
 const devices=new Map();
 const notify=entry=>{for(const fn of [...entry.listeners])fn()};
 function revoke(entry){entry.revoked=true;entry.value=entry.initial;notify(entry)}
 function dropPage(pages,page){const entry=pages.get(page);if(!entry)return;pages.delete(page);revoke(entry)}
 function dropDevice(id){const pages=devices.get(id);if(!pages)return;devices.delete(id);for(const entry of pages.values())revoke(entry);pages.clear()}
 function touch(device,page,entry){const pages=devices.get(device);if(!pages||pages.get(page)!==entry)return false;devices.delete(device);devices.set(device,pages);pages.delete(page);pages.set(page,entry);return true}
 function removeDevice(id){id=String(id);dropDevice(id);for(const [owner,pages] of [...devices]){for(const [page,entry] of [...pages])if(entry.dependencies.has(id))dropPage(pages,page);if(!pages.size)devices.delete(owner)}}
 return {
  open(device,page,initial,{dependencies=[]}={}){
   device=String(device);page=String(page);let pages=devices.get(device);
   if(!pages){pages=new Map();devices.set(device,pages)}
   let entry=pages.get(page);
   if(!entry){const value=typeof initial==='function'?initial():initial;entry={initial:value,value,revoked:false,listeners:new Set(),dependencies:new Set(dependencies.map(String))};
    entry.handle={getSnapshot:()=>entry.value,subscribe:fn=>{entry.listeners.add(fn);return()=>entry.listeners.delete(fn)},setState:update=>{
     if(entry.revoked||!touch(device,page,entry))return;
     const next=typeof update==='function'?update(entry.value):update;if(Object.is(next,entry.value))return;entry.value=next;notify(entry);
    }};pages.set(page,entry);
   }
   entry.dependencies=new Set(dependencies.map(String));touch(device,page,entry);
   while(pages.size>maxPages)dropPage(pages,pages.keys().next().value);
   while(devices.size>maxDevices)dropDevice(devices.keys().next().value);
   return entry.handle;
  },
  has:(device,page)=>devices.get(String(device))?.has(String(page))??false,
  removeDevice,
  retainDevices(ids){const allowed=new Set(ids.map(String));for(const id of [...devices.keys()])if(!allowed.has(id))removeDevice(id);for(const [owner,pages] of [...devices]){for(const [page,entry] of [...pages])if([...entry.dependencies].some(id=>!allowed.has(id)))dropPage(pages,page);if(!pages.size)devices.delete(owner)}},
  clear(){for(const id of [...devices.keys()])dropDevice(id)},
  diagnostics:()=>({devices:devices.size,pages:[...devices.values()].reduce((n,pages)=>n+pages.size,0)})
 };
}
export function isWorkspaceResultFresh(result,key,maxAge=30000,now=Date.now()){
 return result?.key===key&&Number.isFinite(result.fetchedAt)&&now>=result.fetchedAt&&now-result.fetchedAt<maxAge;
}
export const workspaceState=createWorkspaceState();
