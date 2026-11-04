import {directoryBackdrop} from './directory-surface';
import {gsap} from 'gsap';
import {reducedMotion} from '../motion';

// One nonlinear clock for both layers. Only the frame changes layout; the contents
// have their final pixel size and are revealed through it without scaling text.
export function animateDirectoryTransition({content, stage, shade, pending, outgoing, incoming, onComplete}) {
 let animation=null,released=false;
 const width=Math.max(1,stage?.clientWidth||1),height=Math.max(1,stage?.clientHeight||1),phase={value:0};
 const clamp=gsap.utils.clamp(0,1),ease=gsap.parseEase('power3.inOut'),cross=pending.direction==='across';
 const portal=(element,entry)=>element&&entry?{element,rect:entry.rect,face:element.querySelector('.storage-portal-face'),body:element.querySelector('.storage-portal-content'),visible:null}:null;
 const out=portal(outgoing,pending.before),next=portal(incoming,pending.after);
 const sources=new Map();
 for(const entry of [pending.before,pending.after]){
  const id=entry?.sourceId;if(!id||sources.has(id))continue;
  const tile=content?.querySelector(`[data-tile="${CSS.escape(id)}"]`);
  if(tile)sources.set(id,{tile,hidden:false});
 }
 function release(){
  if(released)return;released=true;
  if(shade)shade.style.opacity='';
  for(const {tile} of sources.values())tile.style.visibility='';
 }
 if(reducedMotion()){release();onComplete?.();return {cancel:release};}
 function surface(layer,p,visible){
  if(!layer)return;
  const {element,rect:r,face,body}=layer;
  if(layer.visible!==visible){element.style.visibility=visible?'visible':'hidden';layer.visible=visible;}
  if(!visible)return;
  const x=r.x*(1-p),y=r.y*(1-p);
  Object.assign(element.style,{left:x+'px',top:y+'px',width:r.width+(width-r.width)*p+'px',height:r.height+(height-r.height)*p+'px',borderRadius:Math.min(22,Math.min(r.width,r.height)*.2)*(1-p)+18*p+'px'});
  element.style.setProperty('--storage-lift',String(Math.sin(Math.PI*p)));
  if(face)face.style.opacity=String(1-clamp((p-.18)/.36));
  if(body){body.style.opacity=String(clamp((p-.30)/.40));body.style.transform=`translate3d(${-x}px,${-y}px,0)`;}
 }
 function paint(){
  const t=phase.value,outActive=!cross||t<.60,inActive=!cross||t>=.40;
  surface(out,1-ease(clamp(t/(cross?.60:1))),outActive);
  surface(next,ease(clamp(cross?(t-.40)/.60:t)),inActive);
  if(shade)shade.style.opacity=String(1-directoryBackdrop(t));
  for(const [id,source] of sources){
   const hidden=pending.before?.sourceId===id&&outActive||pending.after?.sourceId===id&&inActive;
   if(source.hidden!==hidden){source.tile.style.visibility=hidden?'hidden':'';source.hidden=hidden;}
  }
 }
 paint();animation=gsap.to(phase,{value:1,duration:pending.duration||.52,ease:'none',onUpdate:paint,onComplete(){release();animation=null;onComplete?.();}});
 return {cancel(){animation?.kill();animation=null;release();}};
}
