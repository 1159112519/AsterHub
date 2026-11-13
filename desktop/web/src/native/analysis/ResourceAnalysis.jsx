import {t,tr,useLocale} from '../../i18n/index.js';
import React,{useEffect,useMemo,useRef,useState} from 'react';
import {ChevronDown} from 'lucide-react';
import {Button,Chart,SectionTitle} from '../../components';
import {ProcessPath} from '../../ProcessPath';
import {ToolNavigation} from '../../foundation/ToolNavigation';
import {useWorkspaceState} from '../../foundation/use-workspace-state';
import {ThemedSelect} from '../../themed-select';
import {native} from '../bridge';
import {cardKey,finite,formatTraffic,gpuAggregate,gpuValues,memoryGiB,memoryProcessEndpoints,nearestIndex,networkChanges,processes,processSampleIndex,rankChanges,shiftRange,trafficScale} from './analysis-data.mjs';
import {CoreHistory,GpuHeadroom,MemoryHistory,NetworkQuality,clock,gib} from './AnalysisDetails';
import {CompareRange} from './CompareRange';
import {createAnalysisQuery} from './analysis-query.mjs';
import './analysis.css';

const titles={cpu:['占用分析','CPU 利用率','进程占用'],gpu:['显存分析','显存占用','显存变化排行'],memory:['内存分析','内存使用','内存变化排行'],network:['流量分析','网络速率','网卡流量排行']};
const colors={cpu:'var(--blue)',gpu:'var(--chart-gpu,var(--green))',memory:'var(--icon-pink)',network:'var(--blue)'};
const windows=[[15,'近 15 分钟'],[30,'近 30 分钟'],[60,'近 1 小时'],[180,'近 3 小时']];
const percent=n=>finite(n)?`${n.toFixed(1)}%`:'—';
const volume=formatTraffic;
const signed=n=>!finite(n)?'—':`${n>0?'+':n<0?'−':''}${gib(Math.abs(n))}`;
const timeLabel=(prior,point)=>point?(prior?`${clock(prior.time)}–${clock(point.time)}`:clock(point.time)):'—';

export function AnalysisWorkspace({metric,deviceId,children}){useLocale();
 const [view,setView]=useWorkspaceState(deviceId,`${metric}-resource-tools`,()=>({tab:'monitor'}));
 return <div className="ev-analysis-workspace"><ToolNavigation label={tr(`${titles[metric][0]}工具`)} items={[["monitor","运行监测"],["analysis",titles[metric][0]]]} value={view.tab} onChange={tab=>setView({tab})}>{tr(view.tab==='analysis'?<ResourceAnalysis metric={metric} deviceId={deviceId} key={`${deviceId}:${metric}`}/>:children)}</ToolNavigation></div>;
}

function Trend({series,points,selected,onSelect,unit,maxValue,from,to,markers,onMove}){useLocale();
 const frame=useRef(null),[height,setHeight]=useState(264);
 useEffect(()=>{const host=frame.current,ro=new ResizeObserver(([entry])=>setHeight(Math.max(196,Math.floor(entry.contentRect.height))));ro.observe(host);return()=>ro.disconnect()},[]);
 const times=useMemo(()=>points.map(p=>p.time),[points]),breaks=useMemo(()=>points.map(p=>p.gapBefore),[points]);
 return <div ref={frame} className="ev-traffic-frame"><Chart series={series} height={height} timestamps={times} timeDomain={[from,to]} breakBefore={breaks} unit={unit} maxValue={maxValue} selected={selected} onSelect={onSelect} calendarAxis renderOverlay={markers.length?geometry=><CompareRange {...geometry} markers={markers} onChange={onMove}/>:undefined}/></div>;
}
function ReadingRow({row,bar,value,children,onToggle,open,showGpu}){useLocale();

 const contents=<><span className="ev-app-info"><span className="ev-app-title"><strong title={row.name}>{row.name}</strong>{showGpu&&row.gpu!=null&&<span className="ev-gpu-tag">GPU {row.gpu}</span>}</span>{row.pid!=null&&<small>PID {row.pid}</small>}{row.path?<ProcessPath as="small" process={row}/>:row.pid!=null?<small>{t('路径未记录')}</small>:null}</span><span className="ev-app-bar" aria-hidden="true"><i style={{width:`${bar}%`}}/></span><span className="ev-app-number">{value}</span>{onToggle&&<ChevronDown size={14} aria-hidden="true"/>}</>;
 return onToggle?<><button type="button" className="ev-app" aria-label={row.name} aria-expanded={open} onClick={onToggle}>{tr(contents)}</button>{tr(open&&children)}</>:<div className="ev-app ev-app-static">{tr(contents)}</div>;
}
function EndpointRows({markers,row}){useLocale();
 return <dl className="ev-connections ev-enter" aria-label={tr(`${row.name} 的明细`)}>{tr(markers.map(marker=><div key={marker.key}><dt className="ev-time-key" data-ev-key={marker.key} data-time={marker.time} style={{'--ev-marker':marker.color}}><i className="ev-time-dot" aria-hidden="true"/><span>{tr(clock(marker.time))}（{tr(marker.label)}）</span></dt><dd>{tr(gib(marker.key==='start'?row.before:row.value))}</dd></div>))}</dl>;
}
const netRate=(p,adapter,field)=>{
 const numbers=p.adapters?.filter(a=>adapter==='all'||a.Id===adapter).map(a=>a[field]).filter(finite);
 return numbers?.length?numbers.reduce((sum,n)=>sum+n,0):null;
};
const noPoints=[];
const analysisQuery=createAnalysisQuery(params=>native.request('analysis.query',params,{timeout:26000}));
export function ResourceAnalysis({metric,deviceId}){useLocale();
 const cpu=metric==='cpu',gpu=metric==='gpu',net=metric==='network',memory=metric==='memory';
 const [title,trendTitle,rankTitle]=titles[metric],color=colors[metric];
 const [range,setRange]=useState(30),[duration,setDuration]=useState(2),[epoch,refresh]=useState(0),[error,setError]=useState('');
 const [selected,setSelected]=useState(null),[manualStart,setManualStart]=useState(null),[band,setBand]=useState(null),[expanded,setExpanded]=useState(null),[card,setCard]=useState(null),[adapter,setAdapter]=useState('all');
 const list=useRef(null),appliedRefresh=useRef(0);
 const cacheKey=JSON.stringify([deviceId,metric,range]);
 const [result,setResult]=useState(()=>({key:cacheKey,snapshot:analysisQuery.peek(cacheKey)?.value??null}));
 const [busy,setBusy]=useState(()=>!result.snapshot);
 const snapshot=result.key===cacheKey?result.snapshot:analysisQuery.peek(cacheKey)?.value;
 useEffect(()=>{
  let active=true;
  const force=appliedRefresh.current!==epoch;appliedRefresh.current=epoch;
  const cached=analysisQuery.peek(cacheKey);
  setBusy(force||!cached);setError('');
  analysisQuery.query(cacheKey,{deviceId,metric,minutes:range},{force}).then(value=>{
   if(active){setResult({key:cacheKey,snapshot:value});setBusy(false);}
  },err=>{if(active){setError(err.message);setBusy(false);}});
  return()=>{active=false;};
 },[cacheKey,deviceId,metric,range,epoch]);
 const points=snapshot?.points??noPoints;
 const cards=useMemo(()=>{const map=new Map();for(const p of points)for(const g of p.gpus??[])map.set(cardKey(g),g);return [...map.entries()];},[points]);
 const adapters=useMemo(()=>{const map=new Map();for(const p of points)for(const n of p.adapters??[])map.set(n.Id,n.Name||n.Id);return [...map.entries()];},[points]);
 const cardId=card==='all'||cards.some(([key])=>key===card)?card:cards[0]?.[0];
 const adapterId=adapters.some(([key])=>key===adapter)?adapter:'all';
  const gpuIds=useMemo(()=>{
  const last=points.findLast(p=>p.gpus?.length);
  return (last?.gpus??[]).map(cardKey);
 },[points]);
 const gpuData=useMemo(()=>gpu?points.map(p=>cardId==='all'?gpuAggregate(p,gpuIds):gpuValues(p,cardId)):[],[gpu,points,cardId,gpuIds]);
 const rates=useMemo(()=>net?[points.map(p=>netRate(p,adapterId,'DownloadBytesPerSecond')),points.map(p=>netRate(p,adapterId,'UploadBytesPerSecond'))]:[],[net,points,adapterId]);
 const rateScale=useMemo(()=>trafficScale(Math.max(0,...rates.flat().filter(finite))),[rates]);
 const memoryAbsolute=memory&&points.some(p=>memoryGiB(p)!=null);
 const series=useMemo(()=>{
  if(net)return rates.map((values,i)=>({name:i?'上传':'下载',color:i?'var(--purple)':'var(--blue)',values:values.map(v=>finite(v)?v/rateScale.divisor:null)}));
  return [{name:trendTitle,color,values:points.map((p,i)=>cpu?p.cpu:gpu?gpuData[i]?.used??null:memoryAbsolute?memoryGiB(p):p.memory)}];
 },[net,points,rates,rateScale,cpu,gpu,gpuData,memoryAbsolute,color,trendTitle]);
 const values=series[0].values,selectable=i=>series.some(s=>finite(s.values[i]));
 const latest=points.findLastIndex((p,i)=>selectable(i));
 const processSample=cpu||memory?points.findLastIndex((p,i)=>selectable(i)&&processes(metric,p,cardId)?.length>0):-1;
 const defaultEnd=processSample<0?latest:processSample;
 const firstSelectable=series[0].values.findIndex((_,i)=>selectable(i));
 const end=selected==null?defaultEnd:nearestIndex(points,selected,(_,i)=>selectable(i)&&(!band||i>firstSelectable));
 const point=points[end];
 const preferProcess=memory&&Boolean(point?.processes);
 const before=target=>{
  const index=points.findIndex(p=>p===target);if(index<1)return -1;
  const desired=target.time-duration*60000;
  const matching=nearestIndex(points,desired,(p,i)=>i<index&&selectable(i)&&(!preferProcess||Boolean(p.processes)));
  return matching<0?nearestIndex(points,desired,(_,i)=>i<index&&selectable(i)):matching;
 };
 const start=cpu||!point?-1:manualStart==null?before(point):nearestIndex(points,manualStart,(_,i)=>i<end&&selectable(i));
 const prior=points[start];
 const current=end>=0&&end===latest&&snapshot.to-point.time<=Math.max(15000,point.interval*3000);
 const markers=point?[...(prior?[{key:'start',time:prior.time,displayTime:band?.[0],index:start,label:'起点',color:'var(--amber)'}]:[]),...(prior||cpu?[{key:'end',time:point.time,displayTime:band?.[1],index:end,label:current?'当前':'终点',color:'var(--blue)'}]:[])]:[];
 const unit=cpu||memory&&!memoryAbsolute?'%':net?rateScale.unit+'/s':'GiB';
 const max=unit==='%'?100:Math.max(unit==='GiB'?1:.1,...(gpu?gpuData.map(v=>v?.total??0):series.flatMap(s=>s.values.filter(finite))));
 const cpuSampleIndex=useMemo(()=>cpu?processSampleIndex(points,end,'cpu'):-1,[cpu,points,end]);
 const cpuPoint=cpuSampleIndex>=0?points[cpuSampleIndex]:null;
 const cpuRows=cpu?(processes(metric,cpuPoint,cardId)??[]).sort((a,b)=>b.value-a.value):[];
 const comparison=useMemo(()=>net?networkChanges(points,start,end):null,[net,points,start,end]);
 const processEndpoints=useMemo(()=>memory?memoryProcessEndpoints(points,start,end):null,[memory,points,start,end]);
 const processPrior=memory?(processEndpoints?points[processEndpoints[0]]:null):prior;
 const processPoint=memory?(processEndpoints?points[processEndpoints[1]]:null):point;
 const processMarkers=memory&&processPrior&&processPoint?markers.map((marker,i)=>({...marker,time:(i?processPoint:processPrior).time,label:i&&processPoint.time!==point.time?'终点':marker.label})):markers;
 const rows=cpu?cpuRows:net?comparison.rows.filter(r=>adapterId==='all'||r.id===adapterId):rankChanges(processes(metric,processPrior,cardId),processes(metric,processPoint,cardId)).filter(row=>!gpu||cardId!=='all'||gpuIds.some(id=>row.cardId===id));
 const process=memory&&expanded?rows.find(row=>row.id===expanded.id):null;
 const amount=r=>cpu?r.value:net?r.down+r.up:Math.abs(r.change),magnitude=Math.max(.0001,...rows.map(amount));
 const format=unit==='%'?percent:gib,change=finite(values[end])&&finite(values[start])?values[end]-values[start]:null;
 const totals=cpuRows.length?cpuRows.reduce((sum,row)=>sum+row.value,0):null;
 const summary=cpu?[['总利用率',percent(cpuPoint?.cpu??point?.cpu)],['已列进程',percent(totals)]]:net?[['下载',rows.length?volume(rows.reduce((sum,r)=>sum+r.down,0)):'—'],['上传',rows.length?volume(rows.reduce((sum,r)=>sum+r.up,0)):'—']]:[['起点占用',format(values[start])],[current?'当前占用':'终点占用',format(values[end])],[gpu?'显存增减':'内存增减',unit==='%'?(finite(change)?`${change>0?'+':''}${change.toFixed(1)} 个百分点`:'—'):signed(change)]];
 const emptyRows=cpu?(deviceId==='local'?'该时刻没有进程 CPU 样本。':'此设备未记录进程 CPU 历史。'):net?'区间内无可比较的网卡计数。':gpu?'两个端点没有可匹配的进程显存样本。':deviceId==='local'?'两个端点没有可匹配的进程内存样本。':'此设备未记录进程内存历史。';
 function pick(time){setBand(null);setSelected(time);setManualStart(null);if(!memory)setExpanded(null);list.current?.scrollTo(0,0);}
 function setWindow(a,b){
  setBand(null);
  if(a===b){let preceding=a-1;while(preceding>=0&&!selectable(preceding))preceding--;if(preceding>=0&&!points[b].gapBefore)a=preceding;else{let following=b+1;while(following<points.length&&!selectable(following))following++;if(following<points.length&&!points[following].gapBefore)b=following;}}
  if(a===b){pick(points[b].time);return;}
  if(!points[a]||!points[b]||a>b)return;setManualStart(points[a].time);setSelected(points[b].time);if(!memory)setExpanded(null);
 }
 function move(key,next){
  if(key==='band'){
   const first=firstSelectable;if(first<0||latest<=first)return;
   const target=shiftRange(next.start,next.end,next.delta,points[first].time,points[latest].time);
   setBand(target);setManualStart(target[0]);setSelected(target[1]);return;
  }
  const eligible=nearestIndex(points,points[next]?.time,(_,i)=>selectable(i)&&(cpu||(key==='start'?i<end:i>start)));
  if(eligible>=0){setBand(null);if(cpu)pick(points[eligible].time);else if(key==='start')setManualStart(points[eligible].time);else{setManualStart(prior.time);setSelected(points[eligible].time);}}
 }
 const peak=useMemo(()=>points.reduce((best,p,i)=>finite(values[i])&&(best<0||values[i]>values[best])?i:best,-1),[points,values]);
 return <section className="ev-analysis ev-traffic ev-resource-analysis" data-resource-analysis={metric} style={{'--ev-series':color}} aria-busy={busy}>
  <div className="ev-traffic-head"><h2>{tr(title)}</h2><div className="ev-analysis-controls">
   {tr(gpu&&cards.length>1&&<ThemedSelect aria-label={t("分析显卡")} menuMinWidth={138} compact value={cardId} onChange={e=>{setCard(e.target.value);pick(null)}}><option value="all">{t("全部 GPU")}</option>{tr(cards.map(([key,g])=><option key={key} value={key}>GPU {tr(g.Index)}</option>))}</ThemedSelect>)}
   {tr(net&&adapters.length>1&&<ThemedSelect aria-label={t("分析网卡")} compact value={adapterId} onChange={e=>{setAdapter(e.target.value);pick(null)}}><option value="all">{t("全部网卡")}</option>{tr(adapters.map(([key,name])=><option key={key} value={key}>{name}</option>))}</ThemedSelect>)}
   <ThemedSelect aria-label={t("趋势时段")} menuMinWidth={128} compact value={String(range)} onChange={e=>{setRange(Number(e.target.value));pick(null)}}>{tr(windows.map(([v,label])=><option key={v} value={v}>{tr(label)}</option>))}</ThemedSelect>
   <Button tone="quiet" className="ev-action" disabled={busy} onClick={()=>refresh(n=>n+1)}>{t("刷新")}</Button>
  </div></div>
  {tr(error&&<p className="ev-analysis-notice" role="status">{t("读取存档失败：")}{tr(error)}{tr(snapshot?'。保留上一次结果。':'')}</p>)}
  {tr(busy&&!snapshot&&<p className="ev-analysis-notice" role="status">{t("正在读取监测记录…")}</p>)}
  {tr(!snapshot?null:latest<0?<p className="ev-analysis-notice">{t("所选时段暂无")}{tr(gpu?'显存':net?'网络':'该资源的')}{t("历史读数。监测记录保存后可刷新查看。")}</p>:<>
   <div className="ev-traffic-content">
    <section className="resource-chart ev-traffic-plot" aria-label={tr(trendTitle)}>
     <SectionTitle meta={gpu?(cardId==='all'?t("全部 GPU"):cards.find(([key])=>key===cardId)?.[1]?.Name):null} action={!cpu?<ThemedSelect aria-label={t("对比时长")} compact menuMinWidth={154} value={manualStart==null?String(duration):'custom'} onChange={e=>{setDuration(Number(e.target.value));setBand(null);setManualStart(null)}}>{tr(manualStart!=null&&<option value="custom" disabled>{t("自定义区间")}</option>)}{tr([2,5,15].map(v=><option value={v} key={v}>{t("对比前 ")}{tr(v)}{t(" 分钟")}</option>))}</ThemedSelect>:null}>{tr(trendTitle)}</SectionTitle>
     <Trend series={series} points={points} selected={end} onSelect={i=>{const j=nearestIndex(points,points[i]?.time,(_,k)=>selectable(k));if(j>=0)pick(points[j].time)}} unit={unit} maxValue={max} from={snapshot.from} to={snapshot.to} markers={markers} onMove={move}/>
     <div className="ev-traffic-foot"><span>{tr(peak>=0?`${net?'下载峰值':'峰值'} ${clock(points[peak].time,false)} · ${net?formatTraffic(values[peak]*rateScale.divisor,true):format(values[peak])}`:'该时段无峰值记录')}</span><Button className="ev-action" tone="quiet" disabled={peak<0||end===peak} onClick={()=>pick(points[peak].time)}>{t("定位峰值")}</Button></div>
    </section>
    <section className="ev-traffic-rank" aria-label={tr(rankTitle)}>
     <div className="ev-rank-head"><div><h3>{tr(rankTitle)}</h3><p data-selection>{tr(timeLabel(cpu?null:prior,cpu?(cpuPoint??point):point))}</p></div>{tr(end!==defaultEnd&&<Button tone="quiet" className="ev-action" onClick={()=>pick(null)}>{t("返回最近")}</Button>)}</div>
     <dl className="ev-volume ev-resource-volume" aria-live="polite">{tr(summary.map(([name,value])=><div key={name}><dt>{tr(name)}</dt><dd>{tr(value)}</dd></div>))}</dl>
     <div ref={list} className="ev-app-list ev-analysis-scroll" tabIndex={0} role="region" aria-label={tr(rankTitle)}>{tr(rows.length?rows.map(row=><ReadingRow key={row.id} row={row} showGpu={gpu&&cardId==='all'} bar={amount(row)/magnitude*100} value={cpu?percent(row.value):net?volume(amount(row)):signed(row.change)} open={expanded?.id===row.id} onToggle={cpu||net?null:()=>setExpanded(expanded?.id===row.id?null:row)}><EndpointRows row={row} markers={processMarkers}/></ReadingRow>):<p className="ev-analysis-empty">{tr(emptyRows)}</p>)}</div>
     {(cpu||net||memory&&rows.length>0&&(processPrior!==prior||processPoint!==point))&&<div className="ev-traffic-foot ev-traffic-rank-foot"><span>{cpu?(cpuPoint&&cpuPoint!==point?t('已选 {0} · 进程采样 {1}',[clock(point.time),clock(cpuPoint.time)]):t('以整机 CPU 为基准；仅列出已采样进程。')):net?t(comparison.complete?'按网卡累计计数差计算。':'区间含缺样或计数重置，仅统计有记录部分。'):t('进程采样：{0}–{1}',[clock(processPrior.time),clock(processPoint.time)])}</span></div>}
    </section>
   </div>
   {tr(cpu?<CoreHistory points={points} from={snapshot.from} to={snapshot.to} selectedTime={point?.time} onPick={pick} thinned={snapshot.thinned}/>:gpu?<GpuHeadroom points={points} values={gpuData} from={snapshot.from} to={snapshot.to} prior={prior} point={point} onRange={setWindow}/>:memory?<MemoryHistory points={points} from={snapshot.from} to={snapshot.to} prior={processPrior} point={processPoint} process={process}/>:<NetworkQuality points={points} from={snapshot.from} to={snapshot.to} adapter={adapterId} prior={prior} point={point} onRange={setWindow}/>)}
  </>)}
 </section>;
}
