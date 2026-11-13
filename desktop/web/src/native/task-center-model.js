import {isCleanupActive} from './task-inbox.js';
const updateStates=new Set(['downloading','cancelling','ready','installing','cancelled','error']);
const pendingUpdate=new Set(['downloading','cancelling','ready','installing']);
const pendingScan=new Set(['queued','running','enumerating','aggregating','paused']);
const pendingTransfer=new Set(['queued','running','undoing']);
export const isActiveTransfer=status=>pendingTransfer.has(status);
export const isActiveScan=status=>pendingScan.has(status);
export const formatTransferSize=n=>!Number.isFinite(n)?'—':n<1024?n+' B':n<1048576?(n/1024).toFixed(1)+' KiB':n<1073741824?(n/1048576).toFixed(1)+' MiB':(n/1073741824).toFixed(2)+' GiB';
export const transferLabels={queued:'排队中',running:'正在复制',completed:'已完成',cancelled:'已取消',failed:'失败',undoing:'正在撤销',undone:'已撤销','undo-failed':'撤销未全部完成'};
const activeFirst=(jobs,active)=>[...jobs.filter(job=>active(job.status)),...jobs.filter(job=>!active(job.status))];
export function buildTaskGroups(update={},transfers=[],scans=[],cleanup=[]){
 const showUpdate=update.available!==false&&update.latest&&updateStates.has(update.state);
 return [
  {kind:'update',jobs:showUpdate?[{...update,id:'update',status:update.state}]:[]},
  {kind:'transfer',jobs:activeFirst(transfers,isActiveTransfer)},
  {kind:'scan',jobs:activeFirst(scans,isActiveScan)},
  {kind:'cleanup',jobs:activeFirst(cleanup,isCleanupActive)},
 ].filter(group=>group.jobs.length);
}
export function countPendingTasks(groups){return groups.reduce((total,{kind,jobs})=>total+jobs.filter(job=>kind==='cleanup'?isCleanupActive(job.status):(kind==='update'?pendingUpdate:kind==='transfer'?pendingTransfer:pendingScan).has(job.status)).length,0);}
