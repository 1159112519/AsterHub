// Restore against delayed content without recording browser-clamped programmatic scrolls.
// A real input gesture transfers control to the user. Cleanup never samples replaced DOM.
export function createScrollMemory(element,handle,{maxWaitMs=10000,view=element.ownerDocument.defaultView}={}){
 const stored=handle.getSnapshot(),wanted=Number.isFinite(stored)?Math.max(0,stored):0;
 let disposed=false,restoring=true,userControlled=false,frame=null,timer=null,resize=null,mutations=null;
 function stop(){restoring=false;if(frame!==null){view.cancelAnimationFrame(frame);frame=null}if(timer!==null){view.clearTimeout(timer);timer=null}resize?.disconnect();mutations?.disconnect()}
 function restore(){if(disposed||!restoring)return;element.scrollTop=wanted;if(Math.abs(element.scrollTop-wanted)<=1)stop()}
 function schedule(){if(disposed||!restoring||frame!==null)return;frame=view.requestAnimationFrame(()=>{frame=null;restore()})}
 function input(){if(disposed)return;userControlled=true;stop()}
 const gestures=['wheel','pointerdown','touchstart','keydown'];
 for(const gesture of gestures)element.addEventListener(gesture,input,{passive:true});
 resize=new view.ResizeObserver(schedule);resize.observe(element);for(const child of element.children)resize.observe(child);
 mutations=new view.MutationObserver(()=>{if(!restoring)return;for(const child of element.children)resize.observe(child);schedule()});mutations.observe(element,{childList:true,subtree:true});
 timer=view.setTimeout(stop,maxWaitMs);restore();
 return {onScroll(){if(!disposed&&userControlled)handle.setState(element.scrollTop)},dispose(){if(disposed)return;disposed=true;stop();for(const gesture of gestures)element.removeEventListener(gesture,input)}};
}
