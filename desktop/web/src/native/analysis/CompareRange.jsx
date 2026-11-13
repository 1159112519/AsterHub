import {t,tr,useLocale,locale} from '../../i18n/index.js';
import React,{useRef} from 'react';

export function CompareRange({markers,points,timestamps,top,bottom,pick,timeAt,xAt,onChange,onCommit,clearHover}){useLocale();
 const drag=useRef(null), first=markers[0], last=markers.at(-1),band=markers.length===2;
 if(!first||!last||!points[last.index])return null;
 const start=xAt(first.displayTime??first.time),end=xAt(last.displayTime??last.time);
 const middle=(start+end)/2;
 function move(e){const d=drag.current;if(d?.id!==e.pointerId)return;e.stopPropagation();if(d.key==='band'){const delta=timeAt(e)-d.anchor;if(!d.moved&&Math.abs(e.clientX-d.x)<2)return;d.moved=true;onChange(d.key,{start:d.start,end:d.end,delta});}else onChange(d.key,pick(e));}
 function finish(e,update){if(drag.current?.id!==e.pointerId)return;e.stopPropagation();if(update)move(e);drag.current=null;onCommit?.();}
 return <g className="ev-compare-range">
  {band&&<rect data-ev-band="true" x={start} y={top} width={Math.max(0,end-start)} height={bottom-top} className="ev-compare-band"
   onPointerDown={e=>{if(e.button!==0)return;e.preventDefault();e.stopPropagation();clearHover();e.currentTarget.setPointerCapture(e.pointerId);drag.current={id:e.pointerId,key:'band',anchor:timeAt(e),x:e.clientX,start:first.displayTime??first.time,end:last.displayTime??last.time};}}
   onPointerMove={move} onPointerUp={e=>finish(e,true)} onPointerCancel={e=>finish(e,false)} onLostPointerCapture={e=>finish(e,false)} onClick={e=>e.stopPropagation()}/>}
  {tr(markers.map(marker=>{
   const dot=points[marker.index];if(!dot)return null;
   const isStart=marker.key==='start', [,y]=dot,x=xAt(marker.displayTime??marker.time);
   const min=isStart||!band?0:Math.max(0,first.index+1),max=isStart?last.index-1:points.length-1;
   const hitLeft=isStart||!band?x-10:Math.max(x-10,middle),hitRight=isStart?Math.min(x+10,middle):x+10;
   return <g key={marker.key} className="ev-time-marker" data-ev-marker={marker.key} data-time={marker.time} style={{color:marker.color}} tabIndex={0} role="slider" aria-label={tr(isStart?'对比起点':'对比终点')} aria-valuemin={min} aria-valuemax={max} aria-valuenow={marker.index} aria-valuetext={tr(new Date(timestamps[marker.index]).toLocaleTimeString(locale(),{hour12:false}))}
    onPointerDown={e=>{if(e.button!==0)return;e.preventDefault();e.stopPropagation();clearHover();e.currentTarget.focus({preventScroll:true});e.currentTarget.setPointerCapture(e.pointerId);drag.current={id:e.pointerId,key:marker.key};}}
    onPointerMove={e=>{e.stopPropagation();move(e)}} onPointerUp={e=>finish(e,true)} onPointerCancel={e=>finish(e,false)} onLostPointerCapture={e=>finish(e,false)} onClick={e=>e.stopPropagation()}
    onKeyDown={e=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;e.preventDefault();e.stopPropagation();clearHover();onChange(marker.key,e.key==='Home'?min:e.key==='End'?max:marker.index+(e.key==='ArrowLeft'?-1:1));onCommit?.();}}>
    <line x1={x} x2={x} y1={top} y2={bottom} stroke="currentColor" strokeWidth="1.25" strokeDasharray={isStart?'4 4':undefined} pointerEvents="none"/>
    {tr(Number.isFinite(y)&&<circle cx={x} cy={y} r="4.5" fill="currentColor" stroke="var(--surface)" strokeWidth="1.5" pointerEvents="none"/>)}
    <rect className="ev-time-grip" x={x-3.5} y={top+2} width={7} height={12} rx={3} fill="currentColor" pointerEvents="none"/>
    <rect data-ev-hit="true" x={hitLeft} y={top} width={hitRight-hitLeft} height={bottom-top} fill="transparent"/>
   </g>;
  }))}
 </g>;
}
