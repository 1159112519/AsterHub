import React,{useLayoutEffect,useRef,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {TimelineCurve} from '../timeline-curve';
import {gapLimit,splitTimedPoints,timedPaths} from '../time-series';
import {motionOptions,reducedMotion,subscribeReducedMotion} from '../motion';

function resolvedColor(root,value){root.style.color=value;const result=getComputedStyle(root).color;root.style.removeProperty('color');return result}
function snapshot(g,node){
 const maxGap=gapLimit(g.timestamps,g.expectedMs);
 return g.points.map((points,i)=>({color:resolvedColor(node,g.series[i].color),opacity:1,segments:splitTimedPoints(points,g.timestamps,maxGap,g.breakBefore,Math.min(12000,maxGap),g.sampleIntervals)}));
}
const mix=(a,b,p)=>a+(b-a)*p;
function yAt(points,x){
 let lo=0,hi=points.length-1;
 if(x<=points[lo][0])return points[lo][1];if(x>=points[hi][0])return points[hi][1];
 while(hi-lo>1){const mid=(lo+hi)>>1;if(points[mid][0]<x)lo=mid;else hi=mid;}
 const [x0,y0]=points[lo],[x1,y1]=points[hi];return mix(y0,y1,(x-x0)/Math.max(1e-5,x1-x0));
}
// Morph only overlaps between real runs; fade unmatched runs without crossing a gap.
function pair(before,after){
 const previous=before?.segments??[],next=after?.segments??[];
 const blend=before&&after?gsap.utils.interpolate(before.color,after.color):null;
 const morphs=[],usedBefore=new Set(),usedAfter=new Set();
 for(const a of previous)for(const b of next){
  const start=Math.max(a[0][0],b[0][0]),end=Math.min(a.at(-1)[0],b.at(-1)[0]);
  if(end<=start)continue;
  usedBefore.add(a);usedAfter.add(b);
  const stops=[...new Set([start,end,...[...a,...b].map(pt=>pt[0]).filter(x=>x>start&&x<end)])].sort((x,y)=>x-y);
  const from=stops.map(x=>yAt(a,x)),to=stops.map(x=>yAt(b,x));
  morphs.push({shape:p=>stops.map((x,i)=>[x,mix(from[i],to[i],p)]),a,b,start,end});
 }
 const unmatched=(runs,used,key)=>runs.flatMap(run=>{
  if(!used.has(run))return [run];
  const matches=morphs.filter(m=>m[key]===run).sort((a,b)=>a.start-b.start);
  const cut=(l,r)=>l<r?[[l,yAt(run,l)],...run.filter(([x])=>x>l&&x<r),[r,yAt(run,r)]]:null;
  let start=run[0][0];const visible=[];
  for(const m of matches){const part=cut(start,m.start);if(part)visible.push(part);start=Math.max(start,m.end)}
  const tail=cut(start,run.at(-1)[0]);if(tail)visible.push(tail);return visible;
 });
 const restBefore=unmatched(previous,usedBefore,'a'),restAfter=unmatched(next,usedAfter,'b');
 return p=>[
  ...(morphs.length?[{color:blend(p),opacity:mix(before.opacity,1,p),segments:morphs.map(m=>m.shape(p))}]:[]),
  ...(restBefore.length?[{color:blend?.(p)??before.color,opacity:before.opacity*(1-p),segments:restBefore}]:[]),
  ...(restAfter.length?[{color:blend?.(p)??after.color,opacity:p,segments:restAfter}]:[]),
 ];
}
const ns='http://www.w3.org/2000/svg';
export function OverviewTrendSeries({metric,geometry}){
 const {points,series,timestamps,domain,left,right,baseline,width,height,clip,expectedMs,breakBefore,sampleIntervals,fills}=geometry;
 const root=useRef(null),live=useRef(null),layer=useRef(null),prior=useRef(null),shown=useRef(null),latest=useRef(geometry);
 latest.current=geometry;
 const reduced=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 useLayoutEffect(()=>{
  const display=root.current,overlay=layer.current,curve=live.current,old=prior.current;
  const final=snapshot(latest.current,display);
  if(!old||old.metric===metric||reduced||!final.some(v=>v.segments.length)||!old.series.some(v=>v.segments.length)){prior.current={metric,series:final};shown.current=null;return;}
  const first=shown.current||old.series;
  const interpolate=Array.from({length:Math.max(first.length,final.length)},(_,i)=>pair(first[i],final[i]));
  const panel=display.closest('.overview-focus-trend');panel?.setAttribute('data-switching','');
  overlay.replaceChildren();curve.setAttribute('visibility','hidden');
  const paths=[];
  function paint(p){
   const frame=interpolate.flatMap(fn=>fn(p));shown.current=frame;
   frame.forEach(({color,opacity,segments},i)=>{
    if(!paths[i]){
     const group=document.createElementNS(ns,'g'),area=document.createElementNS(ns,'path'),stroke=document.createElementNS(ns,'path');
     area.setAttribute('fill-opacity','.075');stroke.setAttribute('fill','none');stroke.setAttribute('stroke-width','2');stroke.setAttribute('stroke-linecap','round');stroke.setAttribute('stroke-linejoin','round');group.append(area,stroke);overlay.append(group);paths[i]={group,area,stroke};
    }
    const {group,area,stroke}=paths[i],d=timedPaths(segments,baseline);group.setAttribute('opacity',opacity);area.setAttribute('fill',color);area.setAttribute('d',d.area);stroke.setAttribute('stroke',color);stroke.setAttribute('d',d.stroke);
   });
  }
  function clear(){curve.removeAttribute('visibility');overlay.replaceChildren();panel?.removeAttribute('data-switching')}
  prior.current={metric,series:final};paint(0);
  const progress={value:0},tween=gsap.to(progress,motionOptions('drill',{value:1,ease:'power3.inOut',onUpdate:()=>paint(progress.value),onComplete:()=>{clear();shown.current=null;prior.current={metric,series:snapshot(latest.current,display)}}}));
  return()=>{tween.kill();clear()};
 },[metric,reduced,width,height]);
 useLayoutEffect(()=>{if(!shown.current)prior.current={metric,series:snapshot(geometry,root.current)}},[geometry,metric]);
 return <g ref={root} clipPath={clip}>
  <g ref={live}>{points.map((p,i)=><TimelineCurve key={metric+'/'+series[i].name} points={p} timestamps={timestamps} domain={domain} color={series[i].color} fill={fills[i]} baseline={baseline} live compositorSize={{width,height}} plotWidth={right-left} expectedMs={expectedMs} breakBefore={breakBefore} sampleIntervals={sampleIntervals}/>)}</g>
  <g ref={layer} data-overview-morph="" pointerEvents="none"/>
 </g>;
}
