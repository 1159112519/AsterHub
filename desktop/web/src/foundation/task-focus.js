// One-shot view focus survives a pending device/page mount, but is never persisted.
export function createTaskFocus(){const pending=new Map(),listeners=new Set();let revision=0;return {request(id){pending.set(id,++revision);for(const listener of listeners)listener()},read:id=>pending.get(id)||0,consume(id,ticket){if(pending.get(id)!==ticket)return;pending.delete(id);for(const listener of listeners)listener()},subscribe(fn){listeners.add(fn);return()=>listeners.delete(fn)}}}
export const storageTaskFocus=createTaskFocus();
