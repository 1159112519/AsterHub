import {useSyncExternalStore} from 'react';
import english from './en.json';
let language='zh-CN';
try{if(localStorage.getItem('asterhub-language')==='en')language='en'}catch{}
const listeners=new Set();
export const normalizeLanguage=value=>value==='en'?'en':'zh-CN';
export const locale=()=>language==='en'?'en-US':'zh-CN';
export const getLanguage=()=>language;
export function setLanguage(value){const next=normalizeLanguage(value);document.documentElement.lang=next==='en'?'en':'zh-CN';document.documentElement.dataset.language=next;try{localStorage.setItem('asterhub-language',next)}catch{}if(language===next)return;language=next;for(const listener of [...listeners])listener()}
const subscribe=listener=>{listeners.add(listener);return()=>listeners.delete(listener)};
export function useLocale(){return useSyncExternalStore(subscribe,getLanguage,getLanguage)}
const groups=new Map();
for(const [source,target] of Object.entries(english)){
 if(!/\{\d+\}/.test(source))continue;
 const ids=[];let at=0,pattern='';for(const match of source.matchAll(/\{(\d+)\}/g)){pattern+=source.slice(at,match.index).replace(/[.*+?^${}()|[\]\\]/g,'\\$&')+'([\\s\\S]*?)';ids.push(match[1]);at=match.index+match[0].length}pattern+=source.slice(at).replace(/[.*+?^${}()|[\]\\]/g,'\\$&');const prefix=source.slice(0,source.indexOf('{'));const key=prefix?prefix[0]:'*';if(!groups.has(key))groups.set(key,[]);groups.get(key).push({source,target,ids,prefix,regex:new RegExp('^'+pattern+'$')});
}
for(const group of groups.values())group.sort((a,b)=>b.source.length-a.source.length);
export function t(source,values){if(typeof source!=='string')return source;let text=language==='en'?tr(source):source;if(values)text=text.replace(/\{(\d+)\}/g,(whole,id)=>values[id]??whole);return text}
export function tr(value){
 if(Array.isArray(value))return value.map(tr);
 if(language!=='en'||typeof value!=='string'||!/[\u3400-\u9fff]/.test(value))return value;
 if(Object.hasOwn(english,value))return english[value];
 const trimmed=value.trim();if(Object.hasOwn(english,trimmed))return value.slice(0,value.indexOf(trimmed))+english[trimmed]+value.slice(value.indexOf(trimmed)+trimmed.length);
 for(const entry of [...(groups.get(value[0])||[]),...(groups.get('*')||[])]){if(entry.prefix&&!value.startsWith(entry.prefix))continue;const match=entry.regex.exec(value);if(match){const args={};entry.ids.forEach((id,i)=>args[id]=match[i+1]);return entry.target.replace(/\{(\d+)\}/g,(whole,id)=>args[id]??whole)}}
 if(globalThis.__ASTER_I18N_AUDIT){const missing=globalThis.__ASTER_I18N_MISSING??=new Set();if(missing.size<512)missing.add(value)}
 return value;
}
