import {gsap} from 'gsap';
import {createPreferenceStore} from './foundation/preference-store.js';

// Motion describes a change of state, never an unrelated decoration.
export const motion=Object.freeze({
 duration:Object.freeze({feedback:.14,page:.24,surface:.28,exit:.18,layout:.28,value:.95,chart:1.05,meter:1.05,meterEcho:1.3,trace:.9,drill:.58,tool:.38}),
 ease:Object.freeze({settle:'power3.out',leave:'power2.in',drill:'power3.inOut',css:'cubic-bezier(.22,1,.36,1)'}),
 amplitude:Object.freeze({contentShift:2})
});
export function reducedMotion(){return document.documentElement.dataset.motion==='reduced'||matchMedia('(prefers-reduced-motion: reduce)').matches}
// One shared subscription covers both the in-app switch and the OS preference.
// React consumers re-run their layout effects and settle any in-flight values.
const preferenceStore=createPreferenceStore(reducedMotion,notify=>{
 const media=matchMedia('(prefers-reduced-motion: reduce)'),observer=new MutationObserver(notify);
 observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-motion']});media.addEventListener('change',notify);
 return()=>{observer.disconnect();media.removeEventListener('change',notify)};
});
export const subscribeReducedMotion=preferenceStore.subscribe;
export const motionDiagnostics=preferenceStore.diagnostics;
export function motionOptions(kind,extra={}){return {duration:motion.duration[kind],ease:motion.ease.settle,overwrite:'auto',...extra,...(reducedMotion()?{duration:0}:null)}}
export function installMotionTokens(){const root=document.documentElement;for(const [key,value] of Object.entries(motion.duration))root.style.setProperty(`--motion-${key}`,`${value}s`);root.style.setProperty('--ease',motion.ease.css)}
export function revealContent(element){if(!element||reducedMotion())return null;return gsap.fromTo(element,{opacity:.9},{opacity:1,...motionOptions('page'),clearProps:'opacity'})}
export function moveLayout(element,distance){if(!element||reducedMotion())return null;return gsap.fromTo(element,{y:distance},{y:0,...motionOptions('layout'),clearProps:'transform'})}
// Static text surfaces are painted at their final coordinates from the first frame.
export function liftFromSource(element){
 if(!element||reducedMotion())return null;
 return gsap.fromTo(element,{opacity:0},{opacity:1,...motionOptions('surface'),clearProps:'opacity'});
}
export function returnToSource(element,onComplete){
 if(reducedMotion()){onComplete();return null}
 return gsap.to(element,{opacity:0,...motionOptions('exit'),ease:motion.ease.leave,onComplete});
}
export function moveSurface(element){
 if(!element)return null;
 return gsap.to(element,{opacity:1,...motionOptions('layout'),clearProps:'opacity'});
}
