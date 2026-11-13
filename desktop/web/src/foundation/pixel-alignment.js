export function snapSurfacePosition(position,ratio=1){const scale=Number.isFinite(ratio)&&ratio>0?ratio:1;const snap=value=>Number.isFinite(value)?Math.round(value*scale)/scale:0;return {...position,left:snap(position.left),top:snap(position.top)}}

export function snapRelativePixel(value,origin=0,ratio=1){
 if(!Number.isFinite(value)||!Number.isFinite(origin)||!Number.isFinite(ratio)||ratio<=0)return value;
 return Math.round((origin+value)*ratio)/ratio-origin;
}
// CSS positions the surface relative to the parent padding box, which can itself be fractional.
export function snapElementToParentPixels(frame,position){
 const parent=frame?.offsetParent;if(!parent)return;
 const rect=parent.getBoundingClientRect(),style=getComputedStyle(parent),ratio=globalThis.devicePixelRatio||1;
 for(const [axis,origin,border] of [['left',rect.left,'borderLeftWidth'],['top',rect.top,'borderTopWidth']]){
  if(Number.isFinite(position[axis]))frame.style[axis]=snapRelativePixel(position[axis],origin+(parseFloat(style[border])||0),ratio)+'px';
 }
 frame.dataset.pixelSnapped='true';
}
