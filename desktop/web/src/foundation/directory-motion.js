import {directoryBackdrop} from './directory-surface';
import {gsap} from 'gsap';
import {reducedMotion} from '../motion';

// One nonlinear clock for both layers. Only the frame changes layout; the contents
// have their final pixel size and are revealed through it without scaling text.
export function animateDirectoryTransition({content, stage, shade, pending, outgoing, incoming, onComplete}) {
 if(pending.direction==='tree'){
  const duration=385,easing='cubic-bezier(.25,.65,.3,1)', options={duration,easing,fill:'both'};
  if(reducedMotion()){onComplete?.();return {cancel(){}};}
  const effects=[content.animate([{opacity:0},{opacity:1}],options),shade.animate([{opacity:1,filter:'blur(0px)'},{opacity:0,filter:'blur(2px)'}],options)];
  let live=true;Promise.allSettled(effects.map(effect=>effect.finished)).then(()=>{if(live)onComplete?.()});
  return {cancel(){live=false;effects.forEach(effect=>effect.cancel())}};
 }
 let animation=null,released=false;
 const width=Math.max(1,stage?.clientWidth||1),height=Math.max(1,stage?.clientHeight||1),phase={value:0};
 const clamp=gsap.utils.clamp(0,1),ease=gsap.parseEase('power3.inOut'),cross=pending.direction==='across';
 const portal=(element,entry)=>element&&entry?{element,rect:entry.rect,face:element.querySelector('.storage-portal-face'),body:element.querySelector('.storage-portal-content'),visible:null}:null;
 const out=portal(outgoing,pending.before),next=portal(incoming,pending.after),returning=!!out&&!next;
 const sources=new Map();
 for(const entry of [pending.before,pending.after]){
  const id=entry?.sourceId;if(!id||sources.has(id))continue;
  const tile=content?.querySelector(`[data-tile="${CSS.escape(id)}"]`);
  if(tile)sources.set(id,{tile,hidden:false});
 }
 function release(){
  if(released)return;released=true;
  if(shade)shade.style.opacity='';
  if(content){content.style.filter='';content.style.transform='';}
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
  element.style.setProperty('--storage-outline',String(1-clamp((p-.72)/.28)));
  if(face)face.style.opacity=String(1-clamp((p-.18)/.36));
  if(body){body.style.opacity=String(clamp((p-.30)/.40));body.style.transform=`translate3d(${-x}px,${-y}px,0)`;}
 }
 function paint(){
  const t=phase.value,outActive=!cross||t<.60,inActive=!cross||t>=.40;
  surface(out,1-ease(clamp(t/(cross?.60:returning?.85:1))),outActive);
  const handoff=returning?ease(clamp((t-.85)/.15)):0;
  if(returning)out.element.style.opacity=String(1-handoff);
  surface(next,ease(clamp(cross?(t-.40)/.60:t)),inActive);
  if(shade)shade.style.opacity=String(1-directoryBackdrop(t));
  const retreat=returning?1-ease(clamp(t/.82)):ease(clamp(t/.72));
  if(content&&!cross){content.style.filter=`blur(${(3*retreat).toFixed(2)}px)`;content.style.transform=`translateY(${(5*retreat).toFixed(2)}px)`;}
  for(const [id,source] of sources){
   const hidden=pending.before?.sourceId===id&&outActive||pending.after?.sourceId===id&&inActive;
   const reveal=returning&&pending.before?.sourceId===id&&t>=.85;
   if(source.hidden!==(hidden&&!reveal)){source.tile.style.visibility=hidden&&!reveal?'hidden':'';source.hidden=hidden&&!reveal;}
  }
 }
 paint();animation=gsap.to(phase,{value:1,duration:pending.duration||.52,ease:'none',onUpdate:paint,onComplete(){release();animation=null;onComplete?.();}});
 return {cancel(){animation?.kill();animation=null;release();}};
}
