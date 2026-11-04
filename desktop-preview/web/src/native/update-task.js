import {useEffect,useState} from 'react';
import {native} from './bridge.js';

export function createUpdateStore(client){
 let snapshot={revision:-1,state:'idle',progress:0},subscribed=false;const listeners=new Set();
 const receive=value=>{if(!value||typeof value.state!=='string'||!Number.isFinite(value.revision)||value.revision<snapshot.revision)return;snapshot=value;for(const listener of listeners)listener(snapshot)};
 return {
  getSnapshot:()=>snapshot,
  subscribe(listener){if(!subscribed){subscribed=true;client.subscribe(receive,'update-status')}listeners.add(listener);listener(snapshot);client.request('updates.status').then(receive).catch(()=>{});return()=>listeners.delete(listener)},
  async request(action){const value=await client.request('updates.'+action,{},action==='check'?35000:30000);receive(value);return value}
 };
}
export const updateStore=createUpdateStore(native);
export function useUpdateTask(){const [task,setTask]=useState(updateStore.getSnapshot);useEffect(()=>updateStore.subscribe(setTask),[]);return task}
