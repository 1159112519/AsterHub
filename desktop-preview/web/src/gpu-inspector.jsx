import './gpu-inspector-preview5.css';
import {loadColor} from './load-color';
import {ChartControlBar} from "./chart-control-bar";
import {visibleProcesses} from './processes';
import {ScrollSurface} from './scroll-surface';
import React,{useRef,useState,useMemo,useLayoutEffect,useEffect} from 'react';
import {createPortal} from 'react-dom';
import {ChevronDown,Microchip,X,Thermometer,Wind,Zap,Activity,MemoryStick,Terminal} from 'lucide-react';
import gsap from 'gsap';
import {Chart,NumberValue,IconButton} from './components';
import {colors,wave} from './data';
import {liftFromSource,returnToSource,moveSurface,revealContent} from './motion';

export function GpuInspector({gpu,anchor,pulse,processes=[],onClose,liveSamples=null,onEndProcess=null,confirmationOpen=false}){
 const panel=useRef(null),closeButton=useRef(null),closing=useRef(false),exitTween=useRef(null),onCloseRef=useRef(onClose);
 onCloseRef.current=onClose;
 const [rangeMinutes,setRangeMinutes]=useState(15),[expandedCommands,setExpandedCommands]=useState({});
 const liveTrend=useMemo(()=>liveSamples?.filter(s=>s.time>=Date.now()-rangeMinutes*60000).map(s=>({time:s.time,value:s.gpus.find(g=>g.id===gpu.id)?.util}))??null,[liveSamples,rangeMinutes,gpu.id]);
 const trend=useMemo(()=>[{name:'GPU '+gpu.id,color:colors.gpu,values:liveTrend?liveTrend.map(p=>p.value):gpu.series}],[gpu.id,gpu.series,liveTrend]);
 const [position,setPosition]=useState(null),previousPosition=useRef(null),previousGpu=useRef(gpu.id);
 const util=liveSamples?gpu.util:gpu.util>5?Math.min(100,gpu.util+Math.sin(pulse+gpu.id)*1.2):gpu.util;
 const rows=visibleProcesses(processes).filter(p=>p.gpu===gpu.id);
 function dismiss(restore=false){
  if(closing.current)return;closing.current=true;
  const finish=()=>{if(restore&&anchor?.isConnected)anchor.focus({preventScroll:true});onCloseRef.current()};
  exitTween.current=returnToSource(panel.current,finish);
 }
 useLayoutEffect(()=>{
  closing.current=false;exitTween.current?.kill();
  function place(){
   if(!anchor?.isConnected)return;
   const a=anchor.getBoundingClientRect(),p={height:panel.current.offsetHeight},width=window.innerWidth,height=window.innerHeight;
   const mobile=width<=600,margin=mobile?12:16;
   const footer=document.querySelector('.status-footer')||document.querySelector('.resource-footer')||document.querySelector('.bottom-bar');
   const footerTop=footer?.getBoundingClientRect().top;
   const bottom=mobile?height-12:Math.min(height-16,footerTop>0?footerTop-16:height-96);
   const minTop=mobile?12:76,maxHeight=Math.max(180,bottom-minTop);
   const h=Math.min(p.height,maxHeight),panelWidth=Math.min(mobile?width-24:600,width-margin*2);
   let left=mobile?12:a.right+26;
   if(!mobile&&left+panelWidth>width-margin){left=a.left-panelWidth-26;if(left<margin)left=Math.max(margin,Math.min(width-margin-panelWidth,a.left+a.width/2-panelWidth/2))}
   let top=mobile?bottom-h:Math.max(minTop,Math.min(a.top+8,bottom-h));
   const cx=a.left+a.width/2,cy=a.top+a.height/2;
   const side=mobile?'none':cx<left?'left':cx>left+panelWidth?'right':cy<top+h/2?'top':'bottom';
   const tipX=Math.max(40,Math.min(panelWidth-40,cx-left)),tipY=Math.max(44,Math.min(h-44,cy-top));
   setPosition(old=>{const next={left,top,width:panelWidth,maxHeight,side,tipX,tipY};return old&&JSON.stringify(old)===JSON.stringify(next)?old:next});
  }
  place();const observer=new ResizeObserver(place);observer.observe(panel.current);observer.observe(anchor);
  window.addEventListener('resize',place);window.addEventListener('scroll',place,true);
  return()=>{observer.disconnect();window.removeEventListener('resize',place);window.removeEventListener('scroll',place,true)};
 },[anchor,gpu.id]);
 useLayoutEffect(()=>{
  if(!position)return;
  const previous=previousPosition.current;
  if(closing.current){previousPosition.current=position;return}
  const animation=previous?moveSurface(panel.current,previous,position,anchor):liftFromSource(panel.current,anchor);
  previousPosition.current=position;
  return()=>animation?.kill();
 },[position?.left,position?.top,anchor,gpu.id]);
 useLayoutEffect(()=>{
  if(previousGpu.current===gpu.id)return;previousGpu.current=gpu.id;
  const animation=revealContent(panel.current.querySelector('.scroll-surface-body'));
  return()=>animation?.kill();
 },[gpu.id]);

 useEffect(()=>{
  function pointer(e){if(confirmationOpen||e.target.closest('.modal-backdrop')||panel.current?.contains(e.target)||anchor?.contains(e.target)||e.target.closest('.gpu-tile,.gpu-list tr,.themed-select-menu'))return;dismiss(false)}
  function keyboard(e){if(!confirmationOpen&&e.key==='Escape'){e.stopPropagation();e.preventDefault();dismiss(true)}}
  document.addEventListener('pointerdown',pointer);document.addEventListener('keydown',keyboard);
  return()=>{document.removeEventListener('pointerdown',pointer);document.removeEventListener('keydown',keyboard)};
 },[anchor,gpu.id,confirmationOpen]);
 useLayoutEffect(()=>{if(position)closeButton.current?.focus({preventScroll:true})},[gpu.id,!!position]);
 useEffect(()=>()=>exitTween.current?.kill(),[]);
 return createPortal(<aside id="gpu-inspector" ref={panel} role="dialog" aria-modal="false" aria-labelledby="gpu-inspector-title" className="gpu-inspector gpu-detail" data-tail={position?.side||'none'} style={{left:position?.left??16,top:position?.top??76,width:position?.width??'min(600px,calc(100vw - 32px))',maxHeight:position?.maxHeight??'calc(100dvh - 172px)',visibility:position?'visible':'hidden','--surface-max-height':`${position?.maxHeight??Math.max(180,window.innerHeight-172)}px`,'--tip-x':`${position?.tipX||48}px`,'--tip-y':`${position?.tipY||48}px`}}>
  <span className="thought-dot large" aria-hidden="true"/><span className="thought-dot small" aria-hidden="true"/>
  <ScrollSurface className="gpu-inspector-viewport" contentClassName="gpu-inspector-scroll" resetKey={gpu.id}>
   <header className="gpu-inspector-header"><div className="gpu-inspector-badge"><Microchip size={22}/></div><div><h2 id="gpu-inspector-title">GPU {gpu.id}</h2><p>{gpu.name}</p></div><span className={`inspector-state ${util>10?'busy':''}`}><i/>{util>10?'运行中':'空闲'}</span><button type="button" ref={closeButton} className="icon-button" aria-label="关闭 GPU 详情" onClick={()=>dismiss(true)}><X size={18}/></button></header>
   <div className="inspector-primary"><div style={{color:loadColor(util)}}><span><Activity size={14}/>利用率</span><NumberValue key={gpu.id} value={util} decimals={1} suffix="%"/></div><div style={{color:loadColor(gpu.total>0?gpu.used/gpu.total*100:null)}}><span><MemoryStick size={14}/>显存</span><div><NumberValue key={gpu.id} value={gpu.used} decimals={2}/><span className="inspector-limit"> / {gpu.total?.toFixed(2)??'—'} GiB</span></div></div></div>
   <section className="inspector-trend chart-controls-external"><div className="inspector-section-title"><h3>利用率趋势</h3></div><ChartControlBar rangeMinutes={rangeMinutes} onRangeChange={setRangeMinutes} timeLabel="GPU 详情时间范围"/>{liveTrend?.length<2?<p className="native-chart-wait">等待下一个真实采样点</p>:<Chart key={gpu.id} series={trend} timestamps={liveTrend?.map(p=>p.time)} height={155} scalable showScaleControl={false} live={!!liveSamples} rangeMinutes={rangeMinutes}/>}</section>
   <dl className="detail-stats" aria-label={`GPU ${gpu.id} 硬件指标`}><div><dt><Thermometer size={15}/>温度</dt><dd><NumberValue key={gpu.id} value={gpu.temp} suffix="°C"/></dd></div><div><dt><Zap size={15}/>功耗 / 限制</dt><dd><NumberValue key={gpu.id} value={gpu.power}/><span className="metric-limit">/ {gpu.limit??'—'} <small>W</small></span></dd></div><div><dt><Wind size={15}/>风扇</dt><dd><NumberValue key={gpu.id} value={gpu.fan} suffix="%"/></dd></div></dl>
   <section className="inspector-processes"><div className="inspector-section-title"><h3>GPU 进程 <span>{rows.length}</span></h3></div>{rows.length?<div className="inspector-table-scroll"><table><thead><tr><th>进程 / PID</th><th>用户</th><th>显存</th>{onEndProcess&&<th className="process-action-column">操作</th>}</tr></thead><tbody>{rows.map(p=>{const command=p.commandLine||p.path,expanded=!!expandedCommands[p.pid],detailId=`gpu-${gpu.id}-process-${p.pid}-command`;return <React.Fragment key={p.pid}><tr><td><strong title={p.path}>{p.name}</strong><small>PID {p.pid}</small>{command&&<button type="button" className="process-command-toggle" aria-expanded={expanded} aria-controls={expanded?detailId:undefined} onClick={()=>setExpandedCommands(old=>({...old,[p.pid]:!old[p.pid]}))}><ChevronDown size={13} aria-hidden="true"/>{p.commandLine?'完整命令':'可执行文件'}</button>}</td><td>{p.user}</td><td>{p.memory==null?'—':p.memory.toFixed(2)} <small>GiB</small></td>{onEndProcess&&<td><button type="button" className="process-end-button" onClick={()=>onEndProcess(p)}>结束进程</button></td>}</tr>{expanded&&command&&<tr className="process-command-row"><td colSpan={onEndProcess?4:3}><div id={detailId} className="process-command-flat"><code>{command}</code></div></td></tr>}</React.Fragment>})}</tbody></table></div>:<div className="inspector-empty"><Terminal size={16}/><span>当前没有可识别的 GPU 进程</span></div>}</section>
  </ScrollSurface>
 </aside>,document.body)
}


