import React,{useLayoutEffect,useRef,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {motionOptions,reducedMotion,subscribeReducedMotion} from './motion';
import {splitTimedPoints,timedPaths,gapLimit} from './time-series';
export function TimelineCurve({points,timestamps,domain,color,fill,baseline,live=false,plotWidth,expectedMs,breakBefore=null}){
 const group=useRef(null),previous=useRef(null),displayEnd=useRef(null),preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const span=domain[1]-domain[0],paths=timedPaths(splitTimedPoints(points,timestamps,breakBefore?Infinity:gapLimit(timestamps,expectedMs),breakBefore),baseline);
 const signature=paths.stroke+'|'+domain.join(',')+'|'+plotWidth+'|'+baseline;
 useLayoutEffect(()=>{
  const node=group.current,old=previous.current,end=domain[1],first=timestamps[0];
  const append=live&&old&&old.span===span&&old.width===plotWidth&&end>old.end&&old.end>=first&&timestamps.includes(old.end);
  previous.current={end,span,width:plotWidth};
  if(!append||preference||displayEnd.current==null){displayEnd.current=end;node.setAttribute('transform','translate(0 0)');return}
  const state={end:displayEnd.current};
  const paint=()=>{displayEnd.current=state.end;node.setAttribute('transform',`translate(${(end-state.end)/span*plotWidth} 0)`)};
  paint();const tween=gsap.to(state,{end,...motionOptions('chart'),ease:'power2.inOut',onUpdate:paint,onComplete:()=>{displayEnd.current=end;node.setAttribute('transform','translate(0 0)')}});
  return()=>tween.kill();
 },[signature,preference,live]);
 return <g ref={group} data-motion-kind={live?'timeline-scroll':'history-static'}>{fill&&<path className="data-area motion-data-area" d={paths.area} fill={fill}/>}<path className="data-stroke motion-data-stroke" d={paths.stroke} fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinecap="round" strokeLinejoin="round"/></g>;
}
