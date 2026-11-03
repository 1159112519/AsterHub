import React,{useLayoutEffect,useRef,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {motionOptions,reducedMotion,subscribeReducedMotion} from '../motion';

export function captureToolSource(element){
 if(!element)return null;const rect=element.getBoundingClientRect();let scroll=element.parentElement;
 while(scroll&&!(/auto|scroll/.test(getComputedStyle(scroll).overflowY)&&scroll.scrollHeight>scroll.clientHeight))scroll=scroll.parentElement;
 return {left:rect.left,top:rect.top,width:rect.width,height:rect.height,scroll,scrollTop:scroll?.scrollTop||0};
}
export function ToolScene({view,source,children}){
 const ref=useRef(null),previous=useRef(view),animation=useRef(null),preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 useLayoutEffect(()=>{
  const node=ref.current,changed=previous.current!==view;previous.current=view;
  animation.current?.kill();animation.current=null;
  if(!changed||preference){if(changed&&source.current?.scroll)source.current.scroll.scrollTop=view==='monitor'?source.current.scrollTop:0;gsap.set(node,{clearProps:'transform,opacity,willChange'});return}
  const origin=source.current;if(changed&&origin?.scroll)origin.scroll.scrollTop=view==='monitor'?origin.scrollTop:0;
  const rect=node.getBoundingClientRect();
  const x=origin?Math.max(0,Math.min(rect.width,origin.left+origin.width/2-rect.left)):rect.width/2;
  const y=origin?Math.max(0,Math.min(rect.height,origin.top+origin.height/2-rect.top)):0;
  gsap.set(node,{transformOrigin:`${x}px ${y}px`});
  animation.current=gsap.fromTo(node,{opacity:.88,scale:view==='monitor'?.996:.99,y:view==='monitor'?-3:5},{opacity:1,scale:1,y:0,...motionOptions('tool'),clearProps:'transform,opacity,willChange',onComplete:()=>{animation.current=null}});
  return()=>{animation.current?.kill();animation.current=null;gsap.set(node,{clearProps:'transform,opacity,willChange'})};
 },[view,preference]);
 return <div ref={ref} className="foundation-tool-view" data-tool-view={view}>{children}</div>;
}
