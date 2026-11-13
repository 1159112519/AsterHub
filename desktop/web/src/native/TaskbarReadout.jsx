import {t,tr,useLocale} from '../i18n/index.js';
import {metricCss} from './taskbar-colors.mjs';
import React,{useLayoutEffect,useRef,useId,useState} from 'react';
import {Cpu,Microchip,MemoryStick,HardDrive,Thermometer,Zap,Fan,Download,Upload,Wifi,WifiOff,Activity} from 'lucide-react';
import {taskbarScrollValues,taskbarScrollPath} from './taskbar-scroll';
import {numberFrames} from '../number-motion';
import {moduleLabel} from './taskbar-composer';
import {taskbarMetrics} from './taskbar-layout';
import {taskbarDisplay,taskbarUnitFamily,taskbarGlyphWidth,taskbarNumberWidth,taskbarReadoutLeftShift,taskbarPlaceKey} from './taskbar-display';
const details=Object.fromEntries(taskbarMetrics.map(row=>[row[0],row]));
import {centerTaskbarInk,measureTaskbarInk,taskbarInkY} from './taskbar-ink';
const ease='cubic-bezier(.22,.74,.28,1)';
export const taskbarLabelFont='400 11.5px "Taskbar Inter","HarmonyOS Sans SC",sans-serif';
export const taskbarUnitFont='400 10.5px "Taskbar Inter",sans-serif';
export const taskbarDigitFont='500 12.5px "Taskbar Inter",sans-serif';
const labelFont=taskbarLabelFont,unitFont=taskbarUnitFont,numberFont=taskbarDigitFont;
// Shared 88 x 17 cell: installed slots, library and stage render the same component.
function Digit({value,x,y,numberMotion,phase}){useLocale();
 const next=useRef(null),old=useRef(null),previous=useRef(value),history=useRef({value,prior:value});
 if(history.current.value!==value)history.current={value,prior:history.current.value};const prior=history.current.prior;
 useLayoutEffect(()=>{const before=previous.current;previous.current=value;if(before===value||numberMotion==='direct')return;
  const options={duration:240,easing:ease};
  const incoming=next.current.animate(numberFrames(numberMotion,true,14),options);
  const outgoing=old.current.animate(numberFrames(numberMotion,false,14),options);
  return()=>{incoming.cancel();outgoing.cancel()};
 },[value,numberMotion]);
 return <g data-digit={value}><text ref={old} x={x} y={taskbarInkY(prior,numberFont,y,{x:phase.x+x,y:phase.y})} opacity="0" aria-hidden="true">{tr(prior)}</text><text ref={next} x={x} y={taskbarInkY(value,numberFont,y,{x:phase.x+x,y:phase.y})}>{tr(value)}</text></g>;
}
function Chart({kind,id,sample,points,cores,x,y,motion}){useLocale();
 const path=useRef(null),frame=useRef(0),last=useRef(0),scale=useRef(null),trackKey=useRef(null),lineClip=useId().replaceAll(':',''),lineMask=lineClip+'-fade';
 const ratio=Math.min(1,Math.max(0,(sample?.value??0)/100)),hasData=sample?.value!=null&&!sample.stale;
 const mode=kind==='bars'&&id==='cpu'&&cores?.length?'cores':kind==='bars'&&['gpu','memory','gpuMemory'].includes(id)?'ring':kind==='bars'&&sample?.unit==='%'&&id!=='cpu'?'ticks':'line';
 useLayoutEffect(()=>{
  if(mode!=='line'||!hasData||points?.length<2||!points){last.current=0;scale.current=null;trackKey.current=null;return;}
  if(!path.current)return;
  const key=id+'|'+sample.unit;if(trackKey.current!==key){scale.current=null;last.current=0;trackKey.current=key;}
  scale.current??=Math.max(1,Math.abs(points[0]));
  const p=taskbarScrollValues(points,sample.unit,scale.current),start=performance.now(),duration=Math.min(750,Math.max(120,(start-last.current)*.85)),animate=motion&&last.current>0;
  last.current=start;
  const tick=now=>{if(!path.current)return;const t=animate?1-Math.pow(1-Math.min(1,(now-start)/duration),3):1;
   path.current.setAttribute('d',taskbarScrollPath(p,x,y,t));path.current.dataset.scrollProgress=String(t);
   if(t<1)frame.current=requestAnimationFrame(tick);
  };tick(start);return()=>cancelAnimationFrame(frame.current);
 },[points,mode,hasData,motion,x,y,sample?.unit,id]);
 if(!hasData||mode==='line'&&!(points?.length>1))return <text x={x} y={y+3} className="taskbar-cell-unit taskbar-chart-empty">—</text>;
 if(mode==='ring')return <g className="taskbar-chart"><circle cx={x+9} cy={y-.5} r="4.5" opacity=".28"/><circle cx={x+9} cy={y-.5} r="4.5" pathLength="100" strokeDasharray={`${ratio*100} 100`} transform={`rotate(-90 ${x+9} ${y-.5})`}/></g>;
 if(mode==='ticks')return <g className="taskbar-chart">{tr(Array.from({length:10},(_,i)=><line key={i} x1={x+i*2.3} x2={x+i*2.3} y1={y-2} y2={y+3} opacity={i<ratio*10?1:.28}/>))}</g>;
 if(mode==='cores'){const n=Math.min(12,cores.length);return <g className="taskbar-chart-core">{tr(Array.from({length:n},(_,i)=>{const values=cores.slice(Math.floor(i*cores.length/n),Math.floor((i+1)*cores.length/n));const h=Math.max(1,Math.min(9,.09*values.reduce((a,b)=>a+b,0)/values.length));return <rect key={i} x={x+i*22/n} y={y+3-h} width={Math.max(1,22/n-1)} height={h}/>}))}</g>}
 return <g data-chart-motion="scroll"><defs><clipPath id={lineClip}><rect x={x} y={y-5.6} width="22" height="9.2"/></clipPath><linearGradient id={lineMask}><stop offset="0" stopColor="white" stopOpacity="0"/><stop offset=".09" stopColor="white"/><stop offset=".91" stopColor="white"/><stop offset="1" stopColor="white" stopOpacity="0"/></linearGradient><mask id={lineMask+'-mask'} maskUnits="userSpaceOnUse" x={x} y={y-6} width="22" height="11"><rect x={x} y={y-6} width="22" height="11" fill={`url(#${lineMask})`}/></mask></defs><g clipPath={`url(#${lineClip})`} mask={`url(#${lineMask}-mask)`}><path ref={path} className="taskbar-chart"/></g></g>;
}
const metricIcon={cpu:Cpu,cpuFrequency:Activity,cpuTemperature:Thermometer,gpu:Microchip,gpuFrequency:Activity,gpuMemory:Microchip,gpuTemperature:Thermometer,gpuPower:Zap,gpuFan:Fan,memory:MemoryStick,memoryUsed:MemoryStick,storage:HardDrive,storageRead:Download,storageWrite:Upload,ping:Wifi,loss:WifiOff,io:Activity};
// Reserve against the widest supported value and unit. Offsets can close a gap,
// but not bring the ink of neighboring parts into contact.
function readoutLayout(id,module,peer,width,mode,typography){
 const row=details[id],label=module?moduleLabel(module,row?.[2]||'未知'):row?.[2]||'—';
 const parts=module?.parts??['number'],numeric=parts.includes('number'),partOnly=mode==='part';
 const adjust=part=>{if(mode)return 0;const limit=part.endsWith('X')?6:2;return Math.max(-limit,Math.min(limit,(Number(typography?.global?.[part])||0)+(Number(typography?.metrics?.[id]?.[part])||0)))||0;};
 const labelInk=measureTaskbarInk(label,labelFont),labelWidth=label==='◇'?10.5:labelInk.width;
 const paired=!mode&&parts.length===1&&numeric&&peer?.parts.length===1&&peer.parts[0]==='number'&&(
  (id==='up'&&peer.metricId==='down')||(id==='down'&&peer.metricId==='up')||
  (id==='storageRead'&&peer.metricId==='storageWrite')||(id==='storageWrite'&&peer.metricId==='storageRead')||
  (id==='cpuFrequency'&&peer.metricId==='gpuFrequency')||(id==='gpuFrequency'&&peer.metricId==='cpuFrequency'));
 const peerRow=paired?details[peer.metricId]:null,peerLabel=paired?moduleLabel(peer,peerRow[2]):'';
 const network=id==='up'||id==='down';
 const gap=8,graphWidth=24,unitGap=4,minGap=4;
 const nudge=!mode&&!network?taskbarGlyphWidth('0'):0;
 const nameSpace=(name,w)=>w+(['↑','↓'].includes(name)?10:Math.max(7,parts[0]==='number'?nudge+minGap:0));
 const peerSpace=paired?nameSpace(peerLabel,peerLabel==='◇'?10.5:measureTaskbarInk(peerLabel,labelFont).width):0;
 const first=partOnly||!parts.length?0:Math.max(nameSpace(label,labelWidth),peerSpace);
 const reservedUnit=Math.max(0,...taskbarUnitFamily(id,row?.[3]||'').map(u=>measureTaskbarInk(u,unitFont).width),...(paired?taskbarUnitFamily(peer.metricId,peerRow[3]).map(u=>measureTaskbarInk(u,unitFont).width):[]));
 const other=parts.filter(part=>part!=='number').length*graphWidth+Math.max(0,parts.length-1)*gap;
 const fixedTrack=Math.max(0,Math.min(taskbarNumberWidth('88.8'),(partOnly?72:width)-first+nudge-other-reservedUnit-unitGap-2));
 function place(numberWidth,unitWidth){
  const numberTrack=partOnly?numberWidth:fixedTrack;
  const labelStart=adjust('labelX');
  let edge=partOnly?0:labelStart+labelWidth+minGap,nominal=first;
  const tracks=parts.map(part=>{
   if(part!=='number'){
    const x=Math.max(edge,nominal+adjust('graphX'));edge=x+graphWidth+minGap;nominal+=graphWidth+gap;return {part,x,w:graphWidth};
   }
   const x=Math.max(edge,nominal-nudge+adjust('numberX'));
   const numberEnd=x+numberTrack;
   const unitLeft=Math.max(numberEnd+3,nominal-nudge+numberTrack+unitGap+adjust('unitX'));
   const w=(partOnly?unitWidth:reservedUnit)>0?unitLeft-x+(partOnly?unitWidth:reservedUnit):numberTrack;
   const shift=mode?0:taskbarReadoutLeftShift(id,x,numberTrack,edge-minGap);
   edge=x+w+minGap;nominal+=numberTrack+unitGap+reservedUnit+gap-nudge;
   return {part,x:x-shift,w,start:numberEnd-numberWidth-shift,unitLeft:unitLeft-shift};
  });
  return {tracks,left:partOnly?0:labelStart,right:Math.max(partOnly?0:labelStart+labelWidth,...tracks.map(t=>t.x+t.w))};
 }
 return {row,label,labelInk,labelWidth,fixedTrack,adjust,place};
}
export function taskbarPreviewFootprint(module,peer,width,typography){
 const d=readoutLayout(module.metricId,module,peer,width,undefined,typography);
 return d.place(d.fixedTrack,0);
}
export function Readout({id,module,peer,snapshot,numberMotion='direct',chartMotion=true,points,width=88,typography,mode,colors}){useLocale();
 const root=useRef(null),[phase,setPhase]=useState({x:0,y:0});
 useLayoutEffect(()=>{let frame;const measure=()=>{const r=root.current?.getBoundingClientRect();if(!r)return;const d=globalThis.devicePixelRatio||1,next={x:((r.x*d)%1)/d,y:((r.y*d)%1)/d};setPhase(old=>Math.abs(old.x-next.x)+Math.abs(old.y-next.y)<.0001?old:next)};const queue=()=>{cancelAnimationFrame(frame);frame=requestAnimationFrame(measure)};measure();window.addEventListener('resize',queue);document.addEventListener('scroll',queue,true);document.fonts.addEventListener('loadingdone',queue);document.addEventListener('animationend',queue,true);document.addEventListener('transitionend',queue,true);return()=>{cancelAnimationFrame(frame);window.removeEventListener('resize',queue);document.removeEventListener('scroll',queue,true);document.fonts.removeEventListener('loadingdone',queue);document.removeEventListener('animationend',queue,true);document.removeEventListener('transitionend',queue,true)}},[]);
 id=module?.metricId??id;const row=details[id],sample=snapshot?.values?.[id],clip=useId().replace(/[^a-zA-Z0-9_-]/g,''),label=module?moduleLabel(module,row?.[2]||'未知'):row?.[2]||'—';
 const Icon=label==='◇'?(metricIcon[id]??Activity):null;
 const parts=module?.parts??['number'],numeric=parts.includes('number'),nameOnly=mode==='name',partOnly=mode==='part';
 const {labelInk,labelWidth,fixedTrack,adjust,place}=readoutLayout(id,module,peer,width,mode,typography);
 const {text,unit}=taskbarDisplay(sample,label,row?.[3]||'',()=>fixedTrack);
 const unitInk=measureTaskbarInk(unit,unitFont),numberWidth=taskbarNumberWidth(text);
 const {tracks,left,right}=place(numberWidth,unitInk.width),number=tracks.find(track=>track.part==='number');
 const start=number?.start??0,unitStart=(number?.unitLeft??0)+unitInk.left;
 const readWidth=number?.w??0,graphWidth=24;
 const hitTracks=tracks.map(({x,w})=>`${x.toFixed(3)}:${w.toFixed(3)}`).join(',');
 useLayoutEffect(()=>{const slot=root.current?.closest('.tb42-slot');if(!slot||mode)return;const ranges=hitTracks.split(',').filter(Boolean).map(s=>s.split(':').map(Number));
  slot.style.setProperty('--tb-ink-left',`${left-1}px`);
  slot.style.setProperty('--tb-ink-width',`${right-left+6}px`);
  for(let i=0;i<2;i++){const previous=ranges[i-1],next=ranges[i];const from=next?Math.max(0,next[0]-2):previous?previous[0]+previous[1]+4:0;
   slot.style.setProperty(`--tb-track${i}-x`,`${Math.min(width-14,from+2)}px`);slot.style.setProperty(`--tb-track${i}-w`,`${Math.min(Math.max(14,width-from-3),next?Math.max(14,next[1]+4):14)}px`);}
 },[hitTracks,width,mode,left,right]);
 const visibleWidth=nameOnly?Math.ceil(Math.max(14,labelWidth+4)):partOnly?Math.ceil((numeric?readWidth:graphWidth)+2):width;
 const namePosition=nameOnly&&!Icon?centerTaskbarInk(label,labelFont,visibleWidth/2,8.5):null;
 const labelX=namePosition?.x??labelInk.left+adjust('labelX');
 let cursor=start;
 return <svg ref={root} className="taskbar-mini-cell" data-custom-name={Boolean(colors?.[id]?.label)} viewBox={`0 0 ${visibleWidth} 17`} width={visibleWidth} height="17" style={{...metricCss(colors,id),width:visibleWidth,minWidth:visibleWidth,overflow:mode?undefined:'visible'}} role="img" aria-label={`${tr(row?.[1]||id||'空位置')} ${text} ${unit}`}>
  <defs><clipPath id={clip}><rect x={start-1} y=".5" width={numberWidth+2} height="16"/></clipPath></defs>
  {tr(!partOnly&&(Icon?<Icon x={nameOnly?(visibleWidth-10.5)/2:adjust('labelX')} y={nameOnly?3:3+adjust('label')} width={10.5} height={10.5} className="taskbar-metric-icon" strokeWidth={1.8}/>:<text className="taskbar-cell-label" x={labelX} y={taskbarInkY(label,labelFont,13.5+adjust('label'),{x:phase.x+labelX,y:phase.y})}>{tr(label)}</text>))}
  {tr(tracks.map(({part,x})=>part==='number'?null:<Chart key={part} kind={part} id={id} sample={sample} cores={snapshot?.cpuCores} points={points} x={x+.75} y={10+adjust('graph')} motion={chartMotion}/>))}
  {tr(numeric&&<><g className="taskbar-cell-number" clipPath={`url(#${clip})`}>{tr([...text].map((c,i)=>{const gx=cursor;cursor+=taskbarGlyphWidth(c);return <Digit key={taskbarPlaceKey(text,i)} value={c} x={gx} y={13.5+adjust('number')} phase={phase} numberMotion={sample?.animate===false?'direct':numberMotion}/>}))}</g>
  <text className="taskbar-cell-unit" x={unitStart} y={taskbarInkY(unit.replace('/',''),unitFont,13.5+adjust('unit'),{x:phase.x+unitStart,y:phase.y})}>{tr(unit)}</text></>)}

 </svg>;
}
