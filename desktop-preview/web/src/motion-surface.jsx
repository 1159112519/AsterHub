import {snapSurfacePosition} from './foundation/pixel-alignment';
import React,{useLayoutEffect,useRef,useState,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {motion,motionOptions,reducedMotion,subscribeReducedMotion} from './motion';

// One retained surface: interrupted exits resume in place, never stack another copy.
// The outer layer owns position; the inner layer owns presence. Their tweens cannot conflict.
export function MotionSurface({open,position:rawPosition,changeKey,children,className='',style={},...props}){
 const position=snapSurfacePosition(rawPosition,globalThis.devicePixelRatio);
 const [present,setPresent]=useState(open),preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const frame=useRef(null),surface=useRef(null),content=useRef(null),last=useRef(null),snapshot=useRef(children),phase=useRef('closed');
 if(open)snapshot.current=children;
 useLayoutEffect(()=>{
  if(open&&!present){setPresent(true);return}
  if(!present)return;
  const outer=frame.current,el=surface.current,body=content.current;
  const previous=last.current,was=phase.current;let alive=true;const tweens=[];
  const setPhase=value=>{phase.current=value;outer.dataset.motionState=value};
  const finish=()=>{if(!alive)return;setPhase('closed');last.current=null;snapshot.current=null;setPresent(false)};
  const options=kind=>({...motionOptions(kind),duration:preference?0:motion.duration[kind]});
  if(!open){
   setPhase('exiting');tweens.push(gsap.to(el,{opacity:0,scale:motion.amplitude.surfaceScale,...options('exit'),ease:motion.ease.leave,onComplete:finish}));
  }else{
   setPhase(was==='closed'?'entering':'updating');
   gsap.set(el,{transformOrigin:(position.originX??0)+'px '+(position.originY??0)+'px'});
   if(was==='closed')gsap.set(el,{opacity:0,scale:motion.amplitude.surfaceScale});
   tweens.push(gsap.to(el,{opacity:1,scale:1,...options('surface'),clearProps:'transform,opacity',onComplete:()=>{if(alive)setPhase('open')}}));
   if(previous){
    const x=previous.left-position.left+Number(gsap.getProperty(outer,'x')||0),y=previous.top-position.top+Number(gsap.getProperty(outer,'y')||0);
    tweens.push(gsap.fromTo(outer,{x,y},{x:0,y:0,...options('layout'),clearProps:'transform'}));
    if(previous.key!==changeKey)tweens.push(gsap.fromTo(body,{opacity:preference?1:.72},{opacity:1,...options('feedback'),clearProps:'opacity'}));
   }
   last.current={...position,key:changeKey};
  }
  return()=>{alive=false;tweens.forEach(t=>t.kill())};
 },[open,present,position.left,position.top,position.originX,position.originY,changeKey,preference]);
 if(!open&&!present)return null;
 return <div {...props} ref={frame} className={className} style={{...style,left:position.left,top:position.top,pointerEvents:open?undefined:'none'}} aria-hidden={!open||undefined} inert={!open?'':undefined}><div ref={surface} className="motion-surface"><div ref={content} className="motion-surface-content">{snapshot.current}</div></div></div>;
}
