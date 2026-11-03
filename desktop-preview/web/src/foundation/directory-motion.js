import {gsap} from 'gsap';
import {motion, motionOptions, reducedMotion} from '../motion';

// A single owned timeline preserves source geometry and releases its clone.
export function animateDirectoryTransition({content, stage, pending, onComplete}) {
  const {clone, rect, direction} = pending;
  let timeline, released = false;
  function release() {
    if (released) return; released = true; clone.remove();
    gsap.set(content, {clearProps: 'transform,opacity,willChange'});
  }
  if (reducedMotion()) { release(); onComplete?.(); return {cancel: release}; }
  const width = Math.max(1, stage?.clientWidth || 1), height = Math.max(1, stage?.clientHeight || 1);
  // Only one flat surface is animated. Transforming a cloned directory tree
  // creates enormous raster layers for small source tiles in large directories.
  const sx=Math.max(.05,rect.width/width),sy=Math.max(.05,rect.height/height);
  gsap.set(clone,{position:'absolute',left:0,top:0,width,height,borderRadius:12,pointerEvents:'none',transformOrigin:'0 0',x:direction==='in'?rect.x:0,y:direction==='in'?rect.y:0,scaleX:direction==='in'?sx:1,scaleY:direction==='in'?sy:1,opacity:1,willChange:'transform,opacity'});
  gsap.set(content,{transformOrigin:`${rect.x+rect.width/2}px ${rect.y+rect.height/2}px`,scale:direction==='in'?.94:1.04,opacity:0,willChange:'transform,opacity'});
  timeline=gsap.timeline({defaults:motionOptions('drill',{ease:motion.ease.drill}),onComplete(){release();timeline=null;onComplete?.()}});
  timeline.to(clone,{x:direction==='in'?0:rect.x,y:direction==='in'?0:rect.y,scaleX:direction==='in'?1:sx,scaleY:direction==='in'?1:sy,opacity:0},0).to(content,{scale:1,opacity:1},.06);
  return {cancel(){timeline?.kill();timeline=null;release()}};
}
