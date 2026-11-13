import {t,tr,useLocale,locale} from '../../i18n/index.js';
import React,{useEffect,useMemo,useRef,useState} from 'react';
import {SectionTitle} from '../../components';
import {axisTicks,coreBins,lowMemorySpans,finite,networkChanges,processes} from './analysis-data.mjs';

export const clock=(t,seconds=true)=>new Date(t).toLocaleTimeString(locale(),{hour:'2-digit',minute:'2-digit',...(seconds?{second:'2-digit'}:{}),hour12:false});
export const gib=v=>finite(v)?`${v.toFixed(2)} GiB`:'—';
const frac=(t,from,to)=>Math.max(0,Math.min(1,(t-from)/Math.max(1,to-from)));
export function TimeAxis({from,to,className='',style}){useLocale();
 const host=useRef(null),[width,setWidth]=useState(0);
 useEffect(()=>{const el=host.current,ro=new ResizeObserver(([entry])=>setWidth(entry.contentRect.width));ro.observe(el);return()=>ro.disconnect()},[]);
 return <div ref={host} className={'ev-follow-axis '+className} style={style}>{tr(axisTicks(from,to,width).map(({time,at},i)=><span key={i} style={{left:`${at*100}%`,'--ev-axis-shift':`${at===0?0:at===1?-100:-50}%`}}>{tr(clock(time,false))}</span>))}</div>;
}
export function CoreHistory({points,from,to,selectedTime,onPick,thinned}){useLocale();
 const {bins,ids}=useMemo(()=>coreBins(points,from,to),[points,from,to]);
 const current=points.find(p=>p.time===selectedTime);
 const active=selectedTime==null?-1:Math.min(bins.length-1,Math.floor(frac(selectedTime,from,to)*bins.length));
 const [selectedCore,setCore]=useState(null),[focus,setFocus]=useState(null),[gutter,setGutter]=useState(0),grid=useRef(null);
 useEffect(()=>{const el=grid.current;if(!el)return;const ro=new ResizeObserver(()=>setGutter(el.offsetWidth-el.clientWidth));ro.observe(el);return()=>ro.disconnect()},[ids.length]);
 const core=selectedCore??ids[0],coreValue=current?.coreValues?.[current?.cores?.indexOf(core)];
 const available=ids.flatMap((id,r)=>bins.flatMap((bin,c)=>bin.has(id)?[{r,c,id}]:[]));
 const focused=available.find(p=>p.id===focus?.id&&p.c===focus.c)??available.find(p=>p.id===core&&p.c===active)??available[0];
 function arrow(e,r,c){
  const directions={ArrowLeft:[0,-1],ArrowRight:[0,1],ArrowUp:[-1,0],ArrowDown:[1,0]};const dir=directions[e.key];if(!dir)return;
  e.preventDefault();let nr=r+dir[0],nc=c+dir[1];
  while(nr>=0&&nr<ids.length&&nc>=0&&nc<bins.length){const el=grid.current.querySelector(`[data-cell="${nr}-${nc}"]`);if(el){el.focus();return;}nr+=dir[0];nc+=dir[1];}
 }
 return <section className="ev-followup ev-core-history" aria-label={t("核心负载热图")}><SectionTitle meta={ids.length?`${ids.length} 个逻辑核心`:null}>{t("核心负载热图")}</SectionTitle>
 {tr(ids.length?<><div className="ev-follow-summary"><span>{tr(thinned?'每格取载入样本的峰值':'每格取该时段峰值')}</span><div className="ev-heat-key"><span>{t("低")}</span>{tr([0,1,2,3,4].map(i=><i key={i} data-level={i}/>))}<span>{t("高")}</span></div></div>
 <div className="ev-heat-scroll" ref={grid} role="group" aria-label={t("逐核历史峰值")} style={{'--ev-core-cols':bins.length}}>{tr(ids.map((id,r)=><div className="ev-heat-row" key={id}><span>CPU {tr(id)}</span><div className="ev-heat-track">{tr(bins.map((bin,c)=>{
  const p=bin.get(id);if(!p)return <span className="ev-heat-missing" key={c}/>;
  const label=`CPU ${id} · ${clock(p.time)} · ${p.value.toFixed(0)}%`;
  return <button key={c} type="button" data-cell={`${r}-${c}`} data-time={p.time} data-level={p.value>=85?4:p.value>=65?3:p.value>=40?2:p.value>=20?1:0} data-column={active===c} aria-label={tr(label)} title={tr(label)} aria-pressed={id===core&&p.time===selectedTime} tabIndex={focused?.r===r&&focused.c===c?0:-1} onFocus={()=>setFocus({id,c})} onKeyDown={e=>arrow(e,r,c)} onClick={()=>{setCore(id);onPick(p.time)}}/>;
 }))}</div></div>))}</div><TimeAxis from={from} to={to} className="ev-heat-axis" style={{'--ev-heat-gutter':`${gutter}px`}}/>
 <div className="ev-follow-foot" aria-live="polite">{tr(finite(coreValue)?<><b>CPU {tr(core)}</b><span>{tr(clock(selectedTime))} · {tr(coreValue.toFixed(0))}%</span></>:<span>{t("该时刻无逐核读数")}</span>)}</div></>:<p className="ev-follow-empty">{t("所选时段没有逐核采样。")}</p>)}</section>;
}
function trace(points,values,x,y){
 let open=false;const singleton=[];const path=points.map((p,i)=>{
  if(!finite(values[i])){open=false;return '';}if(p.gapBefore)open=false;
  const command=open?'L':'M';open=true;const px=x(p.time),py=y(values[i]);
  if(command==='M'&&(!finite(values[i+1])||points[i+1]?.gapBefore))singleton.push([px,py]);
  return `${command}${px} ${py}`;
 }).join(' ');
 return <><path className="ev-follow-line" d={path}/>{tr(singleton.map(([px,py],i)=><line key={i} className="ev-follow-line" x1={px} x2={px+.01} y1={py} y2={py}/>))}</>;
}
function band(x,prior,point,height){if(!prior||!point)return null;const a=x(prior.time),b=x(point.time);return <g className="ev-follow-band" data-start={prior.time} data-end={point.time}><rect x={a} width={b-a} height={height}/><line x1={a} x2={a} y2={height}/><line x1={b} x2={b} y2={height}/></g>;}
export function GpuHeadroom({points,from,to,values,prior,point,onRange}){useLocale();
 const spans=lowMemorySpans(points,values),known=values.filter(Boolean),minimum=Math.min(...known.map(v=>v.free));
 const x=t=>frac(t,from,to)*1000,y=v=>58-v*56;
 function edge(i,dir){const a=points[i],b=points[i+dir];return b?frac((a.time+b.time)/2,from,to):frac(a.time+dir*Math.max(1,a.interval)*500,from,to);}
 return <section className="ev-followup ev-headroom" aria-label={t("显存余量时间带")}><SectionTitle>{t("显存余量时间带")}</SectionTitle>
 {tr(known.length?<><div className="ev-follow-summary"><span>{t("时段最低 ")}<b>{tr(gib(minimum))}</b></span><span className="ev-threshold"><i/>{t("余量低于 20%")}</span></div>
 <div className="ev-free-track"><svg viewBox="0 0 1000 60" preserveAspectRatio="none" aria-hidden="true"><rect className="ev-free-threshold" y={y(.2)} width="1000" height={60-y(.2)}/>{tr(band(x,prior,point,60))}{tr(trace(points,values.map(v=>v?v.free/v.total:null),x,y))}</svg>
 {tr(spans.map(({start,end,min})=>{const a=points[start],b=points[end],left=edge(start,-1),right=edge(end,1);const label=`${clock(a.time)}${start!==end?'–'+clock(b.time):''} · 最低余量 ${gib(min)}`;
  return <button key={a.time} type="button" className="ev-free-span" title={tr(label)} aria-label={tr(label)} aria-pressed={prior?.time===a.time&&point?.time===b.time} onClick={()=>onRange(start,end)} style={{left:`${left*100}%`,width:`${(right-left)*100}%`}}/>;}))}</div><TimeAxis from={from} to={to}/>
 <div className="ev-follow-foot"><span>{tr(spans.length?'选择色带可对照该时段的显存变化。':'已记录样本中，余量均不低于 20%。')}</span></div></>:<p className="ev-follow-empty">{t("所选显卡在这段时间没有可用的显存记录。")}</p>)}</section>;
}
export function MemoryHistory({points,from,to,process,prior,point}){useLocale();
 const readings=useMemo(()=>{
  const output=[];let interrupted=false;
  for(const p of points){if(p.gapBefore)interrupted=true;if(!p.processes)continue;const value=processes('memory',p)?.find(r=>r.id===process?.id)?.value;output.push({...p,gapBefore:interrupted||p.time-(output.at(-1)?.time??p.time)>Math.max(25000,p.interval*2500),value});interrupted=false;}
  return output;
 },[points,process?.id]);
 const max=Math.max(.25,Math.ceil(Math.max(...readings.map(p=>p.value??0))*4)/4),x=t=>frac(t,from,to)*1000,y=v=>109-v/max*104;
 const reading=p=>readings.find(r=>r.time===p?.time)?.value;
 return <section className="ev-followup ev-process-history" aria-label={t("进程内存趋势")} data-process={process?.id}><SectionTitle meta={process?`PID ${process.pid}`:null}>{t("进程内存趋势")}</SectionTitle>
 {tr(!process?<p className="ev-follow-empty">{t("展开上方进程查看驻留内存历史。")}</p>:<><div className="ev-follow-summary"><b>{process.name}</b><span><i className="ev-memory-start"/>{t("起点 ")}{tr(gib(reading(prior)))}</span><span><i className="ev-memory-end"/>{t("终点 ")}{tr(gib(reading(point)))}</span></div><div className="ev-memory-plot"><div className="ev-memory-scale"><span>{tr(gib(max))}</span><span>0</span></div><div><svg viewBox="0 0 1000 112" preserveAspectRatio="none" role="img" aria-label={tr(`${process.name} 驻留内存历史`)}><line className="ev-follow-grid" x2="1000" y1="56" y2="56"/>{tr(band(x,prior,point,112))}{tr(trace(readings,readings.map(p=>p.value),x,y))}</svg><TimeAxis from={from} to={to}/></div></div></>)}</section>;
}
export function NetworkQuality({points,from,to,adapter,prior,point,onRange}){useLocale();
 const rows=useMemo(()=>{
  const width=(to-from)/24,bins=Array.from({length:24},()=>null);
  for(let i=1;i<points.length;i++){
   const part=networkChanges(points,i-1,i).rows.filter(r=>adapter==='all'||r.id===adapter);
   if(!part.length||part.every(r=>r.errors==null&&r.discards==null))continue;
   const bin=Math.max(0,Math.min(bins.length-1,Math.floor((points[i].time-from)/Math.max(1,width))));
   const row=bins[bin]??{start:i-1,end:i,errors:null,discards:null};row.end=i;
   for(const r of part){if(r.errors!=null)row.errors=(row.errors??0)+r.errors;if(r.discards!=null)row.discards=(row.discards??0)+r.discards;}
   bins[bin]=row;
  }return bins;
 },[points,from,to,adapter]);
 const max=Math.max(1,...rows.map(r=>(r?.errors??0)+(r?.discards??0)));
 return <section className="ev-followup" aria-label={t("网卡错误与丢弃")}><SectionTitle>{t("网卡错误与丢弃")}</SectionTitle>
 {tr(rows.some(Boolean)?<><div className="ev-follow-summary"><span>{t("本机或远端网卡计数 · 不代表端到端丢包率")}</span></div><div className="ev-net-quality">{tr(rows.map((r,i)=>{
  if(!r)return <span className="ev-heat-missing" key={i}/>;
  const label=`${clock(points[r.start].time)}–${clock(points[r.end].time)} · 错误 ${r.errors??'未记录'} · 丢弃 ${r.discards??'未记录'}`;
  const total=(r.errors??0)+(r.discards??0);
  return <button type="button" key={i} title={tr(label)} aria-label={tr(label)} aria-pressed={prior===points[r.start]&&point===points[r.end]} onClick={()=>onRange(r.start,r.end)}><i data-zero={total===0} style={{height:total?`${Math.max(4,total/max*100)}%`:1}}/></button>;
 }))}</div><TimeAxis from={from} to={to}/></>:<p className="ev-follow-empty">{t("所选网卡没有可用的错误或丢弃计数。")}</p>)}</section>;
}
