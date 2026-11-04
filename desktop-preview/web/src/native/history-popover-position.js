// Align the resting popover edge to a physical screen pixel, including fractional
// chart origins and Windows display scaling. Animation remains owned by MotionSurface.
export function snapHistoryCoordinate(value,origin=0,ratio=1){if(!Number.isFinite(value)||!Number.isFinite(origin)||!Number.isFinite(ratio)||ratio<=0)return value;return Math.round((origin+value)*ratio)/ratio-origin}
export function alignHistoryPopover(frame){
 const parent=frame?.offsetParent;if(!parent)return;const rect=parent.getBoundingClientRect(),style=getComputedStyle(parent),ratio=globalThis.devicePixelRatio||1;
 for(const [axis,origin,border] of [['left',rect.left,'borderLeftWidth'],['top',rect.top,'borderTopWidth']]){const value=parseFloat(frame.style[axis]);if(!Number.isFinite(value))continue;const aligned=snapHistoryCoordinate(value,origin+(parseFloat(style[border])||0),ratio);if(Math.abs(aligned-value)>.001)frame.style[axis]=aligned+'px';}
 frame.dataset.pixelSnapped='true';
}
