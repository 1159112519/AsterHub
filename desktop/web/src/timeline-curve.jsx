import {t,tr,useLocale} from './i18n/index.js';
import React,{useLayoutEffect,useMemo,useRef,useSyncExternalStore} from 'react';
import {reducedMotion,subscribeReducedMotion} from './motion';
import {splitTimedPoints,timedPaths,gapLimit,compactTimedSegments} from './time-series';
export function TimelineCurve({points,timestamps,domain,color,fill,baseline,live=false,plotWidth,expectedMs,breakBefore=null,sampleIntervals=null,valueScale=null,scrollTarget=null,scaleTarget=null,compositorSize=null}){useLocale();
 const group=useRef(null),scaleGroup=useRef(null),scalePrior=useRef(null),previous=useRef(null),preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const span=domain[1]-domain[0];
 // Hover and snapshot selection do not change geometry. Keep large archived paths stable.
 const paths=useMemo(()=>{
  const cadence=expectedMs??sampleIntervals?.findLast(v=>v>0),limit=gapLimit(timestamps,cadence),explicitArchive=!live&&breakBefore!==null;
  const segments=splitTimedPoints(points,timestamps,explicitArchive?Infinity:limit,breakBefore,Math.min(12000,limit),sampleIntervals);
  return timedPaths(!live&&points.length>plotWidth*1.5?compactTimedSegments(segments):segments,baseline);
 },[points,timestamps,breakBefore,sampleIntervals,expectedMs,baseline,live,plotWidth]);
 // Vertical scaling has its own animation owner, independent of timeline scrolling.
 useLayoutEffect(()=>{
  const node=scaleTarget?.current||scaleGroup.current,old=scalePrior.current;scalePrior.current=valueScale;
  if(!node||preference||!(old>0)||!(valueScale>0)||old===valueScale||!node.animate)return;
  const ratio=valueScale/old;
  const from=scaleTarget?`scaleY(${ratio})`:`translateY(${baseline*(1-ratio)}px) scaleY(${ratio})`;
  const animation=node.animate([{transform:from},{transform:'none'}],{duration:850,easing:'cubic-bezier(.45,0,.55,1)'});
  return()=>{const progress=animation.effect.getComputedTiming().progress;const factor=Number.isFinite(progress)?ratio+(1-ratio)*progress:1;scalePrior.current=valueScale/factor;animation.cancel();};
 },[valueScale,baseline,preference,scaleTarget]);
 const signature=domain.join(',')+'|'+plotWidth+'|'+baseline;
 // Scale interpolation changes path heights without cancelling timeline movement.
 useLayoutEffect(()=>{
  const node=scrollTarget?.current||group.current,old=previous.current,end=domain[1],first=timestamps[0],motionWidth=scrollTarget?node?.clientWidth:compositorSize?(node?.clientWidth/compositorSize.width*plotWidth):plotWidth;
  const append=live&&old&&old.span===span&&old.width===plotWidth&&old.motionWidth===motionWidth&&end>old.end&&old.end>=first&&timestamps.includes(old.end);
  previous.current={end,span,width:plotWidth,motionWidth};
  // Let Chromium animate one transform. Read the visible position only when a new sample interrupts it.
  if(!append||preference||!node.animate)return;
  const dx=(end-old.end)/span*motionWidth+(old.remainder||0);
  const animation=node.animate([{transform:`translateX(${dx}px)`},{transform:'none'}],{duration:460,easing:'cubic-bezier(.45,0,.55,1)'});
  return()=>{const progress=animation.effect.getComputedTiming().progress;previous.current.remainder=Number.isFinite(progress)?dx*(1-progress):0;animation.cancel();};
 },[signature,preference,live,scrollTarget]);
 if(compositorSize)return <foreignObject x="0" y="0" width={compositorSize.width} height={compositorSize.height} style={{overflow:'visible',pointerEvents:'none'}}><div ref={group} className="timeline-compositor-scroll" style={{width:'100%',height:'100%'}}><svg width="100%" height="100%" viewBox={`0 0 ${compositorSize.width} ${compositorSize.height}`} preserveAspectRatio="none" style={{display:'block',overflow:'visible'}}><g ref={scaleGroup}>{tr(fill&&<path className="data-area motion-data-area" d={paths.area} fill={fill}/>)}<path className="data-stroke motion-data-stroke" d={paths.stroke} fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinecap="round" strokeLinejoin="round"/></g></svg></div></foreignObject>;
 return <g ref={group} data-motion-kind={live?'timeline-scroll':'history-static'}><g ref={scaleGroup} data-motion-kind="value-scale">{tr(fill&&<path className="data-area motion-data-area" d={paths.area} fill={fill}/>)}<path className="data-stroke motion-data-stroke" d={paths.stroke} fill="none" stroke={color} strokeWidth="2" vectorEffect="non-scaling-stroke" strokeLinecap="round" strokeLinejoin="round"/></g></g>;
}
