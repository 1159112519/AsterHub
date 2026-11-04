// Only inert view preferences may cross a renderer restart. Never serialize arbitrary
// component state: it can contain credentials, action tokens, processes or live jobs.
export const workspaceStorageKey='asterhub-device-workspaces-v1';
export function browserWorkspaceStorage(){try{return globalThis.localStorage??null}catch{return null}}
const object=value=>value&&typeof value==='object'&&!Array.isArray(value);
const text=value=>typeof value==='string'&&value.length<=4096&&!value.includes('\0');
const id=value=>text(value)&&value.length<=256;
const number=(value,min=0,max=1e9)=>Number.isFinite(value)&&value>=min&&value<=max;
const choices=values=>value=>values.includes(value);
const areas=value=>Array.isArray(value)&&value.length<=7&&value.every(choices(['standby','allstandby','workset','filecache','modified','registry','combine']));
const hidden=value=>object(value)&&Object.keys(value).length<=30&&Object.entries(value).every(([key,item])=>id(key)&&typeof item==='boolean');
const schemas={
 navigation:{page:choices(['overview','cpu','gpu','memory','network','storage','history','terminal','files'])},
 'workspace-tools':{storageTool:choices([null,'analysis','large']),memoryTool:choices(['monitor','cleanup','policy'])},
 history:{minutes:value=>number(value,1,525600),metric:choices(['cpu','gpu','memory'])},
 files:{leftId:id,rightId:id,ratio:value=>number(value,28,72),showTransfers:value=>typeof value==='boolean'},
 'network-tool':{tab:choices(['monitor','speed'])},
 network:{adapter:id,range:value=>number(value,1,525600),hidden},
 'storage-metrics':{diskId:id,volumeId:text,range:value=>number(value,1,525600),tab:choices(['throughput','active']),hidden},
 'memory-record-range':{days:choices([1,7,30,90]),filter:choices(['all','manual','automatic'])},
 'memory-tools':{tab:choices(['monitor','cleanup','policy']),selected:value=>value===null||areas(value)},
 'storage-tools':{tool:choices([null,'analysis','large']),path:text,layout:choices(['map','list']),query:text,kind:text,minimum:value=>number(value,0,1e18),age:value=>number(value,0,365000),treeWidth:value=>number(value,160,420)}
};
export function persistentWorkspaceValue(page,value){
 if(page.startsWith('scroll:'))return number(value,0,1e9)?value:undefined;
 if(!object(value))return undefined;
 const schema=page.startsWith('files-pane:')?{path:text}:page.startsWith('resource-inspector:')?{minutes:value=>number(value,1,525600)}:schemas[page];
 if(!schema)return undefined;
 const safe=Object.fromEntries(Object.entries(schema).filter(([key,valid])=>Object.hasOwn(value,key)&&valid(value[key])).map(([key])=>[key,value[key]]));
 // Snapshot identity only gates navigation restoration against a fresh native status.
 // It cannot resume a scan or manufacture a completed/live snapshot.
 if(page==='storage-tools'){
  const snapshotId=value.status?.snapshot?.id??value.snapshotId;
  if(id(snapshotId)&&id(value.nodeId)){
   safe.snapshotId=snapshotId;safe.nodeId=value.nodeId;
   if(number(value.offset,0,1e9))safe.offset=value.offset;
   const expanded=value.expanded instanceof Set?[...value.expanded]:value.expanded;
   if(Array.isArray(expanded)&&expanded.length<=256&&expanded.every(id))safe.expanded=[...expanded];
   const history=value.history;
   if(object(history)&&Array.isArray(history.entries)&&history.entries.length<=256&&Number.isInteger(history.cursor)&&history.cursor>=0&&history.cursor<history.entries.length&&history.entries.every(entry=>object(entry)&&id(entry.id)))safe.history={entries:history.entries.map(entry=>({id:entry.id,directory:entry.directory===true})),cursor:history.cursor};
  }
 }
 // An unsaved policy is a draft only; restoration never invokes policy.save.
 if(page==='memory-tools'&&value.dirty===true&&object(value.policy)){
  const p=value.policy;
  if(typeof p.enabled==='boolean'&&number(p.threshold,1,100)&&number(p.durationMinutes,0,1440)&&number(p.cooldownMinutes,0,525600)&&areas(p.areas)){
   safe.policy={enabled:p.enabled,threshold:p.threshold,durationMinutes:p.durationMinutes,cooldownMinutes:p.cooldownMinutes,areas:[...p.areas]};safe.dirty=true;
  }
 }
 return Object.keys(safe).length?safe:undefined;
}
export function validWorkspaceId(value){return id(value)&&value.length>0}
