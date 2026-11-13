import {t,tr,useLocale,locale} from '../i18n/index.js';
import {RollingDigits,useResolvedNumberMotion} from '../RollingDigits';
import {trafficCaps,trafficCapLabel,validTrafficCap,migrateTrafficCap,stageDurations,validStageSeconds,migrateStageSeconds} from './network-speed.js';
import React,{useEffect,useLayoutEffect,useRef,useState} from 'react';
import {ArrowDown,ArrowUp,History,Play,Settings2,Square} from 'lucide-react';
import {Button,Modal} from '../components';
import {ThemedSelect} from '../themed-select';
import {ToolNavigation} from '../foundation/ToolNavigation';
import {useWorkspaceState} from '../foundation/use-workspace-state';
import {native} from './bridge';
import {NetworkPage} from './PeripheralPages';
import {measureSpeed,nodes,speedUnit,STAGE_SECONDS} from './network-speed';
import {ResourceAnalysis} from './analysis/ResourceAnalysis';
import './network-speed.css';
const EMPTY={samples:{down:[],up:[]},phaseSeconds:{}};
const prefKey='asterhub-network-speed-preferences-v1',historyKey='asterhub-network-speed-records-v1';
function read(key,fallback){try{return JSON.parse(localStorage.getItem(key))??fallback}catch{return fallback}}
function save(key,value){try{localStorage.setItem(key,JSON.stringify(value))}catch{}}
const statusLabels={idle:'等待开始',checking:'正在比较测速节点',down:'正在测量下载',up:'正在测量上传',done:'测速完成',stopped:'已停止',error:'测速中断'};
function Rolling({value,digits=1}){useLocale();
 const mode=useResolvedNumberMotion();
 return <span className="ns-roll"><RollingDigits text={value==null?'—':value.toFixed(digits)} mode={mode}/></span>;
}
const directions=[['down','--speed-download','#9bceac'],['up','--speed-upload','#8eb7ec']];
const easeChart=p=>1-Math.pow(1-p,3);
const visibleCount=(previous,length,progress)=>previous===0?(length<=1?length:1+(length-1)*progress):previous+(length-previous)*progress;
function Plot({samples,finished,runKey,unit,factor,stageSeconds=STAGE_SECONDS}){useLocale();
 const graph=useRef(null),draw=useRef(null),scene=useRef({key:null,series:{down:[],up:[]},high:4,ends:{down:STAGE_SECONDS,up:STAGE_SECONDS},settled:{down:false,up:false},animation:null});
 useLayoutEffect(()=>{
  const canvas=graph.current,ctx=canvas.getContext('2d');
  let size;
  draw.current=()=>{
   if(!size)return;
   const {width:w,height:h,ratio}=size, {series,high,ends,settled,animation}=scene.current;
   const p={left:w<380?43:58,right:w<380?24:34,top:23,bottom:27},ph=h-p.top-p.bottom,pw=w-p.left-p.right;
   if(ph<=0||pw<=0)return;
   ctx.setTransform(ratio,0,0,ratio,0,0);ctx.clearRect(0,0,w,h);
   const ready=series.down.length||series.up.length;
   const colors=getComputedStyle(canvas);
   ctx.font='12px Inter, sans-serif';ctx.textAlign='right';ctx.fillStyle=colors.getPropertyValue('--muted').trim()||'#a8b6ad';
   for(let i=0;i<5;i++){const y=p.top+ph*i/4;ctx.strokeStyle=colors.getPropertyValue('--line').trim()||'#ffffff12';ctx.beginPath();ctx.moveTo(p.left,y);ctx.lineTo(w-p.right,y);ctx.stroke();if(ready)ctx.fillText(String(Number((high*factor*(1-i/4)).toFixed(1))),p.left-8,y+4)}
   if(!ready)return;
   ctx.textAlign='left';ctx.fillText(unit,p.left,13);
   const ticks=w<450?2:4;
   for(let i=0;i<=ticks;i++){const x=p.left+pw*i/ticks;ctx.textAlign=i===0?'left':i===ticks?'right':'center';ctx.fillText(`${i*100/ticks}%`,x,h-6)}
   for(const [key,token,fallback] of directions){
    const color=colors.getPropertyValue(token).trim()||fallback;
    const values=series[key];if(!values.length)continue;
    const previous=animation?.from[key]??values.length,progress=animation?.progress??1;
    const count=visibleCount(previous,values.length,progress);
    const index=Math.min(values.length-1,Math.max(0,Math.floor(count)-1));
    const visible=values.slice(0,index+1);
    if(index+1<values.length&&count%1>0){const a=values[index],b=values[index+1],f=count%1;visible.push({t:a.t+(b.t-a.t)*f,value:a.value+(b.value-a.value)*f})}
    ctx.save();ctx.beginPath();ctx.rect(p.left-4,p.top-4,pw+8,ph+8);ctx.clip();
    ctx.beginPath();ctx.strokeStyle=color;ctx.lineWidth=2;ctx.lineJoin='round';ctx.lineCap='round';
    let x,y;
    visible.forEach((sample,i)=>{x=p.left+pw*(values.length===1&&settled[key]?Math.max(0,Math.min(1,(stageSeconds-ends[key])/(stageSeconds-.001))):Math.min(1,sample.t/ends[key]));y=p.top+ph*(1-sample.value/high);if(i)ctx.lineTo(x,y);else ctx.moveTo(x,y)});
    ctx.stroke();ctx.fillStyle=color;ctx.beginPath();ctx.arc(x,y,3,0,Math.PI*2);ctx.fill();ctx.restore();
   }
  };
  const resize=()=>{const rect=canvas.getBoundingClientRect(),ratio=devicePixelRatio||1;size={width:rect.width,height:rect.height,ratio};canvas.width=Math.round(rect.width*ratio);canvas.height=Math.round(rect.height*ratio);draw.current()};
  resize();const observer=new ResizeObserver(resize);observer.observe(canvas);
  const theme=new MutationObserver(()=>draw.current?.());theme.observe(document.documentElement,{attributes:true,attributeFilter:['data-theme','data-appearance-mode','style']});
  return()=>{observer.disconnect();theme.disconnect();draw.current=null;cancelAnimationFrame(scene.current.animation?.frame)};
 },[unit,factor,stageSeconds]);
 useLayoutEffect(()=>{
  const state=scene.current;
  const same=state.key===runKey&&directions.every(([k])=>state.series[k].length===samples[k].length&&state.settled[k]===(finished[k]!=null));
  if(same)return;
  cancelAnimationFrame(state.animation?.frame);
  const from=Object.fromEntries(directions.map(([key])=>{const length=state.series[key].length,old=state.animation;return [key,old?visibleCount(old.from[key],length,old.progress):length]}));
  const previousHigh=state.high,previousEnds={...state.ends};
  const series=Object.fromEntries(directions.map(([key])=>[key,samples[key].map((point,_,all)=>({...point,t:point.t-all[0].t}))]));
  const all=[...series.down,...series.up],peak=Math.max(0,...all.map(v=>v.value));
  const unit=Math.pow(10,Math.floor(Math.log10(Math.max(1,peak))));
  const high=Math.max(4,Math.ceil(peak/unit/4)*4*unit);
  const settled=Object.fromEntries(directions.map(([key])=>[key,finished[key]!=null]));
  const ends=Object.fromEntries(directions.map(([key])=>[key,settled[key]?Math.max(.001,series[key].at(-1)?.t??0):stageSeconds]));
  const animate=state.key===runKey&&all.length>0&&!matchMedia('(prefers-reduced-motion: reduce)').matches;
  state.key=runKey;state.series=series;state.settled=settled;state.animation=null;
  if(!animate){state.high=high;state.ends=ends;draw.current?.();return}
  const started=performance.now();
  const animation={progress:0,from};state.animation=animation;
  const frame=now=>{
   const t=Math.min(1,(now-started)/300),eased=easeChart(Math.max(0,t));
   animation.progress=eased;state.high=previousHigh+(high-previousHigh)*eased;state.ends=Object.fromEntries(directions.map(([key])=>[key,previousEnds[key]+(ends[key]-previousEnds[key])*eased]));
   if(t>=1)state.animation=null;
   draw.current?.();if(t<1)animation.frame=requestAnimationFrame(frame);
  };
  animation.frame=requestAnimationFrame(frame);
 },[samples,finished,runKey]);
 const ready=samples.down.length+samples.up.length>0;
 return <div className="ns-plot"><canvas ref={graph} data-down-samples={samples.down.length} data-up-samples={samples.up.length} aria-label={tr(ready?'下载与上传曲线，横轴为各自阶段进度':'等待下载和上传样本')}/>{tr(!ready&&<div className="ns-plot-empty">{t("收到有效传输样本后显示曲线")}</div>)}</div>;
}
const request=(action,params,timeout=4000,signal)=>native.request(`networkSpeed.${action}`,{deviceId:'local',...params},{timeout,signal});
const dateText=value=>new Date(value).toLocaleString(locale(),{hour12:false});
export function NetworkWorkspace(props){useLocale();
 const [view,setView]=useWorkspaceState(props.deviceId,'network-tool',()=>({tab:'monitor'}));
 const local=props.deviceId==='local',tab=view.tab==='analysis'||(local&&view.tab==='speed')?view.tab:'monitor';
 return <div className="network-workspace"><ToolNavigation label={t("网络工具")} items={[["monitor","运行监测"],...(local?[["speed","网络测速"]]:[]),["analysis","流量分析"]]} value={tab} onChange={tab=>setView({tab})}>{tr(tab==='speed'?<SpeedPanel/>:tab==='analysis'?<ResourceAnalysis key={props.deviceId} metric="network" deviceId={props.deviceId}/>:<NetworkPage {...props}/>)}</ToolNavigation></div>;
}
function SpeedPanel(){useLocale();
 const [history,setHistory]=useState(()=>{const x=read(historyKey,[]);return Array.isArray(x)?x.filter(r=>r?.samples?.down&&r?.samples?.up&&r?.id&&r?.at):[]});
 const [data,setData]=useState(()=>history[0]??EMPTY),[phase,setPhase]=useState(()=>history.length?'done':'idle'),[elapsed,setElapsed]=useState(()=>history[0]?.elapsed??null);
 const [error,setError]=useState(''),[dialog,setDialog]=useState(''),[days,setDays]=useState('7'),[cutoff,setCutoff]=useState(0);
 const [prefs,setPrefs]=useState(()=>{const x=read(prefKey,{});return {unit:x.unit==='bytes'?'bytes':'bits',node:nodes.some(n=>n.id===x.node)?x.node:'auto',cap:migrateTrafficCap(x.cap,{explicit:x.capCustomized===true}),seconds:migrateStageSeconds(x.seconds),capCustomized:x.capCustomized===true}});
 const [draft,setDraft]=useState(prefs);
 const task=useRef(null),started=useRef(null),alive=useRef(true),running=['checking','down','up'].includes(phase);
 const {factor,unit}=speedUnit(prefs.unit,Math.max(0,data.down??0,data.up??0,...data.samples.down.map(p=>p.value),...data.samples.up.map(p=>p.value)));
 useEffect(()=>{alive.current=true;return()=>{alive.current=false;task.current?.abort()}},[]);
 useEffect(()=>{if(!running)return;const timer=setInterval(()=>setElapsed(Math.floor((performance.now()-started.current)/1000)),250);return()=>clearInterval(timer)},[running]);
 async function start(){
  if(task.current)return;
  const run=new AbortController();task.current=run;started.current=performance.now();setElapsed(0);setData(EMPTY);setPhase('checking');setError('');
  try{
   const record=await measureSpeed({cap:prefs.cap,choice:prefs.node,seconds:prefs.seconds,signal:run.signal,call:request,onUpdate:(stage,value)=>{if(alive.current){setData(value);setPhase(stage)}}});
   if(alive.current){setElapsed(Math.floor(record.elapsed));setHistory(rows=>{const next=[record,...rows].slice(0,30);save(historyKey,next);return next});}
  }catch(e){if(alive.current){setElapsed(Math.floor((performance.now()-started.current)/1000));setPhase(run.signal.aborted?'stopped':'error');if(!run.signal.aborted)setError(e.message)}}
  finally{if(task.current===run)task.current=null;}
 }
 function viewRecord(record){if(running)return;setData(record);setElapsed(Math.floor(record.elapsed));setPhase('done');setError('');}
 const matches=dialog==='clear'?history.filter(row=>{const date=Date.parse(row.at);return date>=cutoff-Number(days)*86400000&&date<=cutoff}):[];
 function clearRecords(){const ids=new Set(matches.map(r=>r.id)),next=history.filter(r=>!ids.has(r.id));setHistory(next);save(historyKey,next);setDialog('');if(ids.has(data.id)){setData(next[0]??EMPTY);setElapsed(next[0]?Math.floor(next[0].elapsed):null);setPhase(next[0]?'done':'idle');}}
 function applySettings(){const cap=Number(draft.cap);if(!validTrafficCap(cap)||!validStageSeconds(Number(draft.seconds)))return;const next={...draft,cap,seconds:Number(draft.seconds),capCustomized:true};setPrefs(next);save(prefKey,next);setDialog('');}
 const datum=(value,mult=1)=>value==null?null:value*mult;
 const sourceLabel=key=>(data.sources?.[key]||[]).map(id=>nodes.find(n=>n.id===id)?.name??id).join(' · ');
 const lossNote=data.loss?`${data.loss.received}/${data.loss.sent} 次应答 · ${data.loss.resolver}`:data.udpUnavailable?'UDP 探测未连通':'等待实测';
 return <div className="network-speed">
  <header className="ns-heading"><Button icon={Settings2} tone="quiet" disabled={running} onClick={()=>{setDraft(prefs);setDialog('settings')}}>{t("测量设置")}</Button></header>
  <div className="ns-layout"><section className="ns-card ns-main" aria-label={t("本次测速")}>
   <div className="ns-title"><span role="status" data-running={running}><i/>{tr(data.partial&&phase==='done'?'测速部分完成 · 已保留样本':statusLabels[phase])}</span><span>{t("本机实测")}</span></div>
   <div className="ns-speeds">{tr(directions.map(([key])=>{const active=data.samples[key].at(-1)?.value;const Icon=key==='down'?ArrowDown:ArrowUp;return <div key={key} data-direction={key}><span title={tr(sourceLabel(key)||undefined)}><Icon size={16}/>{tr(key==='down'?'下载':'上传')}</span><strong data-metric={key}><Rolling value={datum(active,factor)}/>{tr(active!=null&&<small>{tr(unit)}</small>)}</strong></div>}))}</div>
   <Plot samples={data.samples} finished={data.phaseSeconds} runKey={data.id??null} unit={unit} factor={factor} stageSeconds={data.stageSeconds??prefs.seconds}/>
   <div className="ns-legend"><span><i/>{t("下载")}</span><span><i/>{t("上传")}</span><span>{t("横轴为各阶段进度")}</span></div>
   <div className="ns-metrics">{tr([['空闲延迟',data.ping,'ms',1],['抖动',data.jitter,'ms',1],['UDP 丢包',data.loss?.value,'%',1],['测量时长',elapsed,'s',0]].map(([label,value,unit,digits])=><div key={label}><span title={tr(label==='UDP 丢包'?lossNote:undefined)}>{tr(label)}</span><strong><Rolling value={value} digits={digits}/>{tr(value!=null&&<small>{tr(unit)}</small>)}</strong></div>))}</div>
   {tr(error&&<p className="ns-error" role="alert">{tr(error)}</p>)}
   <div className="ns-actions"><span>{t("流量上限 ")}{tr(trafficCapLabel(data.cap??prefs.cap))}{tr(data.used>0&&<>{t(" · 已预留 ")}<Rolling value={data.used/1048576} digits={2}/> MiB</>)}</span>{tr(running?<Button icon={Square} onClick={()=>task.current?.abort()}>{t("停止测速")}</Button>:<Button tone="primary" icon={Play} onClick={start}>{tr(phase==='idle'?'开始测速':'重新测速')}</Button>)}</div>
   <div className="ns-stages" aria-label={t("测速阶段")}>{tr([['checking','探测节点'],['down','下载'],['up','上传'],['done','结果']].map(([id,label],i,all)=><span key={id} data-reached={i<=all.findIndex(([x])=>x===phase)} aria-current={id===phase?'step':undefined}>{tr(label)}</span>))}</div>
  </section><aside className="ns-card ns-summary"><div className="ns-summary-head"><h3>{t("本次测量")}</h3></div>
   <dl>{tr([['平均下载',datum(data.down,factor),unit],['平均上传',datum(data.up,factor),unit],['下载用时',data.phaseSeconds.down,'s'],['上传用时',data.phaseSeconds.up,'s']].map(([label,value,unit])=><div key={label}><dt>{tr(label)}</dt><dd><Rolling value={value}/>{tr(value!=null&&<small>{tr(unit)}</small>)}</dd></div>))}</dl>
   <p className="ns-udp-detail"><span>{t("UDP 探测")}</span><span>{tr(lossNote)}</span></p>
   <div className="ns-history"><div className="ns-summary-head"><h3><History size={16}/>{t("实测记录")}</h3>{tr(history.length>0&&<Button tone="quiet" disabled={running} onClick={()=>{setCutoff(Date.now());setDialog('clear')}}>{t("清理记录")}</Button>)}</div>
    <div className="ns-history-list">{tr(history.map(row=>{const u=speedUnit(prefs.unit,Math.max(row.down??0,row.up??0));return <button type="button" key={row.id} disabled={running} aria-pressed={row.id===data.id} onClick={()=>viewRecord(row)}><span><strong>{tr(row.node)}</strong><small>{tr(dateText(row.at))}</small></span><span>{tr(row.down==null?'—':(row.down*u.factor).toFixed(1)+' '+u.unit)}</span></button>}))}</div>
   </div>
  </aside></div>
  {tr(dialog==='settings'&&<Modal layout="form" title={t("测量设置")} onClose={()=>setDialog('')} className="network-speed-dialog"><div className="ns-form">
   <label htmlFor="speed-unit-label"><span id="speed-unit-label">{t("速率单位")}</span><ThemedSelect aria-labelledby="speed-unit-label" value={draft.unit} onChange={e=>setDraft(x=>({...x,unit:e.target.value}))}><option value="bits">Mbps</option><option value="bytes">KB/s · MB/s</option></ThemedSelect></label>
   <label><span id="speed-node-label">{t("测速节点")}</span><ThemedSelect aria-labelledby="speed-node-label" value={draft.node} onChange={e=>setDraft(x=>({...x,node:e.target.value}))}><option value="auto">{t("自动选择最稳定的节点")}</option>{nodes.map(n=><option value={n.id} key={n.id}>{n.name}</option>)}</ThemedSelect></label>
   <label><span id="speed-cap-label">{t("流量上限")}</span><ThemedSelect aria-labelledby="speed-cap-label" value={draft.cap} onChange={e=>setDraft(x=>({...x,cap:Number(e.target.value)}))}>{tr(trafficCaps.map(([value,label])=><option key={value} value={value}>{tr(label)}</option>))}</ThemedSelect></label>
   <label><span id="speed-duration-label">{t("每阶段时长")}</span><ThemedSelect aria-labelledby="speed-duration-label" value={draft.seconds} onChange={e=>setDraft(x=>({...x,seconds:Number(e.target.value)}))}>{tr(stageDurations.map(value=><option key={value} value={value}>{tr(value)}{t(" 秒")}</option>))}</ThemedSelect></label>
   <p>{t("下载和上传使用相同时长，达到流量上限会提前结束。1 MiB = 1,048,576 字节，1 GiB = 1,024 MiB。")}</p></div>
   <footer className="dialog-actions"><Button onClick={()=>setDialog('')}>{t("取消")}</Button><Button tone="primary" disabled={!validTrafficCap(Number(draft.cap))||!validStageSeconds(Number(draft.seconds))} onClick={applySettings}>{t("保存")}</Button></footer>
  </Modal>)}
  {tr(dialog==='clear'&&<Modal layout="form" title={t("清理实测记录")} onClose={()=>setDialog('')} className="network-speed-dialog"><div className="ns-form"><label><span id="speed-date-label">{t("日期范围")}</span><ThemedSelect aria-labelledby="speed-date-label" value={days} onChange={e=>setDays(e.target.value)}>{tr([[1,'最近 24 小时'],[7,'最近 7 天'],[30,'最近 30 天'],[90,'最近 90 天']].map(([key,text])=><option value={key} key={key}>{tr(text)}</option>))}</ThemedSelect></label><p>{t("将删除此范围内的 ")}{tr(matches.length)}{t(" 条记录。")}</p></div><footer className="dialog-actions"><Button onClick={()=>setDialog('')}>{t("取消")}</Button><Button tone="danger" disabled={!matches.length} onClick={clearRecords}>{t("确认删除")}</Button></footer></Modal>)}
 </div>;
}
