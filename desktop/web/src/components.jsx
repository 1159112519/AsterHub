import {t,tr,useLocale,locale} from './i18n/index.js';
import {RollingDigits,useResolvedNumberMotion} from './RollingDigits';
import {measurementState} from './foundation/measurement.js';
import './foundation/ui.css';
import {createPortal} from 'react-dom';
import {ThemedSelect} from './themed-select';
import {getChartScale,setChartScale,subscribeChartScale,percentageMaximum,percentageTicks} from './chart-scale';
import {TimelineCurve} from './timeline-curve';
import {gapLimit} from './time-series';
import {MotionSurface} from './motion-surface';
import {motionOptions,reducedMotion,subscribeReducedMotion} from './motion';
import './motion-layout.css';
import React,{useEffect,useState,useRef,useMemo,useLayoutEffect,useSyncExternalStore,createContext,useContext} from 'react';
import {gsap} from 'gsap';
import {X,ChevronDown,ArrowUpRight,MoreHorizontal} from 'lucide-react';
import {minutesLabel} from './data';
export {Button,IconButton} from './foundation/ui';
import {Button,IconButton,DialogContent} from './foundation/ui';
import {useDismissableLayer} from './foundation/overlay';
const NumericUpdateCadence=createContext(2000);
export function NumericUpdateProvider({intervalMs,children}){useLocale();return <NumericUpdateCadence.Provider value={intervalMs}>{tr(children)}</NumericUpdateCadence.Provider>}
export function NumberValue({supported=true,stale=false,...props}){useLocale();const state=measurementState(props.value,{supported,stale});return state==='ready'||state==='stale'?<AnimatedNumber {...props} state={state}/>:<span data-value-state={state} className={`number ${props.className||''}`}>—</span>}
function useMotionPreference(){return useSyncExternalStore(subscribeReducedMotion,reducedMotion)}
function AnimatedNumber({value,decimals=0,suffix='',className='',state='ready'}){useLocale();
 const mode=useResolvedNumberMotion(),intervalMs=useContext(NumericUpdateCadence);
 const duration=Math.max(80,Math.min(240,(Number.isFinite(intervalMs)?intervalMs:2000)*.46));
 return <span data-value-state={state} data-number-motion={mode} className={`number ${className}`}><RollingDigits text={value.toFixed(decimals)} mode={mode} duration={duration}/>{tr(suffix&&<small>{tr(suffix)}</small>)}</span>;
}
export function Meter({value,color='var(--green)',label}){useLocale();
 const ratio=Math.max(0,Math.min(Number.isFinite(value)?value:0,100))/100,reduce=useMotionPreference();
 const style={transform:`scaleX(${ratio})`};
 return <div className="meter motion-meter" data-reduced={reduce} style={{"--meter-color":color}} aria-label={tr(label)} role="meter" aria-valuenow={Number.isFinite(value)?value:undefined} aria-valuemin={0} aria-valuemax={100}><span className="meter-echo" style={style}/><span style={style}/></div>
}
// Store the displayed Bezier control points, not the last requested samples.
// Subdivision matches topology exactly, without changing the visible curve.
function curveCoordinates(points){
 const clamp=(v,a,b)=>Math.max(Math.min(a,b),Math.min(Math.max(a,b),v));
 if(!points.length)return [];
 const result=[...points[0]];
 for(let i=1;i<points.length;i++){const p=points[i],a=points[i-1],before=points[Math.max(0,i-2)],after=points[Math.min(points.length-1,i+1)];result.push(clamp(a[0]+(p[0]-before[0])/6,a[0],p[0]),clamp(a[1]+(p[1]-before[1])/6,a[1],p[1]),clamp(p[0]-(after[0]-a[0])/6,a[0],p[0]),clamp(p[1]-(after[1]-a[1])/6,a[1],p[1]),...p)}
 return result
}
function curvePath(curve){if(!curve.length)return '';let path=`M${curve[0]},${curve[1]}`;for(let i=2;i<curve.length;i+=6)path+=` C${curve[i]},${curve[i+1]} ${curve[i+2]},${curve[i+3]} ${curve[i+4]},${curve[i+5]}`;return path}
function subdivideCurve(curve,count){
 const originalCount=(curve.length-2)/6;
 if(count===originalCount)return curve;
 const roots=[],heap=[],mid=(a,b)=>[(a[0]+b[0])/2,(a[1]+b[1])/2];
 const make=points=>({points,length:Math.hypot(points[1][0]-points[0][0],points[1][1]-points[0][1])+Math.hypot(points[2][0]-points[1][0],points[2][1]-points[1][1])+Math.hypot(points[3][0]-points[2][0],points[3][1]-points[2][1])});
 const push=node=>{let i=heap.length;heap.push(node);while(i){const parent=(i-1)>>1;if(heap[parent].length>=node.length)break;heap[i]=heap[parent];i=parent}heap[i]=node};
 const pop=()=>{const first=heap[0],last=heap.pop();if(heap.length){let i=0;while(i*2+1<heap.length){let child=i*2+1;if(child+1<heap.length&&heap[child+1].length>heap[child].length)child++;if(heap[child].length<=last.length)break;heap[i]=heap[child];i=child}heap[i]=last}return first};
 let start=curve.slice(0,2);
 for(let i=2;i<curve.length;i+=6){const node=make([start,curve.slice(i,i+2),curve.slice(i+2,i+4),curve.slice(i+4,i+6)]);roots.push(node);push(node);start=node.points[3]}
 if(!roots.length&&count){const node=make(Array.from({length:4},()=>curve.slice(0,2)));roots.push(node);push(node)}
 // Longest-leaf lookup is logarithmic. Keep the subdivision tree so output
 // follows original segment order without a costly sort or repeated scans.
 for(let leaves=roots.length;leaves<count;leaves++){const node=pop(),[a,b,c,d]=node.points,ab=mid(a,b),bc=mid(b,c),cd=mid(c,d),abc=mid(ab,bc),bcd=mid(bc,cd),center=mid(abc,bcd);node.left=make([a,ab,abc,center]);node.right=make([center,bcd,cd,d]);push(node.left);push(node.right)}
 const result=curve.slice(0,2),stack=roots.slice().reverse();
 while(stack.length){const node=stack.pop();if(node.left){stack.push(node.right,node.left)}else{const p=node.points;result.push(...p[1],...p[2],...p[3])}}
 return result
}
function useCurveMotion(stroke,area,points,baseline,hasArea=false){
 const preference=useMotionPreference(),current=useRef(null),targetCurve=curveCoordinates(points),signature=curvePath(targetCurve);
 useLayoutEffect(()=>{
  const node=stroke.current;if(!node)return;
  const paint=frame=>{const path=curvePath(frame.curve);node.setAttribute('d',path);if(area.current)area.current.setAttribute('d',path?`${path} L${frame.curve.at(-2)},${frame.baseline} L${frame.curve[0]},${frame.baseline} Z`:'')};
  const target={curve:targetCurve,baseline},previous=current.current;
  if(!previous?.curve.length||!targetCurve.length||preference){current.current=target;paint(target);return}
  const count=Math.max((previous.curve.length-2)/6,(targetCurve.length-2)/6),from=subdivideCurve(previous.curve,count),to=subdivideCurve(targetCurve,count),progress={value:0};
  // Paint newly attached area nodes from the same visible geometry as the stroke.
  paint(previous);
  // React never writes d. Interrupted transitions start at the last painted frame.
  const animation=gsap.to(progress,{value:1,...motionOptions('chart'),ease:'power2.inOut',onUpdate(){const f=progress.value;current.current={curve:from.map((v,i)=>v+(to[i]-v)*f),baseline:previous.baseline+(baseline-previous.baseline)*f};paint(current.current)},onComplete(){current.current=target;paint(target)}});
  return()=>animation.kill()
 },[signature,baseline,hasArea,preference]);
}
function AnimatedCurve({points,color,fill,baseline}){useLocale();const stroke=useRef(null),area=useRef(null);useCurveMotion(stroke,area,points,baseline,Boolean(fill));return <>{tr(fill&&<path ref={area} className="data-area motion-data-area" fill={fill}/>)}<path ref={stroke} className="data-stroke motion-data-stroke" fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinecap="round" strokeLinejoin="round"/></>}
export function Sparkline({values,timestamps=null,color='var(--green)',bounds=false,maxValue=100,spanMs=null,expectedMs,breakBefore=null,sampleIntervals=null}){useLocale();
 const stroke=useRef(null),area=useRef(null),scrollTarget=useRef(null),scaleTarget=useRef(null),scale=Math.max(Number.EPSILON,maxValue),end=timestamps?.at(-1),windowMs=spanMs??Math.max(1000,Math.round(gapLimit(timestamps||[],expectedMs)/2500)*1000)*31,domain=[end-windowMs,end],id=React.useId().replace(/:/g,'');
 const points=values.map((v,i)=>[timestamps?.length?(timestamps[i]-domain[0])/windowMs*112:i/Math.max(1,values.length-1)*112,Number.isFinite(v)?29-Math.max(0,Math.min(scale,v))/scale*25:null]);
 useCurveMotion(stroke,area,timestamps?[]:points.filter(p=>p[1]!=null),29);
 if(timestamps?.length)return <span className="sparkline motion-sparkline compositor-sparkline" aria-hidden="true"><svg className="sparkline-fixed" viewBox="0 0 112 32" preserveAspectRatio="none"><g className="sparkline-guides" style={{display:bounds?undefined:'none'}}><line x1="0" x2="112" y1="3" y2="3"/><line x1="0" x2="112" y1="29" y2="29"/></g></svg><span className="sparkline-clip"><span ref={scrollTarget} className="sparkline-scroll"><span ref={scaleTarget} className="sparkline-scale"><svg viewBox="0 0 112 32" preserveAspectRatio="none"><TimelineCurve points={points} timestamps={timestamps} domain={domain} color={color} baseline={29} valueScale={maxValue} scrollTarget={scrollTarget} scaleTarget={scaleTarget} live plotWidth={112} expectedMs={expectedMs} breakBefore={breakBefore} sampleIntervals={sampleIntervals}/></svg></span></span></span></span>;
 return <svg className="sparkline motion-sparkline" viewBox="0 0 112 32" aria-hidden="true" preserveAspectRatio="none"><defs><clipPath id={'spark-'+id}><rect x="0" y="0" width="112" height="32"/></clipPath></defs><g className="sparkline-guides" style={{display:bounds?undefined:'none'}}><line x1="0" x2="112" y1="3" y2="3"/><line x1="0" x2="112" y1="29" y2="29"/></g>{tr(timestamps?.length?<g clipPath={'url(#spark-'+id+')'}><TimelineCurve points={points} timestamps={timestamps} domain={domain} color={color} baseline={29} valueScale={maxValue} live plotWidth={112} expectedMs={expectedMs} breakBefore={breakBefore} sampleIntervals={sampleIntervals}/></g>:<path ref={stroke} fill="none" stroke={color} strokeWidth="1.7" vectorEffect="non-scaling-stroke"/>)}</svg>
}

const chartClock=new Intl.DateTimeFormat(locale(),{hour:'2-digit',minute:'2-digit',hourCycle:'h23'});
const historyDayLabel=new Intl.DateTimeFormat(locale(),{month:'2-digit',day:'2-digit'}),historyYearLabel=new Intl.DateTimeFormat(locale(),{year:'numeric',month:'2-digit',day:'2-digit'});
export function Chart({series,height=220,onSelect,selected=null,compact=false,startMinute=540,unit='%',rangeMinutes=15,startDate='2026-10-28',selectionPopover=null,timestamps=null,maxValue=100,axisFormat=null,live=false,expectedInterval=null,breakBefore=null,sampleIntervals=null,scalable=false,showScaleControl=true,calendarAxis=false,hideHoverTooltip=false,timeDomain=null,renderOverlay=null}){useLocale();
 const [axisHost,setAxisHost]=useState(null);useLayoutEffect(()=>{setAxisHost(host.current?.parentElement?.matches('.resource-chart')?host.current.parentElement.querySelector(':scope > .section-title'):null)},[]);
 const [hover,setHover]=useState(null),host=useRef(null),svg=useRef(null),raf=useRef(null),pending=useRef(null);const [chartWidth,setChartWidth]=useState(900);const w=chartWidth,h=height,left=42,right=14,top=30,bottom=30,n=series[0]?.values.length||1;useEffect(()=>{const observer=new ResizeObserver(entries=>{const width=Math.round(entries[0].contentRect.width);if(width>100)setChartWidth(width)});if(host.current)observer.observe(host.current);return()=>observer.disconnect()},[]);const chartId=React.useId().replace(/:/g,'');
 const timed=!!timestamps?.length,end=timed?timestamps.at(-1):0,domain=timed?(timeDomain??[live?end-rangeMinutes*60000:timestamps[0],end]):[0,1],span=Math.max(1,domain[1]-domain[0]);
 const firstDate=new Date(domain[0]),lastDate=new Date(domain[1]),dated=calendarAxis&&timed&&firstDate.toDateString()!==lastDate.toDateString();
 const dateLabel=firstDate.getFullYear()!==lastDate.getFullYear()?historyYearLabel:historyDayLabel,clock=chartClock;
 const axisMode=useSyncExternalStore(subscribeChartScale,getChartScale),axisState=useRef(null),scaleKey=series.map(s=>s.name).join('|')+'|'+rangeMinutes+'|'+axisMode;
 if(scalable){
  const visible=series.flatMap(s=>s.values.filter((v,i)=>!timed||timestamps[i]>=domain[0]));
  const proposed=axisMode==='full'?100:percentageMaximum(visible),previous=axisState.current;
  if(!live||!previous||previous.key!==scaleKey||proposed>previous.max)axisState.current={key:scaleKey,max:proposed,smallerSince:null};
  else if(proposed<previous.max){if(previous.smallerSince==null)previous.smallerSince=end||Date.now();if((end||Date.now())-previous.smallerSince>=30000)axisState.current={key:scaleKey,max:proposed,smallerSince:null}}
  else previous.smallerSince=null;
  maxValue=axisState.current.max;
 }
 const axisTicks=scalable?percentageTicks(maxValue):[0,.25,.5,.75,1].map(f=>maxValue*f);
 const points=useMemo(()=>series.map(s=>s.values.map((v,i)=>[left+(timed?(timestamps[i]-domain[0])/span:i/Math.max(1,s.values.length-1))*(w-left-right),Number.isFinite(v)?top+(maxValue-Math.max(0,Math.min(maxValue,v)))/maxValue*(h-top-bottom):null])),[series,h,w,timestamps,maxValue,domain[0],span]);
 useEffect(()=>()=>{if(raf.current)cancelAnimationFrame(raf.current)},[]);
 function fractionAt(e){const r=host.current.getBoundingClientRect();const x=(e.clientX-r.left)/r.width*w;return (x-left)/(w-left-right)}
 function timeAt(e){return domain[0]+fractionAt(e)*span}
 function xAt(time){return left+(time-domain[0])/span*(w-left-right)}
 function pick(e){const fraction=Math.max(0,Math.min(1,fractionAt(e)));if(timestamps?.length){const target=domain[0]+fraction*span;let lo=0,hi=timestamps.length-1;while(lo<hi){const mid=Math.floor((lo+hi)/2);if(timestamps[mid]<target)lo=mid+1;else hi=mid}return lo>0&&target-timestamps[lo-1]<timestamps[lo]-target?lo-1:lo}return Math.round(fraction*(n-1))}
 function clearHover(){if(raf.current)cancelAnimationFrame(raf.current);raf.current=null;pending.current=null;setHover(null)}
 function move(e){pending.current=e.clientX;if(!raf.current)raf.current=requestAnimationFrame(()=>{setHover(pick({clientX:pending.current}));raf.current=null})}
 const selectedPx=selected===null?0:points[0]?.[selected]?.[0]??left,peekWidth=Math.min(376,w-24),peekLeft=Math.max(12,Math.min(w-peekWidth-12,selectedPx+(selectedPx<w/2?14:-peekWidth-14)));
 const index=hover??selected,px=index==null?0:points[0]?.[index]?.[0]??left;const dateFor=i=>timestamps?.length?new Date(timestamps[Math.round(i)]):new Date(new Date(startDate+'T00:00:00').getTime()+(startMinute+i/Math.max(1,n-1)*rangeMinutes)*60000);const timeFor=i=>dateFor(i).toLocaleTimeString(locale(),{hour12:false});
 return <div className={`chart motion-chart ${compact?'compact-chart':''}`} ref={host} onPointerMove={move} onPointerLeave={clearHover} onClick={e=>onSelect?.(pick(e))} tabIndex={0} role={onSelect?'slider':'img'} aria-label={tr(onSelect?'历史时间图，左右键选择时间点':'资源趋势图，聚焦后用左右键查看数值')} aria-valuemin={onSelect?0:undefined} aria-valuemax={onSelect?n-1:undefined} aria-valuenow={onSelect?(selected??n-1):undefined} aria-valuetext={tr(onSelect?timeFor(selected??n-1):undefined)} onKeyDown={e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const next=e.key==='Home'?0:e.key==='End'?n-1:Math.max(0,Math.min(n-1,(index??n-1)+(e.key==='ArrowLeft'?-1:1)));setHover(next);onSelect?.(next)}}}>
 {tr(scalable&&showScaleControl&&(axisHost?createPortal(<AxisControl axisMode={axisMode} clearHover={clearHover} inHeading/>,axisHost):<AxisControl axisMode={axisMode} clearHover={clearHover}/>))}
 {/* axis control is outside the plot when a resource heading is present */}

 <svg ref={svg} viewBox={`0 0 ${w} ${h}`} style={{height}} preserveAspectRatio="none" aria-hidden={renderOverlay?undefined:true}><defs><clipPath id={`plot-${chartId}`}><rect x={left} y={top-1} width={w-left-right} height={h-top-bottom+2}/></clipPath>{series.map((s,i)=><linearGradient key={s.name} id={`area-${chartId}-${i}`} x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={s.color} stopOpacity=".13"/><stop offset="100%" stopColor={s.color} stopOpacity=".01"/></linearGradient>)}</defs>
 <text x={left} y="15" className="axis-label chart-axis-unit">{tr(unit)}</text>
 {tr(axisTicks.map(v=><g key={v}><line x1={left} x2={w-right} y1={top+(maxValue-v)/maxValue*(h-top-bottom)} y2={top+(maxValue-v)/maxValue*(h-top-bottom)} className="grid-line"/><text x={left-12} y={top+(maxValue-v)/maxValue*(h-top-bottom)+4} textAnchor="end" className="axis-label">{tr(axisFormat?axisFormat(v):Number(v.toFixed(1)))}</text></g>))}
 {points.map((p,i)=><g key={series[i].name} clipPath={`url(#plot-${chartId})`}>{tr(timed?<TimelineCurve points={p} timestamps={timestamps} domain={domain} color={series[i].color} fill={series.length<3?`url(#area-${chartId}-${i})`:null} baseline={h-bottom} live={live} compositorSize={live?{width:w,height:h}:null} plotWidth={w-left-right} expectedMs={expectedInterval?expectedInterval*1000:null} breakBefore={breakBefore} sampleIntervals={sampleIntervals}/>:<AnimatedCurve points={p} color={series[i].color} fill={series.length<3?`url(#area-${chartId}-${i})`:null} baseline={h-bottom}/>)}</g>)}
 {tr((calendarAxis&&dated&&w<590?[0,.5,1]:[0,.25,.5,.75,1]).map(f=>{const d=new Date(domain[0]+f*span),time=(timestamps&&timestamps.length===0?'—':timestamps?.length?clock.format(d):timeFor(f*(n-1)).slice(0,5)),x=left+f*(w-left-right);return <text key={f} x={x} y={h-5} textAnchor={f===0?'start':f===1?'end':'middle'} className="axis-label">{tr(dated?dateLabel.format(d)+' '+time:time)}</text>}))}
 {tr(renderOverlay?.({points:points[0],timestamps,left,top,bottom:h-bottom,pick,timeAt,xAt,clearHover}))}
 {tr(index!==null&&(!renderOverlay||hover!==null&&hover!==selected)&&<g pointerEvents="none"><line x1={px} x2={px} y1={top} y2={h-bottom} className="crosshair"/>{tr(points.map((p,i)=>Number.isFinite(p[index]?.[1])&&<circle key={i} cx={p[index]?.[0]} cy={p[index]?.[1]} r="4" fill={series[i].color} stroke="var(--surface)" strokeWidth="2"/>))}</g>)}</svg>
 {tr(hover!==null&&!hideHoverTooltip&&(!selectionPopover||hover!==selected)&&<div className="chart-tooltip" style={{left:`clamp(100px,${px/w*100}%,calc(100% - 110px))`}}><time>{tr(dateFor(hover).toLocaleDateString(locale(),{month:'long',day:'numeric'}))} {tr(timeFor(hover))}</time>{tr(series.map(s=><div key={s.name}><span className="legend-dot" style={{background:s.color}}/>{s.name}<strong>{tr(Number.isFinite(s.values[hover])?s.values[hover].toFixed(1)+' '+unit:'暂无数据')}</strong></div>))}</div>)}
 {tr(onSelect&&selected!==null&&<MotionSurface staticContent constrainTo=".main-content" open={Boolean(selectionPopover)} position={{left:peekLeft,top:12,originX:selectedPx-peekLeft,originY:Math.max(0,points[0]?.[selected]?.[1]-12)}} changeKey={selected+'/'+startDate+'/'+startMinute+'/'+rangeMinutes} className="chart-selection-popover" style={{width:peekWidth}} onClick={e=>e.stopPropagation()} onPointerMove={e=>e.stopPropagation()} onKeyDown={e=>{if(e.key!=='Escape')e.stopPropagation()}}>{tr(selectionPopover)}</MotionSurface>)}
 </div>
}
export function SectionTitle({children,meta,action}){useLocale();return <div className="section-title"><div><h2>{tr(children)}</h2>{tr(meta&&<span className="section-meta">{tr(meta)}</span>)}</div>{tr(action)}</div>}
export function Empty({icon:Icon,title,description,action}){useLocale();return <div className="empty-state"><div className="empty-icon"><Icon size={28}/></div><h3>{tr(title)}</h3><p>{tr(description)}</p>{tr(action)}</div>}
export function Modal({title,children,onClose,wide=false,className='',layout='custom'}){useLocale();const ref=useRef(null);const layer=useDismissableLayer({refs:[ref],onDismiss:onClose});useEffect(()=>{const previous=document.activeElement;const handler=e=>{if(e.defaultPrevented||document.activeElement?.closest('[role="dialog"]')!==ref.current)return;if(e.key==='Tab'&&ref.current){const all=[...ref.current.querySelectorAll('button:not(:disabled),input,select,textarea,[tabindex="0"]')].filter(x=>x.offsetParent!==null);if(!all.length)return;const first=all[0],last=all.at(-1);if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus()}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus()}}};document.addEventListener('keydown',handler);ref.current?.querySelector('button,input')?.focus({preventScroll:true});return ()=>{document.removeEventListener('keydown',handler);previous?.focus({preventScroll:true})}},[]);return <div className="modal-backdrop"><section className={`modal ${wide?'wide':''} ${className}`} ref={ref} role="dialog" data-layout={layout} aria-modal="true" aria-label={tr(title)}><header><h2>{tr(title)}</h2><IconButton icon={X} label={t("关闭窗口")} onClick={onClose}/></header>{tr(layout==='form'?<DialogContent>{tr(children)}</DialogContent>:children)}</section></div>}
export function FormDialog(props){useLocale();return <Modal {...props} layout="form"/>}
export function Dropdown({label,children,icon:Icon=ChevronDown,className=''}){useLocale();
 const [open,setOpen]=useState(false),ref=useRef(null);
 useDismissableLayer({open,refs:[ref],onDismiss:()=>setOpen(false)});
 return <div className={`dropdown ${className}`} ref={ref}><button type="button" className={label?'button quiet':'icon-button'} aria-label={tr(label||'更多操作')} aria-haspopup="menu" aria-expanded={open} onClick={()=>setOpen(!open)}>{tr(label&&<span className="control-label">{tr(label)}</span>)}<Icon size={label?15:18}/></button>{tr(open&&<div className="dropdown-menu" role="menu" onClick={()=>setOpen(false)}>{tr(children)}</div>)}</div>;
}


function AxisControl({axisMode,clearHover,inHeading=false}){useLocale();return <div className={'chart-axis-control'+(inHeading?' in-heading':'')} onPointerEnter={clearHover} onFocusCapture={clearHover} onClick={e=>e.stopPropagation()} onPointerMove={e=>e.stopPropagation()} onKeyDown={e=>e.stopPropagation()}><ThemedSelect aria-label={t("曲线纵轴范围")} value={axisMode} onChange={e=>setChartScale(e.target.value)}><option value="auto">{t("自动范围")}</option><option value="full">{t("固定 0–100%")}</option></ThemedSelect></div>}
