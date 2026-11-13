import {createPollingResource} from '../foundation/polling-resource.js';

// Keep 280px for the map and 6px for its splitter; narrow containers hide the tree.
export function storageTreeBounds(available=1000){return {min:160,max:Math.max(160,Math.min(420,available-286))};}
export function clampStorageTreeWidth(width,available=1000){const {min,max}=storageTreeBounds(available);return Math.max(min,Math.min(max,Number.isFinite(width)?width:220));}

// Completed views survive component remounts; inactive devices retain data, never timers.
export function createStorageDeviceState({request, limit = 8, workspace = null}) {
 if (!Number.isInteger(limit) || limit < 1) throw new RangeError('Invalid storage device cache limit');
 const devices = new Map();
 function trim(keepId) {
  while (devices.size > limit) {
   const victim = [...devices].find(([id,entry]) => id !== keepId && !entry.resource?.diagnostics().subscribers);
   if (!victim) break;
   victim[1].resource?.dispose(); devices.delete(victim[0]);
  }
 }
 function entry(deviceId) {
  let value = devices.get(deviceId);
  if (!value) {const restored=workspace?.open(deviceId,'storage-tools',()=>null).getSnapshot();value={view:restored?{...restored,...(Array.isArray(restored.expanded)?{expanded:new Set(restored.expanded)}:{})}:null,resource:null};}
  devices.delete(deviceId); devices.set(deviceId, value); trim(deviceId);
  return value;
 }
 return {
  read(deviceId) { return entry(deviceId).view; },
  save(deviceId, view) {
   // Keep the current branch and recent folder pages, rather than every visited branch.
   const folders = view.folders || {}, keys = new Set(['0', ...(view.data?.crumbs || []).map(node => node.id)]);
   for (const key of Object.keys(folders).reverse()) { if (keys.size >= 32) break; keys.add(key); }
   workspace?.open(deviceId,'storage-tools',()=>null).setState(view);
   entry(deviceId).view = {...view, folders: Object.fromEntries([...keys].filter(key => folders[key]).map(key => [key, folders[key]]))};
  },
  resource(deviceId) {
   const value = entry(deviceId);
   if (!value.resource) value.resource = createPollingResource({interval: 900, load: signal => request('storage.status', {deviceId}, {signal})});
   return value.resource;
  },
  diagnostics: () => ({devices: devices.size})
 };
}
