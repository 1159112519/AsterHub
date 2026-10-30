import {ThemedSelect} from './themed-select';
import {getChartScale,setChartScale,subscribeChartScale,percentageMaximum,percentageTicks} from './chart-scale';
import {TimelineCurve} from './timeline-curve';
import {gapLimit} from './time-series';
import {MotionSurface} from './motion-surface';
import {motionOptions,reducedMotion,subscribeReducedMotion} from './motion';
import './motion-preview5.css';
import React,{useEffect,useState,useRef,useMemo,useLayoutEffect,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {X,ChevronDown,ArrowUpRight,MoreHorizontal} from 'lucide-react';
import {minutesLabel} from './data';
export function IconButton({icon:Icon,label,onClick,className='',...props}){return <button type="button" aria-label={label} title={label} className={`icon-button ${className}`} onClick={onClick} {...props}><Icon size={18}/></button>}
export function Button({children,icon:Icon,tone='',className='',...props}){return <button type="button" className={`button ${tone} ${className}`} {...props}>{Icon&&<Icon size={16}/>}<span>{children}</span></button>}
export function NumberValue(props){return Number.isFinite(props.value)?<AnimatedNumber {...props}/>:<span className={`number ${props.className||''}`}>—</span>}
function useMotionPreference(){return useSyncExternalStore(subscribeReducedMotion,reducedMotion)}
function AnimatedNumber({value,decimals=0,suffix='',className=''}){
 const preference=useMotionPreference(),ref=useRef(null),current=useRef(value),initialText=useRef(value.toFixed(decimals));
 useLayoutEffect(()=>{if(preference){current.current=value;if(ref.current)ref.current.textContent=value.toFixed(decimals);return}const obj={v:current.current};if(ref.current)ref.current.textContent=obj.v.toFixed(decimals);const t=gsap.to(obj,{v:value,...motionOptions('value'),ease:'power2.inOut',onUpdate(){current.current=obj.v;if(ref.current)ref.current.textContent=obj.v.toFixed(decimals)}});return()=>t.kill()},[value,decimals,preference]);
 return <span className={`number ${className}`}><span ref={ref}>{initialText.current}</span>{suffix&&<small>{suffix}</small>}</span>
}
export function Meter({value,color='var(--green)',label}){
 const preference=useMotionPreference(),fill=useRef(null),echo=useRef(null),mounted=useRef(false),positions=useRef([0,0]),ratio=Math.max(0,Math.min(Number.isFinite(value)?value:0,100))/100;
 useLayoutEffect(()=>{const nodes=[fill.current,echo.current],paint=(i,v)=>{positions.current[i]=v;nodes[i].style.transform=`scaleX(${v})`};if(!mounted.current||preference){nodes.forEach((_,i)=>paint(i,ratio));mounted.current=true;return}const animations=nodes.map((node,i)=>{const state={v:positions.current[i]};return gsap.to(state,{v:ratio,...motionOptions(i?'meterEcho':'meter'),ease:'power2.inOut',onUpdate(){paint(i,state.v)}})});return()=>animations.forEach(t=>t.kill())},[ratio,preference]);
 return <div className="meter motion-meter" aria-label={label} role="meter" aria-valuenow={Number.isFinite(value)?value:undefined} aria-valuemin={0} aria-valuemax={100}><span ref={echo} className="meter-echo" style={{background:color}}/><span ref={fill} style={{background:color}}/></div>
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
function AnimatedCurve({points,color,fill,baseline}){const stroke=useRef(null),area=useRef(null);useCurveMotion(stroke,area,points,baseline,Boolean(fill));return <>{fill&&<path ref={area} className="data-area motion-data-area" fill={fill}/>}<path ref={stroke} className="data-stroke motion-data-stroke" fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinecap="round" strokeLinejoin="round"/></>}
export function Sparkline({values,timestamps=null,color='var(--green)',bounds=false,maxValue=100,spanMs=null}){
 const stroke=useRef(null),area=useRef(null),scale=Math.max(Number.EPSILON,maxValue),end=timestamps?.at(-1),windowMs=spanMs??Math.max(1000,Math.round(gapLimit(timestamps||[])/3000)*1000)*31,domain=[end-windowMs,end],id=React.useId().replace(/:/g,'');
 const points=values.map((v,i)=>[timestamps?.length?(timestamps[i]-domain[0])/windowMs*112:i/Math.max(1,values.length-1)*112,Number.isFinite(v)?29-Math.max(0,Math.min(scale,v))/scale*25:null]);
 useCurveMotion(stroke,area,timestamps?[]:points.filter(p=>p[1]!=null),29);
 return <svg className="sparkline motion-sparkline" viewBox="0 0 112 32" aria-hidden="true" preserveAspectRatio="none"><defs><clipPath id={'spark-'+id}><rect x="0" y="0" width="112" height="32"/></clipPath></defs><g className="sparkline-guides" style={{display:bounds?undefined:'none'}}><line x1="0" x2="112" y1="3" y2="3"/><line x1="0" x2="112" y1="29" y2="29"/></g>{timestamps?.length?<g clipPath={'url(#spark-'+id+')'}><TimelineCurve points={points} timestamps={timestamps} domain={domain} color={color} baseline={29} live plotWidth={112}/></g>:<path ref={stroke} fill="none" stroke={color} strokeWidth="1.7" vectorEffect="non-scaling-stroke"/>}</svg>
}

export function Chart({series,height=220,onSelect,selected=null,compact=false,startMinute=540,unit='%',rangeMinutes=15,startDate='2026-10-28',selectionPopover=null,timestamps=null,maxValue=100,axisFormat=null,live=false,expectedInterval=null,breakBefore=null,scalable=false}){
 const [hover,setHover]=useState(null),host=useRef(null),svg=useRef(null),raf=useRef(null),pending=useRef(null);const [chartWidth,setChartWidth]=useState(900);const w=chartWidth,h=height,left=42,right=14,top=30,bottom=30,n=series[0]?.values.length||1;useEffect(()=>{const observer=new ResizeObserver(entries=>{const width=Math.round(entries[0].contentRect.width);if(width>100)setChartWidth(width)});if(host.current)observer.observe(host.current);return()=>observer.disconnect()},[]);const chartId=React.useId().replace(/:/g,'');
 const timed=!!timestamps?.length,end=timed?timestamps.at(-1):0,domain=timed?[live?end-rangeMinutes*60000:timestamps[0],end]:[0,1],span=Math.max(1,domain[1]-domain[0]);
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
 const points=useMemo(()=>series.map(s=>s.values.map((v,i)=>[left+(timed?(timestamps[i]-domain[0])/span:i/Math.max(1,s.values.length-1))*(w-left-right),Number.isFinite(v)?top+(maxValue-Math.max(0,Math.min(maxValue,v)))/maxValue*(h-top-bottom):null])),[series,h,w,timestamps,maxValue,live,rangeMinutes]);
 useEffect(()=>()=>{if(raf.current)cancelAnimationFrame(raf.current)},[]);
 function pick(e){const r=host.current.getBoundingClientRect();const x=(e.clientX-r.left)/r.width*w;const fraction=Math.max(0,Math.min(1,(x-left)/(w-left-right)));if(timestamps?.length){const target=domain[0]+fraction*span;let lo=0,hi=timestamps.length-1;while(lo<hi){const mid=Math.floor((lo+hi)/2);if(timestamps[mid]<target)lo=mid+1;else hi=mid}return lo>0&&target-timestamps[lo-1]<timestamps[lo]-target?lo-1:lo}return Math.round(fraction*(n-1))}
 function move(e){pending.current=e.clientX;if(!raf.current)raf.current=requestAnimationFrame(()=>{setHover(pick({clientX:pending.current}));raf.current=null})}
 const selectedPx=selected===null?0:points[0]?.[selected]?.[0]??left,peekWidth=Math.min(376,w-24),peekLeft=Math.max(12,Math.min(w-peekWidth-12,selectedPx+(selectedPx<w/2?14:-peekWidth-14)));
 const index=hover??selected,px=index==null?0:points[0]?.[index]?.[0]??left;const dateFor=i=>timestamps?.length?new Date(timestamps[Math.round(i)]):new Date(new Date(startDate+'T00:00:00').getTime()+(startMinute+i/Math.max(1,n-1)*rangeMinutes)*60000);const timeFor=i=>dateFor(i).toLocaleTimeString('zh-CN',{hour12:false});
 return <div className={`chart motion-chart ${compact?'compact-chart':''}`} ref={host} onPointerMove={move} onPointerLeave={()=>{if(raf.current)cancelAnimationFrame(raf.current);raf.current=null;pending.current=null;setHover(null)}} onClick={e=>onSelect?.(pick(e))} tabIndex={0} role={onSelect?'slider':'img'} aria-label={onSelect?'历史时间图，左右键选择时间点':'资源趋势图，聚焦后用左右键查看数值'} aria-valuemin={onSelect?0:undefined} aria-valuemax={onSelect?n-1:undefined} aria-valuenow={onSelect?(selected??n-1):undefined} aria-valuetext={onSelect?timeFor(selected??n-1):undefined} onKeyDown={e=>{if(['ArrowLeft','ArrowRight','Home','End'].includes(e.key)){e.preventDefault();const next=e.key==='Home'?0:e.key==='End'?n-1:Math.max(0,Math.min(n-1,(index??n-1)+(e.key==='ArrowLeft'?-1:1)));setHover(next);onSelect?.(next)}}}>
 {scalable&&<div className="chart-axis-control" onClick={e=>e.stopPropagation()} onPointerMove={e=>e.stopPropagation()} onKeyDown={e=>e.stopPropagation()}><ThemedSelect aria-label="曲线纵轴范围" value={axisMode} onChange={e=>setChartScale(e.target.value)}><option value="auto">自动范围</option><option value="full">固定 0–100%</option></ThemedSelect></div>}
 <svg ref={svg} viewBox={`0 0 ${w} ${h}`} style={{height}} preserveAspectRatio="none" aria-hidden="true"><defs><clipPath id={`plot-${chartId}`}><rect x={left} y={top-1} width={w-left-right} height={h-top-bottom+2}/></clipPath>{series.map((s,i)=><linearGradient key={s.name} id={`area-${chartId}-${i}`} x1="0" y1="0" x2="0" y2="1"><stop offset="0%" stopColor={s.color} stopOpacity=".13"/><stop offset="100%" stopColor={s.color} stopOpacity=".01"/></linearGradient>)}</defs>
 <text x={left} y="15" className="axis-label chart-axis-unit">{unit}</text>
 {axisTicks.map(v=><g key={v}><line x1={left} x2={w-right} y1={top+(maxValue-v)/maxValue*(h-top-bottom)} y2={top+(maxValue-v)/maxValue*(h-top-bottom)} className="grid-line"/><text x={left-12} y={top+(maxValue-v)/maxValue*(h-top-bottom)+4} textAnchor="end" className="axis-label">{axisFormat?axisFormat(v):Number(v.toFixed(1))}</text></g>)}
 {points.map((p,i)=><g key={series[i].name} clipPath={`url(#plot-${chartId})`}>{timed?<TimelineCurve points={p} timestamps={timestamps} domain={domain} color={series[i].color} fill={series.length<3?`url(#area-${chartId}-${i})`:null} baseline={h-bottom} live={live} plotWidth={w-left-right} expectedMs={expectedInterval?expectedInterval*1000:null} breakBefore={breakBefore}/>:<AnimatedCurve points={p} color={series[i].color} fill={series.length<3?`url(#area-${chartId}-${i})`:null} baseline={h-bottom}/>}</g>)}
 {[0,.25,.5,.75,1].map(f=><text key={f} x={left+f*(w-left-right)} y={h-5} textAnchor={f===0?'start':f===1?'end':'middle'} className="axis-label">{(timestamps&&timestamps.length===0?'—':timestamps?.length?new Date(domain[0]+f*span).toLocaleTimeString('zh-CN',{hour12:false}):timeFor(f*(n-1))).slice(0,5)}</text>)}
 {index!==null&&<g><line x1={px} x2={px} y1={top} y2={h-bottom} className="crosshair"/>{points.map((p,i)=>Number.isFinite(p[index]?.[1])&&<circle key={i} cx={p[index]?.[0]} cy={p[index]?.[1]} r="4" fill={series[i].color} stroke="var(--surface)" strokeWidth="2"/>)}</g>}</svg>
 {hover!==null&&(!selectionPopover||hover!==selected)&&<div className="chart-tooltip" style={{left:`clamp(100px,${px/w*100}%,calc(100% - 110px))`}}><time>{dateFor(hover).toLocaleDateString('zh-CN',{month:'long',day:'numeric'})} {timeFor(hover)}</time>{series.map(s=><div key={s.name}><span className="legend-dot" style={{background:s.color}}/>{s.name}<strong>{Number.isFinite(s.values[hover])?s.values[hover].toFixed(1)+' '+unit:'暂无数据'}</strong></div>)}</div>}
 {onSelect&&selected!==null&&<MotionSurface open={Boolean(selectionPopover)} position={{left:peekLeft,top:12,originX:selectedPx-peekLeft,originY:Math.max(0,points[0]?.[selected]?.[1]-12)}} changeKey={selected+'/'+startDate+'/'+startMinute+'/'+rangeMinutes} className="chart-selection-popover" style={{width:peekWidth}} onClick={e=>e.stopPropagation()} onPointerMove={e=>e.stopPropagation()} onKeyDown={e=>{if(e.key!=='Escape')e.stopPropagation()}}>{selectionPopover}</MotionSurface>}
 </div>
}
export function SectionTitle({children,meta,action}){return <div className="section-title"><div><h2>{children}</h2>{meta&&<span className="section-meta">{meta}</span>}</div>{action}</div>}
export function Empty({icon:Icon,title,description,action}){return <div className="empty-state"><div className="empty-icon"><Icon size={28}/></div><h3>{title}</h3><p>{description}</p>{action}</div>}
export function Modal({title,children,onClose,wide=false,className=''}){const ref=useRef(null);useEffect(()=>{const previous=document.activeElement;const handler=e=>{if(e.key==='Escape')onClose();if(e.key==='Tab'&&ref.current){const all=[...ref.current.querySelectorAll('button:not(:disabled),input,select,textarea,[tabindex="0"]')].filter(x=>x.offsetParent!==null);if(!all.length)return;const first=all[0],last=all.at(-1);if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus()}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus()}}};document.addEventListener('keydown',handler);ref.current?.querySelector('button,input')?.focus();return ()=>{document.removeEventListener('keydown',handler);previous?.focus()}},[]);return <div className="modal-backdrop" onMouseDown={e=>{if(e.target===e.currentTarget)onClose()}}><section className={`modal ${wide?'wide':''} ${className}`} ref={ref} role="dialog" aria-modal="true" aria-label={title}><header><h2>{title}</h2><IconButton icon={X} label="关闭窗口" onClick={onClose}/></header>{children}</section></div>}
export function Dropdown({label,children,icon:Icon=ChevronDown,className=''}){const [open,setOpen]=useState(false),ref=useRef(null);useEffect(()=>{function close(e){if(!ref.current?.contains(e.target)||e.key==='Escape')setOpen(false)}document.addEventListener('pointerdown',close);document.addEventListener('keydown',close);return ()=>{document.removeEventListener('pointerdown',close);document.removeEventListener('keydown',close)}},[]);return <div className={`dropdown ${className}`} ref={ref}><button className={label?'button quiet':'icon-button'} aria-label={label||'更多操作'} aria-haspopup="menu" aria-expanded={open} onClick={()=>setOpen(!open)}>{label&&<span>{label}</span>}<Icon size={label?15:18}/></button>{open&&<div className="dropdown-menu" role="menu" onClick={()=>setOpen(false)}>{children}</div>}</div>}
