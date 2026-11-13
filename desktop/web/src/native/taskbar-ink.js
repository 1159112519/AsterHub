// Final output pixels, rather than font ascent/descent alone, define the visible bottom.
// Each measurement is cached at the current font, DPI and subpixel origin.
const cache=new Map(),boundsCache=new Map(),measureCache=new Map();let canvas,measureContext;
export function centerTaskbarInk(text,font,cx,cy){
 if(typeof document==='undefined'||!document.fonts.check(font,text))return {x:cx,y:cy};
 const key=font+'|'+text;let offset=boundsCache.get(key);
 if(!offset){
  measureContext??=document.createElement('canvas').getContext('2d');measureContext.font=font;
  const m=measureContext.measureText(text);
  offset={x:(m.actualBoundingBoxLeft-m.actualBoundingBoxRight)/2,y:(m.actualBoundingBoxAscent-m.actualBoundingBoxDescent)/2};
  boundsCache.set(key,offset);
 }
 return {x:cx+offset.x,y:cy+offset.y};
}
export function taskbarInkY(text,font,bottom,phase={x:0,y:0}){
 if(!text||typeof document==='undefined'||!document.fonts.check(font))return bottom;
 const dpr=globalThis.devicePixelRatio||1,fx=((phase.x*dpr)%1+1)%1,fy=(((phase.y+bottom)*dpr)%1+1)%1;
 const key=[text,font,dpr,fx.toFixed(4),fy.toFixed(4)].join('|');let shift=cache.get(key);
 if(shift===undefined){canvas??=document.createElement('canvas');const width=Math.ceil((text.length*16+20)*dpr),height=Math.ceil(40*dpr);canvas.width=width;canvas.height=height;const c=canvas.getContext('2d',{willReadFrequently:true});c.setTransform(dpr,0,0,dpr,0,0);c.font=font;c.fillStyle='#fff';c.textBaseline='alphabetic';const base=Math.ceil(24*dpr);c.fillText(text,(4+fx)/dpr,(base+fy)/dpr);const data=c.getImageData(0,0,width,height).data;let last=-1;for(let y=height-1;y>=0&&last<0;y--)for(let x=0;x<width;x++)if(data[(y*width+x)*4+3]>=128){last=y;break;}shift=last<0?0:(base+Math.round(fy)-1-last)/dpr;if(cache.size>=768)cache.clear();cache.set(key,shift);}
 return bottom+shift;
}

export function measureTaskbarInk(text,font){
 if(!text)return {width:0,left:0};
 if(typeof document==='undefined'||!document.fonts.check(font,text))return {width:[...text].length*8,left:0};
 const key=font+'|'+text;if(measureCache.has(key))return measureCache.get(key);
 measureContext??=document.createElement('canvas').getContext('2d');measureContext.font=font;
 const m=measureContext.measureText(text),result={width:Math.max(0,m.actualBoundingBoxLeft+m.actualBoundingBoxRight),left:m.actualBoundingBoxLeft};
 if(measureCache.size>=512)measureCache.clear();measureCache.set(key,result);return result;
}
