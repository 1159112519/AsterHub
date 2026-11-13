import {useSyncExternalStore} from 'react';
import {workspaceState} from './workspace-state.js';

// A render selects its device synchronously; its setter always stays bound to that
// exact cache entry, including asynchronous callbacks from a previous device.
export function useWorkspaceState(deviceId,page,initial,{dependencies=[]}={}){
 const device=String(deviceId??'local');
 // A removed dependency revokes its entry. Reopen on render so the subscriber
 // receives a fresh writable handle instead of staying bound to a tombstone.
 const handle=workspaceState.open(device,page,initial,{dependencies});
 const value=useSyncExternalStore(handle.subscribe,handle.getSnapshot,handle.getSnapshot);
 return [value,handle.setState];
}
