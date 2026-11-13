import {t,tr,useLocale} from './i18n/index.js';
import {snapElementToParentPixels,snapSurfacePosition} from './foundation/pixel-alignment';
import React,{useLayoutEffect,useRef,useState,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {motion,motionOptions,reducedMotion,subscribeReducedMotion} from './motion';

// One retained surface: interrupted exits resume in place, never stack another copy.
// Position is committed before fade, never interpolated or snapped after opening.
export function MotionSurface({open,position:rawPosition,changeKey,children,staticContent=false,constrainTo=null,className='',style={},...props}){useLocale();
 const [present,setPresent]=useState(open),preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const frame=useRef(null),surface=useRef(null),content=useRef(null),last=useRef(null),snapshot=useRef(children),phase=useRef('closed');
 // An old snapshot fading out must stay beside its own point when a new point is loading.
 const position=(!open&&last.current)||snapSurfacePosition(rawPosition,globalThis.devicePixelRatio);
 if(open)snapshot.current=children;
 useLayoutEffect(()=>{
  if(open&&!present){setPresent(true);return}
  if(!present)return;
  const outer=frame.current,el=surface.current,body=content.current;
  const previous=last.current,was=phase.current;let alive=true,observer=null,scroller=null,fit=null;const tweens=[];
  snapElementToParentPixels(outer,position);
  const setPhase=value=>{phase.current=value;outer.dataset.motionState=value};
  const finish=()=>{if(!alive)return;setPhase('closed');last.current=null;snapshot.current=null;setPresent(false)};
  const options=kind=>({...motionOptions(kind),duration:preference?0:motion.duration[kind]});
  if(!open){
   setPhase('exiting');tweens.push(gsap.to(el,{opacity:0,...options('exit'),ease:motion.ease.leave,onComplete:finish}));
  }else if(staticContent){
   // Place the final text and scroll viewport before changing opacity; never animate their geometry.
   gsap.set(body,{clearProps:'opacity'});
   const keepIn=constrainTo&&outer.closest(constrainTo);
   if(constrainTo){
    fit=()=>{
     const parent=outer.offsetParent;if(!parent)return;
     const rect=keepIn?.getBoundingClientRect(),origin=parent.getBoundingClientRect().top+parent.clientTop;
     const inset=8,lo=Math.max(0,rect?.top??0)+inset,hi=Math.min(innerHeight,rect?.bottom??innerHeight)-inset;
     outer.style.setProperty('--motion-available-height',Math.max(0,hi-lo)+'px');
     const top=Math.max(lo,Math.min(hi-outer.getBoundingClientRect().height,origin+position.top))-origin;
     const fitted={...position,top};snapElementToParentPixels(outer,fitted);last.current={...fitted,key:changeKey};
    };
    fit();scroller=keepIn||window;scroller.addEventListener('scroll',fit,{passive:true});window.addEventListener('resize',fit);
    if(keepIn){observer=new ResizeObserver(fit);observer.observe(keepIn)}
   }else last.current={...position,key:changeKey};
   if(preference){gsap.set(el,{clearProps:'opacity'});setPhase('open')}
   else if(was!=='open'||previous?.key!==changeKey){
    const fresh=was==='closed'||previous?.key!==changeKey;
    setPhase('entering');
    if(fresh)gsap.set(el,{opacity:0});
    tweens.push(gsap.to(el,{opacity:1,...options('surface'),clearProps:'opacity',onComplete:()=>{if(alive)setPhase('open')}}));
   }
  }else{
   const entering=was==='closed'||previous?.key!==changeKey;
   setPhase(entering?'entering':'updating');
   if(entering)gsap.set(el,{opacity:0});
   tweens.push(gsap.to(el,{opacity:1,...options('surface'),clearProps:'opacity',onComplete:()=>{if(alive)setPhase('open')}}));
   if(previous&&previous.key!==changeKey)tweens.push(gsap.fromTo(body,{opacity:preference?1:.72},{opacity:1,...options('feedback'),clearProps:'opacity'}));
   last.current={...position,key:changeKey};
  }
  return()=>{alive=false;tweens.forEach(t=>t.kill());observer?.disconnect();if(fit){scroller.removeEventListener('scroll',fit);window.removeEventListener('resize',fit)}};
 },[open,present,position.left,position.top,position.originX,position.originY,changeKey,preference,staticContent,constrainTo]);
 if(!open&&!present)return null;
 return <div {...props} ref={frame} className={className} style={{...style,left:position.left,top:position.top,pointerEvents:open?undefined:'none'}} aria-hidden={!open||undefined} inert={!open?'':undefined}><div ref={surface} className="motion-surface"><div ref={content} className="motion-surface-content">{tr(snapshot.current)}</div></div></div>;
}
