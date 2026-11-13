import {t,tr,useLocale} from '../i18n/index.js';
import React,{useEffect,useLayoutEffect,useRef,useState,useId} from 'react';
import {createPortal} from 'react-dom';
import {RotateCcw} from 'lucide-react';
import {Button} from '../foundation/ui';
import {useDismissableLayer} from '../foundation/overlay';
import {contrast} from '../appearance/resolve-appearance';
import {taskbarMetrics} from './taskbar-layout';
import {parts,hex,fromHsv,toHsv} from './taskbar-colors.mjs';
const labels=Object.fromEntries(taskbarMetrics.map(([id,label])=>[id,label]));
const defaults={label:'var(--widget-value,var(--text))',reading:'var(--widget-value,var(--text))',graph:'var(--widget-accent,var(--green))'};
const palettes={dark:['#F1F3F6','#8DBCF5','#67D4DC','#8DDF9F','#E4C471','#F3A997','#F5A9CB','#C7AFFB'],light:['#222731','#215DB1','#007580','#28754E','#845000','#A44027','#A13D6A','#6840B1']};
function actualColor(expression){
 const probe=document.createElement('i');probe.hidden=true;probe.style.color=expression;document.body.append(probe);
 const css=getComputedStyle(probe).color;probe.remove();
 // CSS tokens can compute to either rgb() or color(srgb ...) on different skins.
 const canvas=document.createElement('canvas');canvas.width=canvas.height=1;
 const ctx=canvas.getContext('2d');ctx.fillStyle=css;ctx.fillRect(0,0,1,1);
 return '#'+Array.from(ctx.getImageData(0,0,1,1).data).slice(0,3).map(v=>v.toString(16).padStart(2,'0')).join('').toUpperCase();
}
function Picker({target,original,preview,finish,reduced}){useLocale();
 const initial=original??target.fallback;
 const [state,setState]=useState({hsv:toHsv(initial),text:initial,value:original});
 const ref=useRef(null),anchor=useRef(target.anchor),entry=useRef(null),drag=useRef(null),id=useId();
 const [pos,setPos]=useState(null);
 const cancel=(focus=false)=>{finish(false);if(focus)anchor.current?.focus({preventScroll:true})};
 const accept=value=>{finish(true,value);anchor.current?.focus({preventScroll:true})};
 useDismissableLayer({refs:[anchor,ref],onDismiss:reason=>cancel(reason==='escape')});
 useLayoutEffect(()=>{
  const place=()=>{const el=ref.current,b=anchor.current?.getBoundingClientRect();if(!el||!b)return;
   const w=el.offsetWidth,h=el.offsetHeight,edge=8,gap=7;
   const beside=b.right+gap+w<=window.innerWidth-edge;
   const x=beside?b.right+gap:b.left-gap-w>=edge?b.left-gap-w:Math.min(b.left,window.innerWidth-w-edge);
   const y=beside||x+w<=b.left?b.top-8:b.bottom+gap+h<=window.innerHeight-edge?b.bottom+gap:b.top-h-gap;
   setPos({left:Math.max(edge,Math.min(x,window.innerWidth-w-edge)),top:Math.max(edge,Math.min(y,window.innerHeight-h-edge))});
  };
  place();const observer=new ResizeObserver(place);observer.observe(ref.current);window.addEventListener('resize',place);window.addEventListener('scroll',place,true);
  return()=>{observer.disconnect();window.removeEventListener('resize',place);window.removeEventListener('scroll',place,true)};
 },[]);
 useLayoutEffect(()=>{entry.current?.focus({preventScroll:true})},[]);
 function keepFocus(e){
  if(e.key!=='Tab')return;
  const items=[...ref.current.querySelectorAll('button:not(:disabled),input,[tabindex="0"]')];
  const first=items[0],last=items.at(-1);
  if(e.shiftKey&&document.activeElement===first){e.preventDefault();last?.focus({preventScroll:true});}
  if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first?.focus({preventScroll:true});}
 }
 function change(hsv,instant=false){const value=fromHsv(...hsv);setState({hsv,text:value,value});preview(value,instant)}
 function type(text){const value=hex(text);setState(s=>({...s,text,value:value??s.value,hsv:value?toHsv(value):s.hsv}));if(value)preview(value,false)}
 function point(e){const r=e.currentTarget.getBoundingClientRect(),unit=n=>Math.min(1,Math.max(0,n));change([state.hsv[0],unit((e.clientX-r.left)/r.width),unit(1-(e.clientY-r.top)/r.height)],true)}
 function arrow(e){const dx={ArrowLeft:-1,ArrowRight:1}[e.key]??0,dy={ArrowDown:-1,ArrowUp:1}[e.key]??0;if(!dx&&!dy)return;e.preventDefault();const delta=e.shiftKey ? .1 : .02;change([state.hsv[0],Math.max(0,Math.min(1,state.hsv[1]+dx*delta)),Math.max(0,Math.min(1,state.hsv[2]+dy*delta))],true)}
 const [h,s,v]=state.hsv, invalid=!hex(state.text),color=hex(state.text)??initial;
 const weak=state.value&&contrast(color,target.background)<(target.part==='graph'?3:4.5);
 const scheme=document.documentElement.dataset.appearanceMode==='light'?'light':'dark';
 return createPortal(<div ref={ref} id={id} className="tb-color-picker" onKeyDown={keepFocus} role="dialog" aria-label={tr(`${labels[target.metric]} · ${target.label}颜色`)} data-reduced={reduced} style={pos??{left:0,top:0,visibility:'hidden'}}>
  <header><strong>{tr(labels[target.metric])}</strong><span>{tr(target.label)}{t("颜色")}</span></header>
  <div className="tb-color-sv" role="button" tabIndex={0} aria-label={t("饱和度与明度，方向键微调")} style={{'--hue':fromHsv(h,1,1)}} onKeyDown={arrow} onPointerDown={e=>{if(e.button!==0||!e.isPrimary)return;drag.current=e.pointerId;e.currentTarget.setPointerCapture(e.pointerId);point(e)}} onPointerMove={e=>{if(drag.current===e.pointerId)point(e)}} onLostPointerCapture={()=>{drag.current=null}}><i style={{left:`${s*100}%`,top:`${(1-v)*100}%`,background:color}}/></div>
  <input className="tb-color-hue" type="range" min="0" max="359" value={Math.round(h)%360} aria-label={t("色相")} onChange={e=>change([+e.target.value,s,v],true)}/>
  <div className="tb-color-presets" aria-label={t("推荐色板")}>{tr(palettes[scheme].map(c=><button type="button" key={c} title={tr(c)} aria-label={tr(`色板 ${c}`)} aria-pressed={state.value===c} style={{'--chip':c}} onClick={()=>change(toHsv(c))}/>))}</div>
  <div className="tb-color-hex"><label htmlFor={id+'-hex'}>HEX</label><input id={id+'-hex'} ref={entry} value={state.text} aria-invalid={invalid} spellCheck={false} maxLength={8} onChange={e=>type(e.target.value)} onKeyDown={e=>{if(e.key==='Enter'&&!invalid){e.preventDefault();accept(state.value)}}}/><span style={{background:color}} aria-hidden="true"/></div>
  <div className="tb-color-note" aria-live="polite">{tr(invalid?'请输入 6 位十六进制色值':weak?'与当前预览底色对比偏低':state.value?'上方预览同步更新':'当前使用默认颜色')}</div>
  <footer><Button icon={RotateCcw} className="tb-color-default" disabled={state.value===null} onClick={()=>{setState({hsv:toHsv(target.fallback),text:target.fallback,value:null});preview(null,false)}}>{t("恢复默认")}</Button><Button onClick={()=>cancel(true)}>{t("取消")}</Button><Button tone="primary" className="tb-color-confirm" disabled={invalid} onClick={()=>accept(state.value)}>{t("确定")}</Button></footer>
 </div>,document.body);
}
export function ColorPane({metrics,categoryIndex,navigation,selected,colorMap,previewMap,changeColor,previewColor,reduced}){useLocale();
 const [target,setTarget]=useState(null),scroll=useRef(null);

 function done(commit,value){if(commit)changeColor(target.metric,target.part,value);previewColor(null);setTarget(null)}
 useLayoutEffect(()=>{const view=scroll.current;if(!view)return;const row=[...view.querySelectorAll('[data-color-metric]')].find(el=>el.dataset.colorMetric===selected);view.scrollTop=row?Math.max(0,row.offsetTop-view.clientHeight/2+row.offsetHeight/2):0},[categoryIndex,selected]);
 return <div className="tb-color-colors tb42-library-body">
  {tr(navigation)}
  <div className="tb-color-table" role="table" aria-label={t("按指标设置颜色")}>
   <div className="tb-color-head tb-color-row" role="row"><span role="columnheader">{t("指标")}</span>{tr(parts.map(([p,label])=><span key={p} role="columnheader">{tr(label)}</span>))}</div>
   <div className="tb-color-scroll" role="rowgroup" ref={scroll} key={categoryIndex} data-reduced={reduced}>{tr(metrics.map(metric=><div key={metric} className="tb-color-row" role="row" data-color-metric={metric} data-selected={metric===selected}>
    <span role="rowheader" title={tr(labels[metric])}>{tr(labels[metric])}</span>
    {tr(parts.map(([part,label])=>{const c=previewMap?.[metric]?.[part],active=target?.metric===metric&&target?.part===part;return <span key={part} role="cell"><button className="tb-color-chip" type="button" data-default={!c} aria-label={tr(`${labels[metric]} · ${label}颜色`)} aria-expanded={active} aria-haspopup="dialog" title={tr(c??'跟随皮肤默认')} onClick={e=>{previewColor(null);setTarget(active?null:{metric,part,label,anchor:e.currentTarget,fallback:actualColor(defaults[part]),background:actualColor('var(--surface)')})}} style={{'--chip':c??defaults[part]}}><i/></button></span>}))}
   </div>))}</div>
  </div>
  {tr(target&&<Picker key={`${target.metric}:${target.part}`} target={target} original={colorMap?.[target.metric]?.[target.part]??null} reduced={reduced} finish={done} preview={(value,instant)=>previewColor({metric:target.metric,part:target.part,value,instant})}/>)}
 </div>;
}
