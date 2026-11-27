import React,{useMemo,useRef,useLayoutEffect,useSyncExternalStore} from 'react';
import {Cpu,Microchip,MemoryStick,Network,HardDrive,ArrowDown,ArrowUp} from 'lucide-react';
import {Chart,NumberValue,SectionTitle} from '../components';
import {t,tr,useLocale} from '../i18n/index.js';
import {liveRangeSamples,orderedSamples,timelineSampleProps} from '../time-series';
import {ByteValue} from './ResourceWidgets';
import {storageSummary} from './resource-summary';
import {resourceChartSamples} from './light-telemetry';
import {storageRate} from './storage-rates';
import {bytes} from './telemetry';
import {useWorkspaceState} from '../foundation/use-workspace-state';
import {reducedMotion,motion} from '../motion';
import {OverviewTrendSeries} from './OverviewTrendSeries';
import './overview-focus.css';

const metrics=[['cpu','CPU',Cpu,'var(--icon-blue)','CPU 使用率'],['gpu','GPU',Microchip,'var(--icon-purple)','GPU 使用率'],['memory','内存',MemoryStick,'var(--icon-pink)','内存使用率'],['network','网络',Network,'var(--icon-cyan)','网络速率'],['storage','存储',HardDrive,'var(--icon-amber)','磁盘读写速率']];
const span=3;
// Use extra vertical room in tall windows without crowding the inventory in shorter ones.
const getTrendHeight=()=>260+Math.min(52,Math.max(0,Math.round((window.innerHeight-780)*.22)));
const watchTrendHeight=notify=>{window.addEventListener('resize',notify);return()=>window.removeEventListener('resize',notify);};
const gi=(n)=>Number.isFinite(n)?n.toFixed(1)+' GiB':'—';
const rate=(s,up=false)=>s.network?.[up?'UploadBytesPerSecond':'DownloadBytesPerSecond'];
const io=(s,write=false)=>storageRate(s,write?'WriteBytesPerSecond':'ReadBytesPerSecond');
function resourceSeries(rows,id,s,color){
 if(id==='network'||id==='storage'){
  const pick=id==='network'?rate:io;
  const names=id==='network'?['下载','上传']:['读取','写入'];
  return names.map((name,i)=>({name:t(name),color:i?'var(--blue)':color,values:rows.map(r=>pick(r,!!i))}));
 }
 if(id==='gpu')return s.gpus.map((g,i)=>({name:s.gpus.length>1?'GPU '+g.id:'GPU',color:[color,'var(--blue)','var(--amber)','var(--icon-pink)'][i%4],values:rows.map(r=>r.gpus?.find(item=>item.id===g.id)?.util)}));
 return [{name:id==='cpu'?'CPU':t('内存'),color,values:rows.map(r=>r[id])}];
}
function readingText(used,total,format){return Number.isFinite(used)&&Number.isFinite(total)?format(used)+' / '+format(total):'—'}
function context(s,id,visibleSeries){
 const h=s.hardware||{};
 if(id==='cpu')return [h.CpuModel,[['逻辑处理器',h.SystemThreads??(s.cores.length||'—')],['物理核心',h.PhysicalCores??'—'],['当前频率',Number.isFinite(s.cpuFrequencyMHz)?(s.cpuFrequencyMHz/1000).toFixed(2)+' GHz':'—']]];
 if(id==='gpu')return [s.gpus.length===1?s.gpus[0].name:null,s.gpus.map(g=>[s.gpus.length===1?'显存用量':'GPU '+g.id,s.gpus.length===1?readingText(g.used,g.total,gi):Number.isFinite(g.util)?g.util.toFixed(1)+'%':'—',g.name]).concat(s.gpus.length===1?[['显卡温度',Number.isFinite(s.gpus[0].temp)?Math.round(s.gpus[0].temp)+' °C':'—'],['显卡功耗',Number.isFinite(s.gpus[0].power)?Math.round(s.gpus[0].power)+' W':'—']]:[])];
 if(id==='memory')return [null,[['已使用',gi(s.memUsed)],['可用',Number.isFinite(s.memUsed)&&Number.isFinite(s.memTotal)?gi(Math.max(0,s.memTotal-s.memUsed)):'—'],['总容量',gi(s.memTotal)]]];
 if(id==='network'){const peak=vs=>vs.reduce((max,v)=>Number.isFinite(v)?Math.max(max??0,v):max,null);return [s.network?.Interfaces?.map(i=>i.Name).slice(0,2).join(' · '),[['下载峰值',bytes(peak(visibleSeries[0].values),true)],['上传峰值',bytes(peak(visibleSeries[1].values),true)],['网络接口',s.network?.Interfaces?.length??'—']]];}
 const cap=storageSummary(s);return [null,[['可用空间',bytes(cap.free)],['总容量',bytes(cap.total)],['存储卷',cap.count||'—']]];
}
export default function OverviewFocus({sample,samples,lightSamples,hasSample,deviceId}){useLocale();
 const trendHeight=useSyncExternalStore(watchTrendHeight,getTrendHeight);
 const [selected,setSelected]=useWorkspaceState(deviceId,'overview-metric','cpu');const controls=useRef([]),strip=useRef(null),underline=useRef(null),underlineMotion=useRef(null),contextNode=useRef(null),lastContext=useRef(null);
 const idx=Math.max(0,metrics.findIndex(([id])=>id===selected));
 const [id,label,,color,title]=metrics[idx];
 const rows=useMemo(()=>orderedSamples(liveRangeSamples(id==='cpu'||id==='memory'?resourceChartSamples(samples,lightSamples,id):samples,span)),[id,samples,lightSamples]);
 const isRate=id==='network'||id==='storage';
 const all=resourceSeries(rows,id,sample,color),windowStart=(rows.at(-1)?.time??0)-span*60000;
 const firstVisible=rows.findIndex(r=>r.time>=windowStart),visibleSeries=all.map(s=>({...s,values:s.values.slice(Math.max(0,firstVisible))})),maximum=visibleSeries.reduce((max,s)=>s.values.reduce((m,v)=>Number.isFinite(v)?Math.max(m,v):m,max),0);
 const [factor,units]=maximum>=1024*1024?[1024*1024,'MiB/s']:maximum>=1024?[1024,'KiB/s']:[1,'B/s'];
 const rateTop=maximum/factor*1.12;const magnitude=10**Math.floor(Math.log10(Math.max(1,rateTop)));const top=Math.ceil(Math.max(1,rateTop)/magnitude)*magnitude;
 const series=isRate?all.map(s=>({...s,values:s.values.map(v=>Number.isFinite(v)?v/factor:null)})):all;
 const haveLine=visibleSeries.some(s=>s.values.some(Number.isFinite));
 const [model,fields]=context(sample,id,visibleSeries);const contextTitle=id==='network'?t('网络摘要'):t('当前状态');const validFields=fields.filter(([,v])=>v!=='—');
 const topGpu=sample.gpus.reduce((max,g)=>Number.isFinite(g.util)?Math.max(max??0,g.util):max,null);
 const current={cpu:sample.cpu,memory:sample.memory,gpu:topGpu,storage:storageSummary(sample).percent};
 const descriptions={cpu:'总占用',gpu:sample.gpus.length>1?'最高占用':'核心占用',memory:'物理内存用量',network:'当前上下行',storage:'总空间已用'};
 useLayoutEffect(()=>{
  const parent=strip.current,marker=underline.current,active=controls.current[idx];if(!parent||!marker||!active)return;
  const sync=(animate)=>{
   const prior=marker.getBoundingClientRect(),host=parent.getBoundingClientRect(),target=active.getBoundingClientRect(),oldColor=getComputedStyle(marker).backgroundColor;
   underlineMotion.current?.cancel();
   Object.assign(marker.style,{left:active.offsetLeft+'px',top:(active.offsetTop+active.offsetHeight-2)+'px',width:active.offsetWidth+'px',backgroundColor:color});
   const next=marker.getBoundingClientRect(),newColor=getComputedStyle(marker).backgroundColor;
   if(animate&&prior.width&&host.width&&!reducedMotion())underlineMotion.current=marker.animate([{transform:`translate(${prior.left-next.left}px,${prior.top-next.top}px) scaleX(${prior.width/next.width})`,backgroundColor:oldColor},{transform:'none',backgroundColor:newColor}],{duration:motion.duration.tool*1000,easing:motion.ease.css});
  };
  sync(true);const observer=new ResizeObserver(()=>{const b=active.offsetWidth;if(marker.style.width!==b+'px'||marker.style.left!==active.offsetLeft+'px'||marker.style.top!==(active.offsetTop+active.offsetHeight-2)+'px')sync(false)});observer.observe(parent);observer.observe(active);return()=>observer.disconnect();
 },[idx,color]);
 useLayoutEffect(()=>()=>underlineMotion.current?.cancel(),[]);
 useLayoutEffect(()=>{const previous=lastContext.current;lastContext.current=id;if(!previous||previous===id||reducedMotion())return;const animation=contextNode.current?.animate([{opacity:.35},{opacity:1}],{duration:motion.duration.surface*1000,easing:motion.ease.css});return()=>animation?.cancel()},[id]);
 function switchKey(e,i){const next=e.key==='ArrowRight'?(i+1)%metrics.length:e.key==='ArrowLeft'?(i+metrics.length-1)%metrics.length:e.key==='Home'?0:e.key==='End'?metrics.length-1:null;if(next===null)return;e.preventDefault();setSelected(metrics[next][0]);controls.current[next]?.focus({preventScroll:true});}
 return <section className="overview-focus" aria-label={t('资源速览')}>
  <div className="overview-focus-caption"><h2>{t('实时读数')}</h2></div>
  <div className="overview-focus-surface">
   <div className="overview-focus-select" role="tablist" ref={strip} aria-label={t('选择趋势指标')}>
    {metrics.map(([key,name,Icon,tone],i)=><button type="button" key={key} ref={b=>controls.current[i]=b} id={'overview-tab-'+key} role="tab" aria-controls="overview-trend-panel" aria-selected={key===id} tabIndex={key===id?0:-1} className="overview-focus-tile" style={{'--metric-tone':tone}} onClick={()=>setSelected(key)} onKeyDown={e=>switchKey(e,i)}>
      <span className="overview-focus-name"><Icon size={16} aria-hidden="true"/>{tr(name)}</span>
      {key==='network'?<><span className="overview-focus-reading overview-net-value"><ArrowDown size={12} aria-label={t('下载')}/><ByteValue value={rate(sample)} rate/></span><span className="overview-focus-qualifier overview-net-value"><ArrowUp size={11} aria-label={t('上传')}/><ByteValue value={rate(sample,true)} rate/></span></>:<><span className="overview-focus-reading"><NumberValue value={current[key]} decimals={1} suffix="%" stale={sample.stale||((key==='cpu'||key==='memory')&&sample.light?.stale)}/></span><span className="overview-focus-qualifier">{tr(descriptions[key])}</span></>}
    </button>)}
    <span ref={underline} className="overview-focus-indicator" aria-hidden="true"/>
   </div>
   <div className="overview-focus-panel" role="tabpanel" id="overview-trend-panel" aria-labelledby={'overview-tab-'+id}>
    <section className="overview-focus-trend" aria-label={tr(title)}>
     <SectionTitle meta={t('最近 3 分钟')}>{tr(title)}</SectionTitle>
     {haveLine?<Chart series={series} renderSeries={geometry=><OverviewTrendSeries metric={id} geometry={geometry}/>} timestamps={rows.map(r=>r.time)} height={trendHeight} timeAxisSeconds live rangeMinutes={span} maxValue={isRate?top:100} unit={isRate?units:'%'} showScaleControl={false} expectedInterval={rows.at(-1)?.interval||2} {...timelineSampleProps(rows)}/>:<div className="overview-focus-empty" style={{minHeight:trendHeight}} role="status">{t(hasSample?'暂无趋势数据':'等待采样')}</div>}
     <div className="overview-focus-legend" aria-label={t('图例')}>{haveLine&&(series.length>1?series:[{name:isRate?units:t('使用率'),color:series[0].color}]).map(s=><span key={s.name}><i style={{background:s.color}}/>{s.name}</span>)}</div>
    </section>
    <section className="overview-focus-context" ref={contextNode} aria-label={contextTitle}>
     <SectionTitle>{contextTitle}</SectionTitle>
     {model&&<p className="overview-focus-model" title={model}>{model}</p>}
     {validFields.length?<dl>{validFields.map(([k,v,hint])=><div key={k} title={hint}><dt>{tr(k)}</dt><dd>{v}</dd></div>)}</dl>:<p className="overview-focus-model">{t('暂无数据')}</p>}
    </section>
   </div>
  </div>
 </section>;
}
