import {useSyncExternalStore} from 'react';
import {createPollingResource} from '../foundation/polling-resource.js';
import {native} from './bridge.js';
import {isActiveTransfer} from './task-center-model.js';

// Host queue spans all endpoints. All surfaces share one serial list request.
export const fileTransfers=createPollingResource({interval:1500,nextInterval:data=>data?.some(job=>isActiveTransfer(job.status))?1500:8000,load:signal=>native.request('files.transfers',{}, {signal})});
const empty={data:null,error:null,loading:false},emptyList=[];
const noop=()=>()=>{},getEmpty=()=>empty;
export function useFileTransfers(enabled=true){
 const snapshot=useSyncExternalStore(enabled?fileTransfers.subscribe:noop,enabled?fileTransfers.getSnapshot:getEmpty);
 return {...snapshot,jobs:snapshot.data||emptyList};
}
export async function refreshFileTransfers(){
 // A request already in flight may have read the old state before the command.
 if(fileTransfers.diagnostics().pending)await fileTransfers.refresh();
 await fileTransfers.refresh();
}
