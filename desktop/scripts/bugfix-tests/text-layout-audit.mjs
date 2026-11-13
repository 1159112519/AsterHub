// Geometry inventory of every visible DOM text node. Alignment stays semantic:
// left-aligned copy is never changed to centred copy by this audit.
export function auditVisibleText(){
 const records=[],walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);
 const round=n=>Math.round(n*100)/100;
 const rect=r=>({x:round(r.x),y:round(r.y),width:round(r.width),height:round(r.height)});
 for(let node;node=walker.nextNode();){
  const text=node.textContent.trim(),element=node.parentElement;if(!text||!element)continue;
  if(element.closest('script,style,noscript,[hidden],[aria-hidden="true"],.sr-only'))continue;
  const style=getComputedStyle(element);if(style.visibility!=='visible'||style.display==='none')continue;
  const range=document.createRange();range.selectNodeContents(node);const r=range.getBoundingClientRect();
  if(!r.width||!r.height||r.right<=0||r.left>=innerWidth||r.bottom<=0||r.top>=innerHeight)continue;
  let visible=true,clipped=false,ellipsis=false;
  for(let ancestor=element;ancestor&&ancestor!==document.body;ancestor=ancestor.parentElement){
   const s=getComputedStyle(ancestor),b=ancestor.getBoundingClientRect();
   if(s.display==='none'||s.visibility==='hidden'||Number(s.opacity)===0){visible=false;break}
   if(s.textOverflow==='ellipsis'||s.webkitLineClamp!=='none'&&Number(s.webkitLineClamp)>0)ellipsis=true;
   if(/hidden|clip|scroll|auto/.test(s.overflowX)){
    if(r.right<=b.left||r.left>=b.right){visible=false;break}
    if(r.left<b.left-1.5||r.right>b.right+1.5)clipped=true;
   }
   if(/hidden|clip|scroll|auto/.test(s.overflowY)&&(r.bottom<=b.top||r.top>=b.bottom)){visible=false;break}
  }
  if(!visible)continue;
  const parent=element.getBoundingClientRect();
  records.push({text:text.slice(0,120),tag:element.tagName,className:element.getAttribute('class')||'',align:style.textAlign,font:style.fontFamily,fontSize:style.fontSize,lineHeight:style.lineHeight,textRect:rect(r),parentRect:rect(parent),horizontalClipping:clipped,intentionalTruncation:ellipsis});
 }
 return records;
}
