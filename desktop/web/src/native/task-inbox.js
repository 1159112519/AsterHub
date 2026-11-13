export const isCleanupActive=status=>status==='queued'||status==='running';
export function isTaskRunning(kind,job){
 const status=job.status;
 if(kind==='cleanup')return isCleanupActive(status);
 if(kind==='scan')return ['queued','running','enumerating','aggregating','paused'].includes(status);
 if(kind==='transfer')return ['queued','running','undoing'].includes(status);
 return ['downloading','cancelling','installing'].includes(status);
}
// A ready update still needs installation; a live failed or cancelled update can resume.
export const canClearFinishedTask=(kind,job)=>!isTaskRunning(kind,job)&&!(kind==='update'&&(job.status==='ready'||(!job.archived&&['error','cancelled'].includes(job.status))));
export const taskIdentity=(kind,job)=>kind+':'+(kind==='update'?job.latest:job.id);
const revision=job=>job.status+'|'+(job.finished||'');
const archiveKey='asterhub-task-inbox-v1';
export function readTaskInbox(storage){
 try{const inbox=JSON.parse(storage?.getItem(archiveKey)||'{}');return inbox&&typeof inbox==='object'&&!Array.isArray(inbox)?inbox:{}}
 catch{return {}}
}
export function writeTaskInbox(storage,inbox){try{storage?.setItem(archiveKey,JSON.stringify(inbox))}catch{}}
export function recordTaskInbox(previous,groups,now=new Date().toISOString()){
 let next=previous;
 for(const {kind,jobs} of groups)for(const job of jobs){
  const key=taskIdentity(kind,job),known=next[key],active=isTaskRunning(kind,job);
  if(active){if(!known)continue;const copy={...next};delete copy[key];next=copy;continue;}
  if(known?.revision===revision(job))continue;
  next={...next,[key]:{kind,job,revision:revision(job),time:job.finished||now,dismissed:false}};
 }
 if(next===previous)return previous;
 return Object.fromEntries(Object.entries(next).sort(([,a],[,b])=>Date.parse(b.time)-Date.parse(a.time)).slice(0,200));
}
export function dismissTask(inbox,kind,job){
 if(isTaskRunning(kind,job))return inbox;
 const key=taskIdentity(kind,job),saved=inbox[key];
 return saved?{...inbox,[key]:{...saved,dismissed:true}}:inbox;
}
export function clearFinishedTasks(inbox,live){
 const visible=taskInboxGroups(live,inbox),recorded=recordTaskInbox(inbox,live);
 let next=recorded;
 for(const {kind,jobs} of visible)for(const job of jobs){
  if(!canClearFinishedTask(kind,job))continue;
  const key=taskIdentity(kind,job),item=recorded[key];
  if(!item||item.dismissed)continue;
  if(next===recorded)next={...recorded};
  next[key]={...item,dismissed:true};
 }
 return next;
}
export function taskInboxGroups(live,inbox){
 const liveKeys=new Set(live.flatMap(({kind,jobs})=>jobs.map(job=>taskIdentity(kind,job))));
 return ['update','transfer','scan','cleanup'].flatMap(kind=>{
  const current=live.find(group=>group.kind===kind)?.jobs||[];
  const active=current.filter(job=>isTaskRunning(kind,job));
  const recent=current.filter(job=>!isTaskRunning(kind,job)&&!inbox[taskIdentity(kind,job)]?.dismissed).map(job=>({...job,time:inbox[taskIdentity(kind,job)]?.time||job.finished}));
  const stored=Object.entries(inbox).filter(([key,item])=>item.kind===kind&&!item.dismissed&&!liveKeys.has(key)).map(([,item])=>({...item.job,time:item.time,archived:true}));
  const jobs=[...active,...[...recent,...stored].sort((a,b)=>(Date.parse(b.time)||0)-(Date.parse(a.time)||0))];
  return jobs.length?[{kind,jobs}]:[];
 });
}
