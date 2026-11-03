import {gsap} from 'gsap';
import {reducedMotion} from '../motion';

// Own exactly one clock. Geometry moves; text is never transformed or raster-scaled.
export function animateDirectoryTransition({content, stage, pending, outgoing, incoming, onComplete}) {
 let animation=null,released=false;
 const width=Math.max(1,stage?.clientWidth||1),height=Math.max(1,stage?.clientHeight||1),phase={value:0};
 const clamp=gsap.utils.clamp(0,1),ease=gsap.parseEase('power3.inOut'),cross=pending.direction==='across';
 function release(){if(released)return;released=true;if(content){content.style.filter='';for(const tile of content.querySelectorAll('[data-tile]'))tile.style.visibility='';}}
 if(reducedMotion()){release();onComplete?.();return {cancel:release};}
 function surface(element,entry,p,visible){
  if(!element||!entry)return;const r=entry.rect;
  Object.assign(element.style,{visibility:visible?'visible':'hidden',left:r.x*(1-p)+'px',top:r.y*(1-p)+'px',width:r.width+(width-r.width)*p+'px',height:r.height+(height-r.height)*p+'px',borderRadius:Math.min(22,Math.min(r.width,r.height)*.2)*(1-p)+18*p+'px'});
  element.style.setProperty('--storage-lift',String(Math.sin(Math.PI*p)));
  const face=element.querySelector('.storage-portal-face'),body=element.querySelector('.storage-portal-content');
  if(face)face.style.opacity=String(1-clamp((p-.18)/.36));if(body)body.style.opacity=String(clamp((p-.30)/.40));
 }
 function paint(){
  const t=phase.value,out=1-ease(clamp(t/(cross?.60:1))),next=ease(clamp(cross?(t-.40)/.60:t));
  surface(outgoing,pending.before,out,!cross||t<.60);surface(incoming,pending.after,next,!cross||t>=.40);
  if(content){content.style.filter=`brightness(${1-.08*Math.max(pending.before?out:0,pending.after?next:0)})`;
   for(const tile of content.querySelectorAll('[data-tile]'))tile.style.visibility=(pending.before?.sourceId===tile.dataset.tile&&(!cross||t<.60))||(pending.after?.sourceId===tile.dataset.tile&&(!cross||t>=.40))?'hidden':'visible';
  }
 }
 paint();animation=gsap.to(phase,{value:1,duration:pending.duration||.52,ease:'none',onUpdate:paint,onComplete(){release();animation=null;onComplete?.();}});
 return {cancel(){animation?.kill();animation=null;release();}};
}
