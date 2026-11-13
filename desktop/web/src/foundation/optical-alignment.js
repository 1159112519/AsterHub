import {useLayoutEffect} from 'react';
export function opticalOffset(metrics,pixelRatio=1){const {actualBoundingBoxAscent:a,actualBoundingBoxDescent:d,fontBoundingBoxAscent:fa,fontBoundingBoxDescent:fd}=metrics;if(![a,d,fa,fd].every(Number.isFinite))return 0;const shift=((a-d)-(fa-fd))/2;return Math.round(Math.max(-3,Math.min(3,shift))*pixelRatio)/pixelRatio;}
export function centeredInkOffset(ink,anchor,current=0,pixelRatio=1){
 if(![ink?.top,ink?.bottom,anchor?.top,anchor?.bottom,current,pixelRatio].every(Number.isFinite)||pixelRatio<=0)return current;
 const next=current+(anchor.top+anchor.bottom-ink.top-ink.bottom)/2;
 return Math.round(Math.max(-6,Math.min(6,next))*pixelRatio)/pixelRatio;
}
export function baselineCenteredOffset(ink,anchor,current=0,ratio=1){
 if(![ink?.baseline,ink?.top,ink?.bottom,anchor?.top,anchor?.bottom,current,ratio].every(Number.isFinite)||ratio<=0)return current;
 const shift=(anchor.top+anchor.bottom-ink.top-ink.bottom)/2;
 // Snap the final baseline in screen pixels, not the relative CSS displacement.
 const baseline=Math.round((ink.baseline+shift)*ratio)/ratio;
 return Math.max(-6,Math.min(6,current+baseline-ink.baseline));
}
const selectors=['.button-content','.table-optical-label','.preferences-nav-label','.themed-select>span','.themed-select-option>span','.foundation-tool-navigation .tool-label','.sidebar .task-navigation-label','.sidebar .nav-item>span','.native-updater .about-link-button>span','.native-updater .about-external-link>a>span','.control-label'].join(',');
const baselineCache=new Map(),rasterCache=new Map();let rasterCanvas;
function glyphRasterBounds(text,css,metrics){
 const ratio=window.devicePixelRatio||1,key=[css.fontStyle,css.fontWeight,css.fontSize,css.fontFamily,text,ratio].join('|');if(rasterCache.has(key))return rasterCache.get(key);
 // The font outline and its hinted pixel coverage differ at fractional WebView DPI.
 // Measure the same glyph coverage at the current physical resolution, then align its baseline.
 rasterCanvas??=document.createElement('canvas');const pad=4,baseline=Math.ceil(metrics.fontBoundingBoxAscent*ratio)+pad;
 rasterCanvas.width=Math.max(1,Math.min(4096,Math.ceil((metrics.width+8)*ratio)));rasterCanvas.height=baseline+Math.ceil(metrics.fontBoundingBoxDescent*ratio)+pad;
 const ctx=rasterCanvas.getContext('2d',{willReadFrequently:true});ctx.setTransform(ratio,0,0,ratio,0,0);ctx.font=`${css.fontStyle} ${css.fontWeight} ${css.fontSize} ${css.fontFamily}`;ctx.fillStyle='#000';ctx.textBaseline='alphabetic';ctx.fillText(text,pad/ratio,baseline/ratio);
 const image=ctx.getImageData(0,0,rasterCanvas.width,rasterCanvas.height);let top=Infinity,bottom=-Infinity;
 for(let y=0;y<image.height;y++){for(let x=0;x<image.width;x++){if(image.data[(y*image.width+x)*4+3]>16){top=Math.min(top,y);bottom=Math.max(bottom,y+1);break;}}}
 const value=Number.isFinite(top)?{a:(baseline-top)/ratio,d:(bottom-baseline)/ratio}:null;if(rasterCache.size>512)rasterCache.clear();rasterCache.set(key,value);return value;
}
function baselineFromRange(text,css){
 const key=[css.fontStyle,css.fontWeight,css.fontSize,css.fontFamily,css.lineHeight,text].join('|');if(baselineCache.has(key))return baselineCache.get(key);
 // A zero-height inline box exposes the browser's actual fallback-font baseline.
 // FontBoundingBox metrics alone disagree with DOM fallback runs by 1-2 pixels.
 const host=document.createElement('span');host.dataset.opticalMeasure='true';host.setAttribute('aria-hidden','true');
 Object.assign(host.style,{position:'fixed',left:'-10000px',top:'0',display:'inline-block',visibility:'hidden',pointerEvents:'none',whiteSpace:'pre',fontStyle:css.fontStyle,fontWeight:css.fontWeight,fontSize:css.fontSize,fontFamily:css.fontFamily,lineHeight:css.lineHeight,fontSynthesis:'none',letterSpacing:css.letterSpacing});
 const run=document.createTextNode(text),marker=document.createElement('i');Object.assign(marker.style,{display:'inline-block',width:'0',height:'0',padding:'0',margin:'0',border:'0',verticalAlign:'baseline'});host.append(run,marker);document.body.append(host);
 const range=document.createRange();range.selectNodeContents(run);const offset=marker.getBoundingClientRect().top-range.getBoundingClientRect().top;host.remove();if(baselineCache.size>512)baselineCache.clear();baselineCache.set(key,offset);return offset;
}
// Measure each real text run, including the fallback Chinese font and multiline labels.
// The group is centered inside its control, never by changing the control's own position.
export function textInkBounds(node,context){
 const walker=document.createTreeWalker(node,NodeFilter.SHOW_TEXT);let text,top=Infinity,bottom=-Infinity,referenceBaseline;
 while((text=walker.nextNode())){if(!text.textContent.trim()||text.parentElement.closest('svg,[aria-hidden=true]'))continue;
  const css=getComputedStyle(text.parentElement);if(css.visibility==='hidden'||css.display==='none')continue;
  context.font=`${css.fontStyle} ${css.fontWeight} ${css.fontSize} ${css.fontFamily}`;const m=context.measureText(text.textContent);
  const {actualBoundingBoxAscent,fontBoundingBoxAscent:fa,fontBoundingBoxDescent:fd}=m;if(![actualBoundingBoxAscent,m.actualBoundingBoxDescent,fa,fd].every(Number.isFinite))continue;const raster=glyphRasterBounds(text.textContent,css,m),a=raster?.a??actualBoundingBoxAscent,d=raster?.d??m.actualBoundingBoxDescent;
  const range=document.createRange();range.selectNodeContents(text);const fromRange=baselineFromRange(text.textContent,css);
  for(const box of range.getClientRects()){if(!box.width||!box.height)continue;const baseline=box.top+fromRange;if(referenceBaseline===undefined)referenceBaseline=baseline;top=Math.min(top,baseline-a);bottom=Math.max(bottom,baseline+d);}
 }
 return Number.isFinite(top)?{top,bottom,baseline:referenceBaseline}:null;
}
export function useOpticalAlignment(){useLayoutEffect(()=>{
 let alive=true;const queue=new Set(),context=document.createElement('canvas').getContext('2d');
 const measure=node=>{if(!node.isConnected||!context)return;const anchor=node.classList.contains('table-optical-label')?node.closest('th'):node.closest('button,a,[role=option]');if(!anchor)return;const box=anchor.getBoundingClientRect();if(!box.width||!box.height)return;
  const ink=textInkBounds(node,context);if(!ink)return;const current=parseFloat(getComputedStyle(node).top)||0;
  const offset=node.classList.contains('preferences-nav-label')?centeredInkOffset(ink,box,current,window.devicePixelRatio||1):baselineCenteredOffset(ink,box,current,window.devicePixelRatio||1),value=offset+'px';if(node.style.getPropertyValue('--label-optical-y')!==value)return [node,value];
 };
 const flush=()=>{if(!alive||!queue.size)return;const changes=[...queue].map(measure);queue.clear();for(const change of changes)if(change)change[0].style.setProperty('--label-optical-y',change[1]);};
 const own=node=>{const label=node?.closest?.(selectors);if(label)queue.add(label);};
 const collect=node=>{if(node.nodeType!==1||node.dataset?.opticalMeasure!==undefined)return;own(node);node.querySelectorAll(selectors).forEach(n=>queue.add(n));};
 const all=()=>{baselineCache.clear();rasterCache.clear();collect(document.body);flush();};
 document.fonts.ready.then(()=>alive&&all());document.fonts.addEventListener('loadingdone',all);
 // Mutation callbacks run before the browser paints new controls. A later animation frame visibly moves the text.
 const observer=new MutationObserver(changes=>{for(const c of changes){
  if(c.type==='childList'){if(c.addedNodes.length||c.removedNodes.length)own(c.target);for(const n of c.addedNodes)collect(n);}
  else if(c.type==='characterData')own(c.target.parentElement);
  else if(c.target===document.documentElement)all();
  else if(c.target.matches(selectors)||c.target.closest('button,a,[role=option]'))collect(c.target);
 }flush();});
 observer.observe(document.body,{childList:true,subtree:true,characterData:true,attributes:true,attributeFilter:['class']});
 observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-theme','data-density']});
 collect(document.body);flush();window.addEventListener('resize',all);
 return()=>{alive=false;observer.disconnect();document.fonts.removeEventListener('loadingdone',all);window.removeEventListener('resize',all);queue.clear();};
},[]);}
