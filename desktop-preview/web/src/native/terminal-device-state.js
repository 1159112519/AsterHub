// A renderer and its read cursor live for the lifetime of its native session.
// Device selection changes only the visible renderer, never native ownership.
export function createTerminalDeviceState({request,render,dispose,prepare=()=>{},onChange=()=>{}}){
 const records=new Map(),devices=new Map(),closed=new Set(),rendering=new Map();
 // No inventory has arrived yet. A missing startup snapshot is not device removal.
 let knownDevices=null,viewDevice=null,viewActive=false;
 const hasDevice=deviceId=>knownDevices===null||knownDevices.has(deviceId);
 function state(deviceId){if(!devices.has(deviceId))devices.set(deviceId,{selectedId:null,pending:0,error:'',restored:false,restorePromise:null,createPromise:null,ensurePromise:null});return devices.get(deviceId)}
 // State identity also invalidates an old operation if the same ID is added again.
 const current=(deviceId,owner)=>hasDevice(deviceId)&&devices.get(deviceId)===owner;
 const tabs=deviceId=>[...records.values()].filter(record=>record.deviceId===deviceId&&!record.stopped);
 function selected(deviceId){const s=state(deviceId),record=records.get(s.selectedId);return record?.deviceId===deviceId&&!record.stopped?record:tabs(deviceId).at(-1)}
 function release(record){if(!record.stopped)dispose(record)}
 function updateDevices(ids){
  const next=new Set(ids),changed=knownDevices===null||next.size!==knownDevices.size||[...next].some(id=>!knownDevices.has(id));knownDevices=next;
  for(const [deviceId] of devices)if(!next.has(deviceId))devices.delete(deviceId);
  for(const [id,record] of records)if(!next.has(record.deviceId)){closed.add(id);release(record);records.delete(id)}
  if(changed)onChange();
 }
 function setError(deviceId,error=''){if(!hasDevice(deviceId))return;state(deviceId).error=error;onChange()}
 async function run(deviceId,work,owner=state(deviceId)){
  if(!current(deviceId,owner))return;
  owner.pending++;owner.error='';onChange();
  try{return await work()}catch(error){if(current(deviceId,owner)){owner.error=error.message;throw error}}finally{owner.pending--;if(current(deviceId,owner))onChange()}
 }
 async function adopt(meta,settings,owner){
  if(!current(meta.deviceId,owner)||closed.has(meta.id))return;
  if(records.has(meta.id))return records.get(meta.id);
  const existing=rendering.get(meta.id);if(existing?.owner===owner)return existing.promise;
  const entry={owner};
  entry.promise=(async()=>{const record=await render(meta,settings,()=>current(meta.deviceId,owner)&&!closed.has(meta.id));if(!record)return;if(!current(meta.deviceId,owner)||closed.has(meta.id)){release(record);return}records.set(meta.id,record);if(!selected(meta.deviceId)||!owner.selectedId)owner.selectedId=meta.id;onChange();return record})().finally(()=>{if(rendering.get(meta.id)===entry)rendering.delete(meta.id)});
  rendering.set(meta.id,entry);return entry.promise;
 }
 function restore(deviceId,settings,owner){
  if(!current(deviceId,owner)||owner.restored)return Promise.resolve();if(owner.restorePromise)return owner.restorePromise;
  owner.restorePromise=(async()=>{const list=await request('terminal.list',{deviceId});if(!current(deviceId,owner))return;for(const meta of list){if(!current(deviceId,owner))return;if(meta.deviceId===deviceId)await adopt(meta,settings,owner)}if(current(deviceId,owner))owner.restored=true})().finally(()=>{owner.restorePromise=null});
  return owner.restorePromise;
 }
 function create(deviceId,settings){
  const owner=state(deviceId);if(!current(deviceId,owner))return Promise.resolve();if(owner.createPromise)return owner.createPromise;
  owner.createPromise=run(deviceId,async()=>{await prepare();if(!current(deviceId,owner))return;const meta=await request('terminal.create',{deviceId,cols:100,rows:30},20000);if(!current(deviceId,owner))return;if(!meta?.id||meta.deviceId!==deviceId)throw new Error('终端所属设备不匹配，请重新打开');const record=await adopt(meta,settings,owner);if(current(deviceId,owner)){if(record)owner.selectedId=record.id;onChange()}return record},owner).finally(()=>{owner.createPromise=null});
  return owner.createPromise;
 }
 function ensure(deviceId,settings){
  const owner=state(deviceId);if(!current(deviceId,owner))return Promise.resolve();if(owner.ensurePromise)return owner.ensurePromise;
  owner.ensurePromise=run(deviceId,async()=>{await restore(deviceId,settings,owner);if(current(deviceId,owner)&&!tabs(deviceId).length)await create(deviceId,settings)},owner).finally(()=>{owner.ensurePromise=null});
  return owner.ensurePromise;
 }
 function select(deviceId,id){const record=records.get(id);if(!hasDevice(deviceId)||!record||record.deviceId!==deviceId||record.stopped||record.closing)return false;state(deviceId).selectedId=id;onChange();return true}
 async function close(record){
  if(!record||record.stopped||record.closing||!hasDevice(record.deviceId))return;
  const owner=state(record.deviceId);record.closing=true;closed.add(record.id);
  try{return await run(record.deviceId,async()=>{await request('terminal.close',{id:record.id});release(record);records.delete(record.id);if(current(record.deviceId,owner)){if(owner.selectedId===record.id)owner.selectedId=tabs(record.deviceId).at(-1)?.id||null;onChange()}},owner)}catch(error){closed.delete(record.id);throw error}finally{record.closing=false}
 }
 return {records,tabs,selected,state,ensure,create,close,select,setError,updateDevices,hasDevice,
  setView(deviceId,active){viewDevice=deviceId;viewActive=active},
  isInputOwner(record){return viewActive&&hasDevice(viewDevice)&&record.deviceId===viewDevice&&selected(viewDevice)===record&&!record.stopped&&!record.closing},
 };
}
