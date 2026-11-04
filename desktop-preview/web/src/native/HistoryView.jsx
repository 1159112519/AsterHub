import {useWorkspaceState} from '../foundation/use-workspace-state.js';
import {isWorkspaceResultFresh} from '../foundation/workspace-state.js';
import {BoundedCache} from '../foundation/bounded-cache.js';
import {createRequestScope} from '../foundation/request-scope.js';
import React,{memo,useCallback,useEffect,useLayoutEffect,useRef,useState,useSyncExternalStore,useMemo} from 'react';
import {Clock} from 'lucide-react';
import {HistoryDateTime} from './HistoryDateTime';
import {defaultHistoryRange,historyQueryRange,historyRangeKey,historyRanges,parseLocalMinute,readHistoryRange,saveHistoryRange,validateHistoryRange} from './history-range';
import {gsap} from 'gsap';
import {Button,Chart,Meter,SectionTitle} from '../components';
import {ThemedSelect} from '../themed-select';
import {HistoryPeek} from '../history-peek';
import {ProcessTable} from '../pages/Resources';
import {orderedSamples} from '../time-series';
import {motionOptions,reducedMotion,subscribeReducedMotion} from '../motion';
import {native} from './bridge';
import {normalize,gpuAverage} from './telemetry';
import './history-preview8.css';

const metrics=[['cpu','CPU','var(--blue)'],['gpu','GPU','var(--green)'],['memory','内存','var(--purple)']];
const metricLabel=id=>metrics.find(([key])=>key===id)[1];

// The archive already contains aggregate GPU readings. Do not turn each archived
// number into a synthetic GPU object and rescan it when changing the metric.
function prepareArchive(rows,minutes){
 const points=orderedSamples(rows),timestamps=points.map(p=>p.time);
 const series=Object.fromEntries(metrics.map(([id,name,color])=>[id,[{name,color,values:points.map(p=>p[id])}]]));
 return {points,timestamps,series,minutes,breakBefore:points.some(p=>'gapBefore' in p)?points.map(p=>!!p.gapBefore):null,interval:points.find(p=>p.interval)?.interval};
}

// Keep measured metric charts mounted. A metric click reuses exact SVG geometry
// instead of rebuilding thousands of cubic segments or measuring hidden layouts.
const HistoryMetricChart=memo(function HistoryMetricChart({archive,metric,onSelect,selected,popover}){
 const pick=useCallback(i=>{if(Number.isFinite(archive.series[metric][0].values[i]))onSelect(archive.points[i],i)},[archive,metric,onSelect]);
 return <Chart series={archive.series[metric]} timestamps={archive.timestamps} height={240} scalable rangeMinutes={archive.minutes} live={false} breakBefore={archive.breakBefore} expectedInterval={archive.interval} onSelect={pick} selected={selected} selectionPopover={popover}/>;
});

export const HistoryView=memo(function HistoryView({deviceId}){return <HistoryWorkspace key={deviceId} deviceId={deviceId}/>});
const HistoryWorkspace=memo(function HistoryWorkspace({deviceId}){
 const [view,setView]=useWorkspaceState(deviceId,'history',()=>({...defaultHistoryRange(),...readHistoryRange(deviceId)}));
 const {minutes,metric,result}=view,rangeKey=historyRangeKey(view),custom=view.rangeMode==='custom',rangeMinutes=custom?(view.customTo-view.customFrom)/60000:minutes;
 const emptyArchive=useMemo(()=>prepareArchive([],rangeMinutes),[rangeMinutes]),archive=result?.archive||emptyArchive,total=result?.total||0;
 useEffect(()=>saveHistoryRange(deviceId,view),[deviceId,view.rangeMode,view.customStart,view.customEnd,view.customFrom,view.customTo]);
 const queryParams=useMemo(()=>historyQueryRange(view),[view.rangeMode,minutes,view.customFrom,view.customTo]);
 const [rangeError,setRangeError]=useState('');
 const unapplied=custom&&(parseLocalMinute(view.customStart)!==view.customFrom||parseLocalMinute(view.customEnd)!==view.customTo);
 const setMinutes=value=>{setRangeError('');setView(v=>value==='custom'?{...v,rangeMode:'custom'}:{...v,rangeMode:'preset',minutes:Number(value)})},setMetric=value=>setView(v=>({...v,metric:value}));
 const [recorder,setRecorder]=useState(null),[current,setCurrent]=useState(null),[index,setIndex]=useState(null),[open,setOpen]=useState(false),[reading,setReading]=useState(false),[fullDetails,setFullDetails]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const cache=useRef(null),requests=useRef(null);if(!cache.current)cache.current=new BoundedCache({maxEntries:64});if(!requests.current)requests.current=createRequestScope();const generation=useRef(0),request=useRef(0),region=useRef(null),viewport=useRef(null),previous=useRef(null);
 const preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 useEffect(()=>{const controller=new AbortController();let alive=true;setRecorder(null);native.request('recorder.status',{deviceId},{signal:controller.signal,timeout:25000}).then(value=>{if(alive)setRecorder(value)}).catch(()=>{});return()=>{alive=false;controller.abort()}},[deviceId]);
 const query=useCallback(async()=>{
  const seq=++generation.current,ticket=requests.current.begin('range');requests.current.cancel('detail');request.current++;cache.current.clear();setOpen(false);setCurrent(null);setIndex(null);setReading(false);setFullDetails(false);setBusy(true);setError('');
  const queryId=Date.now().toString(36)+'-'+seq+'-'+Math.random().toString(36).slice(2,8);
  const cancel=()=>native.request('history.cancel',{deviceId,queryId},3000).catch(()=>{});ticket.signal.addEventListener('abort',cancel,{once:true});
  try{const result=await native.request('history.query',{...queryParams,deviceId,queryId},{signal:ticket.signal,timeout:50000});if(seq!==generation.current||!ticket.isCurrent())return;
   const loaded={key:rangeKey,fetchedAt:Date.now(),archive:prepareArchive(result.points,rangeMinutes),total:result.total??result.points.length};historyRanges.set(deviceId+':'+rangeKey,loaded);setView(v=>({...v,result:loaded}));setCurrent(null);setIndex(null);setOpen(false);request.current++;
  }catch(e){if(seq===generation.current&&e.name!=='AbortError')setError(e.message)}finally{if(seq===generation.current&&ticket.isCurrent())setBusy(false);ticket.signal.removeEventListener('abort',cancel);ticket.finish()}
 },[queryParams,rangeKey,rangeMinutes,deviceId,setView]);
 useEffect(()=>{const cached=historyRanges.get(deviceId+':'+rangeKey);if(!isWorkspaceResultFresh(result,rangeKey)){if(isWorkspaceResultFresh(cached,rangeKey)){setView(v=>({...v,result:cached}));setBusy(false);setError('');setCurrent(null);setIndex(null);setOpen(false)}else query()}return()=>{generation.current++;request.current++;requests.current.cancelAll();cache.current.clear()}},[query]);
 const choose=useCallback(async(point,i)=>{
  const ticket=requests.current.begin('detail'),seq=++request.current,key=deviceId+':'+point.id,cached=cache.current.get(key);setError('');setIndex(i);setOpen(true);setFullDetails(false);setCurrent(cached||null);setReading(!cached);if(cached){ticket.finish();return}
  try{const value=await native.request('history.read',{key:point.id,deviceId},{signal:ticket.signal});if(seq!==request.current||!ticket.isCurrent())return;const sample=normalize(value);cache.current.set(key,sample);setCurrent(sample)}catch(e){if(seq===request.current&&e.name!=='AbortError')setError(e.message)}finally{if(seq===request.current&&ticket.isCurrent())setReading(false);ticket.finish()}
 },[deviceId]);
 const close=useCallback((focus=false)=>{setOpen(false);if(focus)region.current?.querySelector('.history-trend-panel:not([aria-hidden="true"]) .chart')?.focus({preventScroll:true})},[]);
 const changeMetric=id=>{if(id===metric)return;requests.current.cancel('detail');request.current++;setMetric(id);setCurrent(null);setIndex(null);setOpen(false);setReading(false);setFullDetails(false)};
 useEffect(()=>{if(!open)return;const outside=e=>{if(!region.current?.contains(e.target))close()},escape=e=>{if(e.key==='Escape')close(true)};document.addEventListener('pointerdown',outside);document.addEventListener('keydown',escape);return()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',escape)}},[open,close]);
 // Only an explicit metric/range/archive change enters this effect. Live captures,
 // hovering and selecting a snapshot must never restart the history transition.
 useLayoutEffect(()=>{
  const node=viewport.current,old=previous.current;previous.current={metric,archive};
  if(preference||!old||!archive.points.length||(old.metric===metric&&old.archive===archive)){gsap.set(node,{clearProps:'opacity,transform'});return}
  const tween=gsap.fromTo(node,{opacity:.66,y:3},{opacity:1,y:0,...motionOptions('page'),clearProps:'opacity,transform'});
  return()=>{tween.kill();gsap.set(node,{clearProps:'opacity,transform'})};
 },[metric,archive,preference]);
 const selectedPoint=index==null?null:archive.points[index],value=current?(metric==='gpu'?gpuAverage(current):current[metric]):selectedPoint?.[metric],date=selectedPoint?new Date(selectedPoint.time):null;
 const popover=open&&date&&Number.isFinite(value)?<HistoryPeek date={date.toLocaleDateString('zh-CN',{month:'numeric',day:'numeric'})} time={date.toLocaleTimeString('zh-CN',{hour12:false})} metric={metricLabel(metric)} value={value} loading={reading} error={!reading&&!current?error:''} rows={current?.processes||[]} gpuCount={current?.gpus.length||0} onClose={()=>close(true)} onViewProcesses={()=>{setFullDetails(true);close();requestAnimationFrame(()=>document.querySelector('.native-history-processes')?.scrollIntoView({behavior:preference?'instant':'smooth',block:'start'}))}}/>:null;
 return <>
  <div className="page-heading"><div><div className="eyebrow">采样存档</div><h1>历史记录</h1><p>{recorder?.installed?(recorder.healthy?'主机持续记录 · ':'主机记录已暂停 · ')+'最近记录 '+new Date(recorder.lastTime).toLocaleString('zh-CN',{hour12:false}):'回到一个时间点，查看当时的资源与进程。'}</p></div><Button onClick={query} disabled={busy}>{busy?'正在读取…':'刷新存档'}</Button></div>
  <div className="history-toolbar"><div className="history-range-controls"><label className="range-control"><Clock size={17}/><ThemedSelect aria-label="历史时间范围" value={custom?'custom':minutes} onChange={event=>setMinutes(event.target.value)}>{[[15,'最近 15 分钟'],[60,'最近 1 小时'],[120,'最近 2 小时'],[1440,'最近 24 小时'],['custom','自定义时间']].map(([value,label])=><option key={value} value={value}>{label}</option>)}</ThemedSelect></label>{busy&&<span className="history-query-pending" role="status">正在读取真实存档…</span>}</div><span className="history-archive-state"><i className="status-dot"/>{total} 个已存档采样{total>archive.points.length?' · 显示 '+archive.points.length+' 个趋势点':''}</span></div>
  {custom&&<div className="history-custom-range"><HistoryDateTime label="开始时间" value={view.customStart} onChange={value=>{setRangeError('');setView(v=>({...v,customStart:value}))}}/><HistoryDateTime label="结束时间" value={view.customEnd} onChange={value=>{setRangeError('');setView(v=>({...v,customEnd:value}))}}/><Button onClick={()=>{const range=validateHistoryRange(view.customStart,view.customEnd);if(range.error){setRangeError(range.error);return}setRangeError('');setView(v=>({...v,customFrom:range.fromUtc,customTo:range.toUtc}))}}>应用时间</Button><p role={rangeError?'alert':undefined}>{rangeError||(unapplied?'时间已修改，点击“应用时间”后更新曲线。按本机时区查询。':'按本机时区查询；日期和时间可直接输入，也可通过日历选择。')}</p></div>}
  {error&&<p role="alert" className="native-error">{error}{result&&' · 仍显示上次成功读取的趋势'}</p>}
  <section className="history-chart" ref={region}>
   <SectionTitle action={<div className="segmented history-metric-tabs" role="group" aria-label="历史资源指标" style={{'--history-selection':metrics.findIndex(([id])=>id===metric)}}><span className="history-metric-selection" aria-hidden="true"/>{metrics.map(([id,label])=><button key={id} type="button" className={metric===id?'active':''} aria-pressed={metric===id} onClick={()=>changeMetric(id)}>{label}</button>)}</div>}>利用率时间线</SectionTitle>
   <div className="history-trend-viewport" ref={viewport} aria-busy={busy} inert={busy?'':undefined} data-history-metric={metric} data-history-minutes={archive.minutes}>
    {metrics.map(([id])=><div key={id} className="history-trend-panel" aria-hidden={metric!==id} inert={metric!==id?'':undefined}><HistoryMetricChart archive={archive} metric={id} onSelect={choose} selected={metric===id?index:null} popover={metric===id?popover:null}/></div>)}
   </div>
   <div className="timeline-selection"><div><Clock size={16}/><span>选中时间</span><strong>{date?date.toLocaleString('zh-CN',{hour12:false}):'点击曲线查看完整快照'}</strong></div>{current&&<span>{current.cores.length} 线程 · {current.gpus.length} GPU</span>}</div>
  </section>
  {fullDetails&&current&&<div className="native-history-processes"><ProcessTable device={{gpuCount:current.gpus.length}} processes={current.processes} readonly/><section className="history-memory"><SectionTitle>显存快照</SectionTitle><div className="memory-snapshot-grid">{current.gpus.map(g=><div key={g.id}><header><span>GPU {g.id}</span><strong>{g.used?.toFixed(2)??'—'} / {g.total?.toFixed(2)??'—'} GiB</strong></header>{g.used!=null&&g.total>0&&<Meter value={g.used/g.total*100}/>}</div>)}</div></section></div>}
 </>;
});
