import {useMemo,useSyncExternalStore} from 'react';
import {workspaceState} from './workspace-state.js';

// A render selects its device synchronously; its setter always stays bound to that
// exact cache entry, including asynchronous callbacks from a previous device.
export function useWorkspaceState(deviceId,page,initial,{dependencies=[]}={}){
 const device=String(deviceId??'local'),dependencyKey=JSON.stringify(dependencies.map(String));
 const handle=useMemo(()=>workspaceState.open(device,page,initial,{dependencies}),[device,page,dependencyKey]);
 const value=useSyncExternalStore(handle.subscribe,handle.getSnapshot,handle.getSnapshot);
 return [value,handle.setState];
}
