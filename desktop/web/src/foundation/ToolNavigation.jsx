import {tr,useLocale} from '../i18n/index.js';
import React,{useId,useLayoutEffect,useRef,useState} from 'react';
import './tool-navigation.css';

// Shared resource-tool tabs: one moving underline, stable text, one live panel.
// Inactive tools remain lazy; their existing query/workspace caches own state.
export function ToolNavigation({label,items,value,onChange,actions,children}){
 useLocale();
 const base=useId(),list=useRef(null),buttons=useRef(new Map());
 const [indicator,setIndicator]=useState(null);
 const signature=items.map(([id,name])=>id+':'+tr(name)).join('|');
 const tabId=id=>base+'-tab-'+encodeURIComponent(id);
 const panelId=id=>base+'-panel-'+encodeURIComponent(id);
 useLayoutEffect(()=>{
  const host=list.current;
  const measure=()=>{const button=buttons.current.get(value),text=button?.querySelector('.tool-label');if(!text)return;
   const rect=text.getBoundingClientRect(),frame=host.getBoundingClientRect();
   // offsetLeft uses CSS pixels even when the application is scaled.
   const scale=host.offsetWidth?frame.width/host.offsetWidth:1;
   setIndicator(old=>{const next={left:(rect.left-frame.left)/scale+host.scrollLeft,width:rect.width/scale};return old&&Math.abs(old.left-next.left)<.1&&Math.abs(old.width-next.width)<.1?old:next});
  };
  measure();const observer=new ResizeObserver(measure);observer.observe(host);
  for(const button of buttons.current.values())observer.observe(button);
  return()=>observer.disconnect();
 },[value,signature]);
 function keyDown(event){
  if(event.altKey||event.ctrlKey||event.metaKey)return;
  const index=items.findIndex(([id])=>buttons.current.get(id)===document.activeElement);
  if(index<0)return;
  const next=event.key==='ArrowRight'?(index+1)%items.length:event.key==='ArrowLeft'?(index-1+items.length)%items.length:event.key==='Home'?0:event.key==='End'?items.length-1:-1;
  if(next<0)return;event.preventDefault();const id=items[next][0];buttons.current.get(id)?.focus({preventScroll:true});if(id!==value)onChange(id);
 }
 const navigation=<nav ref={list} className="foundation-tool-navigation" role="tablist" aria-label={tr(label)} onKeyDown={keyDown}>
  {items.map(([id,name])=><button type="button" role="tab" key={id} id={tabId(id)} ref={node=>{node?buttons.current.set(id,node):buttons.current.delete(id)}} aria-selected={value===id} aria-controls={panelId(id)} tabIndex={value===id?0:-1} onClick={()=>{if(id!==value)onChange(id)}}><span className="tool-label">{tr(name)}</span></button>)}
  <span className="foundation-tool-indicator" aria-hidden="true" style={indicator?{width:indicator.width,transform:'translateX('+indicator.left+'px)'}:{visibility:'hidden'}}/>
 </nav>;
 return <><div className="foundation-tool-row">{navigation}{actions&&<div className="foundation-tool-actions">{actions}</div>}</div>
  {children!==undefined&&items.map(([id])=>id===value?<div key={id} role="tabpanel" className="foundation-tool-panel" id={panelId(id)} aria-labelledby={tabId(id)}>{children}</div>:<div key={id} role="tabpanel" id={panelId(id)} aria-labelledby={tabId(id)} hidden/>)}</>;
}
