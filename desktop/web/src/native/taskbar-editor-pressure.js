// Room preview is presentation-only; keep every saved slot in its original column.
const maxRoom=282,pinch=8,fadeSpan=14;
const clamp=(x,min,max)=>Math.max(min,Math.min(max,x));
const smooth=x=>{const t=clamp(x,0,1);return t*t*(3-2*t)};
export function editorPressure(count,pitch,value,reduced=false){
 const room=value-(maxRoom-count*pitch),full=n=>n*pitch;
 let visible=count,compression=0,fade=0;
 while(visible>0){
  const minimum=full(visible)-(visible-1)*pinch;
  if(room>=minimum){compression=visible>1?clamp((full(visible)-room)/(visible-1),0,pinch):0;break;}
  if(!reduced&&room>=minimum-fadeSpan){compression=visible>1?pinch:0;fade=smooth((minimum-room)/fadeSpan);break;}
  if(visible>2&&room>full(visible-1)){compression=pinch*(1-smooth((minimum-(reduced?0:fadeSpan)-room)/Math.max(1,minimum-(reduced?0:fadeSpan)-full(visible-1))));visible--;break;}
  visible--;
 }
 return Array.from({length:count},(_,i)=>{const leaving=i===visible-1?fade:0;return {x:-i*compression-7*leaving,opacity:i<visible?1-leaving:0,blur:2.8*leaving,hidden:i>=visible||leaving>0};});
}
