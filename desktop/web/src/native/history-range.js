import {BoundedCache} from '../foundation/bounded-cache.js';

export const historyRanges=new BoundedCache({maxEntries:12,maxWeight:36000,weight:value=>Math.max(1,value.archive.points.length)});
const pad=value=>String(value).padStart(2,'0');
export function localMinute(time){const date=new Date(time);return `${date.getFullYear()}-${pad(date.getMonth()+1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`}
export function parseLocalMinute(value){if(!/^\d{4}-\d\d-\d\dT\d\d:\d\d$/.test(value||''))return NaN;const time=new Date(value).getTime();return Number.isFinite(time)&&localMinute(time)===value?time:NaN}
export function defaultHistoryRange(now=Date.now()){const customStart=localMinute(now-3600000),customEnd=localMinute(now);return {minutes:60,metric:'cpu',rangeMode:'preset',customStart,customEnd,customFrom:parseLocalMinute(customStart),customTo:parseLocalMinute(customEnd),result:null}}
export function validateHistoryRange(start,end,now=Date.now()){
 const fromUtc=parseLocalMinute(start),toUtc=parseLocalMinute(end);
 if(!Number.isFinite(fromUtc)||!Number.isFinite(toUtc))return {error:'请输入完整、有效的本地日期和时间。'};
 if(fromUtc<0||toUtc<=fromUtc)return {error:'结束时间必须晚于开始时间。'};
 if(toUtc>now+60000)return {error:'结束时间不能晚于当前时间。'};
 if(toUtc-fromUtc>366*86400000)return {error:'单次查询范围不能超过 366 天。'};
 return {fromUtc,toUtc};
}
export function historyRangeKey(view){return view.rangeMode==='custom'?`custom:${view.customFrom}:${view.customTo}`:`preset:${view.minutes}`}
export function historyQueryRange(view){return view.rangeMode==='custom'?{fromUtc:view.customFrom,toUtc:view.customTo}:{minutes:view.minutes}}

// Only inert date preferences cross renderer restarts; sampled data is never stored.
function historyPreferenceStorage(){try{return globalThis.localStorage}catch{return null}}
const preferenceKey='asterhub-history-ranges-v1';
function storedRanges(storage){try{const data=JSON.parse(storage?.getItem(preferenceKey)||'[]');return Array.isArray(data)?data.filter(row=>row&&typeof row.device==='string'&&row.device.length<=256&&row.value&&typeof row.value==='object').slice(-20):[]}catch{return []}}
export function readHistoryRange(deviceId,storage=historyPreferenceStorage()){
 const saved=storedRanges(storage).find(row=>row.device===String(deviceId))?.value;
 if(!saved||!['preset','custom'].includes(saved.rangeMode)||!Number.isFinite(saved.customFrom)||!Number.isFinite(saved.customTo)||saved.customFrom<0||saved.customTo<=saved.customFrom||saved.customTo-saved.customFrom>366*86400000)return {};
 return {rangeMode:saved.rangeMode,customStart:typeof saved.customStart==='string'&&saved.customStart.length<=32?saved.customStart:localMinute(saved.customFrom),customEnd:typeof saved.customEnd==='string'&&saved.customEnd.length<=32?saved.customEnd:localMinute(saved.customTo),customFrom:saved.customFrom,customTo:saved.customTo};
}
export function saveHistoryRange(deviceId,view,storage=historyPreferenceStorage()){try{const rows=storedRanges(storage).filter(row=>row.device!==String(deviceId));rows.push({device:String(deviceId),value:{rangeMode:view.rangeMode,customStart:view.customStart,customEnd:view.customEnd,customFrom:view.customFrom,customTo:view.customTo}});storage?.setItem(preferenceKey,JSON.stringify(rows.slice(-20)))}catch{}}
