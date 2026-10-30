import React,{memo,useCallback,useEffect,useLayoutEffect,useRef,useState,useSyncExternalStore} from 'react';
import {Clock} from 'lucide-react';
import {gsap} from 'gsap';
import {Button,Chart,Meter,SectionTitle} from '../components';
import {ThemedSelect} from '../themed-select';
import {HistoryPeek} from '../history-peek';
import {ProcessTable} from '../pages/Resources';
import {orderedSamples} from '../time-series';
import {motionOptions,reducedMotion,subscribeReducedMotion} from '../motion';
import {native} from './bridge';
import {normalize,gpuAverage} from './telemetry';
import {HardwareInventory} from './ResourceWidgets';
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

export const HistoryView=memo(function HistoryView({deviceId}){
 const [minutes,setMinutes]=useState(60),[metric,setMetric]=useState('cpu'),[archive,setArchive]=useState(()=>prepareArchive([],60)),[total,setTotal]=useState(0),[current,setCurrent]=useState(null),[index,setIndex]=useState(null),[open,setOpen]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
 const generation=useRef(0),request=useRef(0),region=useRef(null),viewport=useRef(null),previous=useRef(null);
 const preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const query=useCallback(async()=>{
  const seq=++generation.current;request.current++;setBusy(true);setError('');
  try{const result=await native.request('history.query',{minutes,deviceId});if(seq!==generation.current)return;
   setArchive(prepareArchive(result.points,minutes));setTotal(result.total??result.points.length);setCurrent(null);setIndex(null);setOpen(false);request.current++;
  }catch(e){if(seq===generation.current)setError(e.message)}finally{if(seq===generation.current)setBusy(false)}
 },[minutes,deviceId]);
 useEffect(()=>{query();return()=>{generation.current++;request.current++}},[query]);
 const choose=useCallback(async(point,i)=>{
  const seq=++request.current;setError('');try{const value=await native.request('history.read',{key:point.id,deviceId});if(seq!==request.current)return;setCurrent(normalize(value));setIndex(i);setOpen(true)}catch(e){if(seq===request.current)setError(e.message)}
 },[deviceId]);
 const close=useCallback((focus=false)=>{setOpen(false);if(focus)region.current?.querySelector('.history-trend-panel:not([aria-hidden="true"]) .chart')?.focus({preventScroll:true})},[]);
 const changeMetric=id=>{if(id===metric)return;request.current++;setMetric(id);setCurrent(null);setIndex(null);setOpen(false)};
 useEffect(()=>{if(!open)return;const outside=e=>{if(!region.current?.contains(e.target))close()},escape=e=>{if(e.key==='Escape')close(true)};document.addEventListener('pointerdown',outside);document.addEventListener('keydown',escape);return()=>{document.removeEventListener('pointerdown',outside);document.removeEventListener('keydown',escape)}},[open,close]);
 // Only an explicit metric/range/archive change enters this effect. Live captures,
 // hovering and selecting a snapshot must never restart the history transition.
 useLayoutEffect(()=>{
  const node=viewport.current,old=previous.current;previous.current={metric,archive};
  if(preference||!old||!archive.points.length||(old.metric===metric&&old.archive===archive)){gsap.set(node,{clearProps:'opacity,transform'});return}
  const tween=gsap.fromTo(node,{opacity:.66,y:3},{opacity:1,y:0,...motionOptions('page'),clearProps:'opacity,transform'});
  return()=>{tween.kill();gsap.set(node,{clearProps:'opacity,transform'})};
 },[metric,archive,preference]);
 const value=current?(metric==='gpu'?gpuAverage(current):current[metric]):null,date=current?new Date(current.time):null;
 const popover=open&&current&&Number.isFinite(value)?<HistoryPeek date={date.toLocaleDateString('zh-CN',{month:'numeric',day:'numeric'})} time={date.toLocaleTimeString('zh-CN',{hour12:false})} metric={metricLabel(metric)} value={value} rows={current.processes} gpuCount={current.gpus.length} onClose={()=>close(true)} onViewProcesses={()=>{close();document.querySelector('.native-history-processes')?.scrollIntoView({behavior:preference?'instant':'smooth',block:'start'})}}/>:null;
 return <>
  <div className="page-heading"><div><div className="eyebrow">本地存档</div><h1>历史记录</h1><p>回到一个时间点，查看当时的资源与进程。</p></div><Button onClick={query} disabled={busy}>{busy?'正在读取…':'刷新存档'}</Button></div>
  <div className="history-toolbar"><label className="range-control"><Clock size={17}/><ThemedSelect aria-label="历史时间范围" value={minutes} onChange={e=>setMinutes(Number(e.target.value))}>{[[15,'最近 15 分钟'],[60,'最近 1 小时'],[120,'最近 2 小时'],[1440,'最近 24 小时']].map(([v,l])=><option key={v} value={v}>{l}</option>)}</ThemedSelect></label><span className="history-archive-state"><i className="status-dot"/>{total} 个已存档采样{total>archive.points.length?' · 显示 '+archive.points.length+' 个趋势点':''}</span></div>{error&&<p role="alert" className="native-error">{error}</p>}
  <section className="history-chart" ref={region}>
   <SectionTitle action={<div className="segmented history-metric-tabs" role="group" aria-label="历史资源指标" style={{'--history-selection':metrics.findIndex(([id])=>id===metric)}}><span className="history-metric-selection" aria-hidden="true"/>{metrics.map(([id,label])=><button key={id} type="button" className={metric===id?'active':''} aria-pressed={metric===id} onClick={()=>changeMetric(id)}>{label}</button>)}</div>}>利用率时间线</SectionTitle>
   <div className="history-trend-viewport" ref={viewport} aria-busy={busy} data-history-metric={metric} data-history-minutes={archive.minutes}>
    {metrics.map(([id])=><div key={id} className="history-trend-panel" aria-hidden={metric!==id} inert={metric!==id?'':undefined}><HistoryMetricChart archive={archive} metric={id} onSelect={choose} selected={metric===id?index:null} popover={metric===id?popover:null}/></div>)}
   </div>
   <div className="timeline-selection"><div><Clock size={16}/><span>选中时间</span><strong>{date?date.toLocaleString('zh-CN',{hour12:false}):'点击曲线查看完整快照'}</strong></div>{current&&<span>{current.cores.length} 线程 · {current.gpus.length} GPU</span>}</div>
  </section>
  {current&&<div className="native-history-processes"><ProcessTable device={{gpuCount:current.gpus.length}} processes={current.processes} readonly/><section className="history-memory"><SectionTitle>显存快照</SectionTitle><div className="memory-snapshot-grid">{current.gpus.map(g=><div key={g.id}><header><span>GPU {g.id}</span><strong>{g.used?.toFixed(2)??'—'} / {g.total?.toFixed(2)??'—'} GiB</strong></header>{g.used!=null&&g.total>0&&<Meter value={g.used/g.total*100}/>}</div>)}</div><HardwareInventory sample={current}/></section></div>}
 </>;
});
