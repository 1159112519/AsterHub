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
  const sx = Math.max(.001, rect.width / width), sy = Math.max(.001, rect.height / height);
  const tile = {x: rect.x, y: rect.y, scaleX: sx, scaleY: sy};
  const expanded = {x: -rect.x / sx, y: -rect.y / sy, scaleX: 1 / sx, scaleY: 1 / sy};
  gsap.set(content, {transformOrigin: '0 0', ...(direction === 'in' ? tile : expanded), opacity: 0});
  timeline = gsap.timeline({defaults: motionOptions('drill', {ease: motion.ease.drill}), onComplete() {
    release(); timeline = null; onComplete?.();
  }});
  timeline.to(clone, {...(direction === 'in' ? expanded : tile), opacity: 0}, 0)
    .to(content, {x: 0, y: 0, scaleX: 1, scaleY: 1, opacity: 1}, 0);
  return {cancel() { timeline?.kill(); timeline = null; release(); }};
}
