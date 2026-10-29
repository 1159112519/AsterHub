import {gsap} from 'gsap';

// Motion describes a change of state, never an unrelated decoration.
export const motion=Object.freeze({
 duration:Object.freeze({feedback:.14,page:.24,surface:.28,exit:.18,layout:.28,value:.85,chart:1.05,trace:.9}),
 ease:Object.freeze({settle:'power3.out',leave:'power2.in',css:'cubic-bezier(.22,1,.36,1)'}),
 amplitude:Object.freeze({surfaceScale:.986,contentShift:2})
});
export function reducedMotion(){return document.documentElement.dataset.motion==='reduced'||matchMedia('(prefers-reduced-motion: reduce)').matches}
export function motionOptions(kind,extra={}){return {duration:reducedMotion()?0:motion.duration[kind],ease:motion.ease.settle,overwrite:'auto',...extra}}
export function installMotionTokens(){const root=document.documentElement;for(const [key,value] of Object.entries(motion.duration))root.style.setProperty(`--motion-${key}`,`${value}s`);root.style.setProperty('--ease',motion.ease.css)}
export function revealContent(element){if(!element||reducedMotion())return null;return gsap.fromTo(element,{opacity:.9,y:motion.amplitude.contentShift},{opacity:1,y:0,...motionOptions('page'),clearProps:'transform,opacity'})}
export function moveLayout(element,distance){if(!element||reducedMotion())return null;return gsap.fromTo(element,{y:distance},{y:0,...motionOptions('layout'),clearProps:'transform'})}
export function liftFromSource(element,source){
 if(!element)return null;const r=element.getBoundingClientRect(),a=source?.getBoundingClientRect();
 const x=a?Math.max(0,Math.min(r.width,a.left+a.width/2-r.left)):r.width/2;
 const y=a?Math.max(0,Math.min(r.height,a.top+a.height/2-r.top)):r.height/2;
 gsap.set(element,{transformOrigin:`${x}px ${y}px`});
 if(reducedMotion()){gsap.set(element,{opacity:1,scale:1,x:0,y:0});return null}
 return gsap.fromTo(element,{opacity:0,scale:motion.amplitude.surfaceScale},{opacity:1,scale:1,...motionOptions('surface'),clearProps:'transform'});
}
export function returnToSource(element,onComplete){
 if(reducedMotion()){onComplete();return null}
 return gsap.to(element,{opacity:0,scale:motion.amplitude.surfaceScale,...motionOptions('exit'),ease:motion.ease.leave,onComplete});
}
export function moveSurface(element,previous,next,source){
 const x=previous.left-next.left+Number(gsap.getProperty(element,'x')||0),y=previous.top-next.top+Number(gsap.getProperty(element,'y')||0);
 const a=source?.getBoundingClientRect();
 if(a)gsap.set(element,{transformOrigin:`${Math.max(0,Math.min(element.offsetWidth,a.left+a.width/2-next.left))}px ${Math.max(0,Math.min(element.offsetHeight,a.top+a.height/2-next.top))}px`});
 if(reducedMotion()){gsap.set(element,{x:0,y:0,opacity:1,scale:1});return null}
 return gsap.fromTo(element,{x,y},{x:0,y:0,opacity:1,scale:1,...motionOptions('layout'),clearProps:'transform'});
}
