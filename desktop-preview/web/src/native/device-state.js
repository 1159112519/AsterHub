// Transport state must never be interpreted as hardware discovery.
import {normalize} from './telemetry';
export function emptySample(deviceId){return {deviceId,time:null,cpu:null,memory:null,memUsed:null,memTotal:null,cores:[],gpus:[],processes:[],volumes:[],hardware:null,network:null,storage:null,unavailable:true}}
export function connectionMessage(text=''){
 if(/MaxStart|identification string/i.test(text))return '服务器暂时无法接受新的 SSH 连接，正在稍后重试。';
 if(/强迫关闭|强行关闭|closed|reset|aborted|断开/i.test(text))return '与设备的连接已中断，正在重连。';
 if(/超时|timed? ?out/i.test(text))return '设备暂未响应，正在重连。';
 return text?'暂时无法获取设备数据，正在重连。':'';
}
export function mergeReading(old,raw,now=Date.now()){
 if(!raw)return old;
 const lastAttempt=old?.lastAttempt??-Infinity;if(raw.time<=lastAttempt)return old;
 const next=normalize(raw),previous=old?.sample||(!raw.lastGood?.error&&raw.lastGood?normalize(raw.lastGood):null);
 if(raw.error){
  const gap={time:raw.time,interval:raw.interval,cpu:null,memory:null,gpus:[],network:null,storage:null,gap:true};
  return {...old,sample:previous?{...previous,stale:true}:null,lastAttempt:raw.time,lastSuccess:previous?.time??null,error:connectionMessage(raw.error),stale:true,samples:[...(old?.samples||[]),gap].filter(s=>s.time>=now-7200000).slice(-3600)};
 }
 const sample={...next,hardware:next.hardware||previous?.hardware};
 const point={time:next.time,interval:next.interval,cpu:next.cpu,memory:next.memory,network:next.network,storage:next.storage,gpus:next.gpus.map(g=>({id:g.id,util:g.util})),volumes:next.volumes?.map(v=>({Path:v.Path,UsedBytes:v.UsedBytes,AvailableBytes:v.AvailableBytes,TotalBytes:v.TotalBytes,UsedPercent:v.UsedPercent}))};
 return {sample,lastAttempt:raw.time,lastSuccess:raw.time,error:'',stale:false,samples:[...(old?.samples||[]),point].filter(s=>s.time>=now-7200000).slice(-3600)};
}
