import {t,tr,useLocale} from '../i18n/index.js';
import React,{useEffect,useLayoutEffect,useRef,useState,useId} from 'react';
import {ChevronLeft,ChevronRight,Undo2,RotateCcw,ArrowLeftRight,Grip,X,Tag,Activity} from 'lucide-react';
import {numberMotionOptions} from '../number-motion';
import {ThemedSelect} from '../themed-select';
import {native} from './bridge';
import {taskbarMetrics,taskbarDefaults,taskbarRefreshIntervals,taskbarRefreshMs,taskbarCellWidth,taskbarAutoPageIntervals} from './taskbar-layout';
import {migrateLayout,editLayout,displayModule,groups,moduleLabel,nameVariants,variantLabel,partLabels} from './taskbar-composer';
import './taskbar-settings.css';
import './taskbar-colors.css';
import {tabs,moveTab,withColor} from './taskbar-colors.mjs';
import {ColorPane} from './TaskbarColorPane';
import {createTaskbarPreviewFeed} from './taskbar-preview';
import {Readout,taskbarPreviewFootprint,taskbarLabelFont,taskbarDigitFont,taskbarUnitFont} from './TaskbarReadout';
import {centerTaskbarInk} from './taskbar-ink';
const dragType='application/x-asterhub-taskbar-metric';
const details=Object.fromEntries(taskbarMetrics.map(row=>[row[0],row]));
const ease='cubic-bezier(.22,.74,.28,1)';
const categoryFont='400 13px Inter,"HarmonyOS Sans SC",sans-serif';
const pageCellWidth=page=>taskbarCellWidth(page.grid.columns,page.grid.columns!==3&&page.slots.some(s=>s.module?.parts.length===2));
const tuningParts=[['labelX','名称'],['numberX','数字'],['unitX','单位'],['graphX','图形']];
const step=.125,limit=6;
const tuneValue=value=>Math.max(-limit,Math.min(limit,Math.round(value/step)*step));
const formatOffset=value=>`${value>0?'+':value<0?'−':''}${Math.abs(value)}`;
function TypographyRail({part,name,value,onStep,onPreview,onFinish}){useLocale();
 const active=useRef(null),valueButton=useRef(null),entry=useRef(null);const [draft,setDraft]=useState(null);
 useEffect(()=>()=>active.current?.controller.abort(),[]);
 const start=e=>{
  if(!e.isPrimary||e.button!==0)return;
  active.current?.controller.abort();
  const controller=new AbortController(),gesture={pointer:e.pointerId,value,controller};active.current=gesture;
  const end=(event,cancel)=>{
   if(event.pointerId!==gesture.pointer)return;
   controller.abort();active.current=null;onFinish(part,gesture.value,cancel||gesture.cancelled);
  };
  window.addEventListener('pointerup',event=>end(event,false),{signal:controller.signal});
  window.addEventListener('pointercancel',event=>end(event,true),{signal:controller.signal});
  window.addEventListener('keydown',event=>{
   if(event.key!=='Escape')return;
   event.preventDefault();event.stopPropagation();gesture.cancelled=true;onFinish(part,gesture.value,true);
  },{capture:true,signal:controller.signal});
  window.addEventListener('blur',()=>{controller.abort();active.current=null;onFinish(part,gesture.value,true)},{signal:controller.signal});
 };
 const change=e=>{
  const next=Number(e.currentTarget.value);
  if(active.current){if(!active.current.cancelled){active.current.value=next;onPreview(part,next)}}else onStep(part,next);
 };
 function edit(){if(!entry.current)entry.current={done:false};setDraft(String(value));}
 function endEdit(text,save,restore=false){if(!entry.current||entry.current.done)return;entry.current.done=true;entry.current=null;setDraft(null);if(save&&text.trim()!==''&&Number.isFinite(Number(text)))onStep(part,tuneValue(Number(text)));if(restore)requestAnimationFrame(()=>valueButton.current?.focus());}
 const inputValue=e=>{e.currentTarget.focus();e.currentTarget.select();};
 return <div className="tb42-tune-rail">
  <span className="tb42-tune-name">{tr(name)}</span>
  <div className="tb42-tune-axis" style={{'--tb-tune-pos':`${(tuneValue(value)+limit)/(limit*2)*100}%`}}>
   <span className="tb42-tune-track" aria-hidden="true"><i/></span>
   <input type="range" min={-limit} max={limit} step={step} aria-label={tr(`${name}横向偏移`)} aria-valuetext={tr(`${formatOffset(value)} 像素`)} value={value} onPointerDown={start} onChange={change}/>
  </div>
  <div className="tb43-rail-step">
   <button type="button" className="tb43-rail-nudge" aria-label={tr(`${name}左移 0.125 像素`)} disabled={value<=-limit} onClick={()=>onStep(part,tuneValue(value-step))}>−</button>
   {tr(draft===null?<button ref={valueButton} type="button" className="tb42-tune-value" aria-label={tr(`编辑${name}偏移`)} title={t("双击输入像素值")} onDoubleClick={edit} onKeyDown={e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();edit()}}}><span>{tr(formatOffset(value))}</span><small>px</small></button>:<label className="tb42-tune-value tb43-tune-entry"><input autoFocus inputMode="decimal" aria-label={tr(`${name}偏移像素值`)} value={draft} onFocus={inputValue} onChange={e=>setDraft(e.target.value)} onBlur={e=>endEdit(e.target.value,true)} onKeyDown={e=>{if(e.key!=='Enter'&&e.key!=='Escape')return;e.preventDefault();e.stopPropagation();endEdit(e.currentTarget.value,e.key==='Enter',true)}}/><small>px</small></label>)}
   <button type="button" className="tb43-rail-nudge" aria-label={tr(`${name}右移 0.125 像素`)} disabled={value>=limit} onClick={()=>onStep(part,tuneValue(value+step))}>+</button>
  </div>
 </div>;
}
function PageStepIcon({add=false}){useLocale();
 return <svg width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.35" strokeLinecap="round" aria-hidden="true"><path d={add?'M8 3v10M3 8h10':'M3 8h10'}/></svg>;
}
function CategoryInk({name,ready}){useLocale();
 const {x,y}=ready?centerTaskbarInk(name,categoryFont,28,17):{x:28,y:22};
 return <svg width="56" height="34" viewBox="0 0 56 34" aria-hidden="true"><text x={x} y={y} fill="currentColor" style={{font:categoryFont}}>{tr(name)}</text></svg>;
}

export function TaskbarSettings({value,savedValue,reduceMotion=false,onChange}){useLocale();
 const config={...taskbarDefaults(),...value,background:false};const layout=migrateLayout(config);
 const numberMotion=config.motion!==false&&numberMotionOptions.some(([id])=>id===config.numberMotion)?config.numberMotion:'direct';
 const refreshMs=taskbarRefreshMs(config.refreshMs);
 const [pageId,setPageId]=useState(layout.activePageId),[slotId,setSlotId]=useState(null),[category,setCategory]=useState('CPU');
 const categoryIndex=groups.findIndex(([name])=>name===category),categoryDirection=useRef(1),shelfScroll=useRef(null);
 const [editorMode,setEditorMode]=useState('components');
 const [colorDraft,setColorDraft]=useState(null);
 const colorMap=layout.colors??{};
 const previewColors=colorDraft?withColor(colorMap,colorDraft.metric,colorDraft.part,colorDraft.value):colorMap;
 const editorRef=useRef(null);
 useLayoutEffect(()=>{
  if(editorMode!=='colors')return;
  const editor=editorRef.current,body=editor?.closest('.native-preferences-body');if(!body)return;
  const bounds=editor.getBoundingClientRect(),viewport=body.getBoundingClientRect();
  const shift=Math.min(bounds.bottom-viewport.bottom+5,bounds.top-viewport.top-6);
  if(shift>0)body.scrollTop+=Math.ceil(shift);
 },[editorMode]);
 const [libraryFocus,setLibraryFocus]=useState(null);
 const chooseCategory=(name,index)=>{setLibraryFocus(null);if(index===categoryIndex)return;categoryDirection.current=index>categoryIndex?1:-1;setCategory(name)};
 useLayoutEffect(()=>{
  const viewport=shelfScroll.current;if(!viewport)return;
  const item=libraryFocus&&Array.from(viewport.querySelectorAll('[data-metric]')).find(el=>el.dataset.metric===libraryFocus.metric);
  if(editorMode!=='components')return;
  if(!item){viewport.scrollTop=0;return;}
  const rows=viewport.querySelector('.tb42-shelf-rows');
  const target=Math.max(0,item.getBoundingClientRect().top-rows.getBoundingClientRect().top-(viewport.clientHeight-item.offsetHeight)/2);
  viewport.scrollTo({top:target,behavior:'instant'});
  if(!reduceMotion&&!matchMedia('(prefers-reduced-motion: reduce)').matches)item.animate([{opacity:.5},{opacity:1}],{duration:260,easing:ease});
 },[category,libraryFocus,editorMode]);
 const [devices,setDevices]=useState([]),[error,setError]=useState(''),[message,setMessage]=useState(''),[snapshot,setSnapshot]=useState(null),[series,setSeries]=useState({});
 const [fontsReady,setFontsReady]=useState(false),[typeScope,setTypeScope]=useState('global'),[undo,setUndo]=useState([]),[moveFrom,setMoveFrom]=useState(null),[dragging,setDragging]=useState(false),[over,setOver]=useState(null);
 const [systemReduced,setSystemReduced]=useState(()=>matchMedia('(prefers-reduced-motion: reduce)').matches);
 const [carouselWidth,setCarouselWidth]=useState(null),[tuningDraft,setTuningDraft]=useState(null);
 const tabsId=useId(),modeRail=useRef(null);
 useLayoutEffect(()=>{
  const rail=modeRail.current;if(!rail)return;
  const buttons=[...rail.querySelectorAll('[role="tab"]')];
  const place=()=>{
   const selected=buttons.find(button=>button.getAttribute('aria-selected')==='true');
   if(!selected)return;
   rail.style.setProperty('--mode-left',selected.offsetLeft+'px');
   rail.style.setProperty('--mode-width',selected.offsetWidth+'px');
  };
  place();const resize=new ResizeObserver(place);
  resize.observe(rail);buttons.forEach(button=>resize.observe(button));
  return()=>resize.disconnect();
 },[editorMode]);
 const finishTuning=useRef(null);
 const canAutoPage=layout.pages.length>1;
 const stage=useRef(null),carousel=useRef(null),preview=useRef(null),wheelNav=useRef(null),drag=useRef(null),tracks=useRef({}),positions=useRef(new Map()),previousPage=useRef(pageId),confirmDelete=useRef(null),baseline=useRef(savedValue??config),revision=useRef(layout.revision);
 const page=layout.pages.find(p=>p.id===pageId)??layout.pages[0];const selected=page.slots.find(s=>s.id===slotId)??page.slots[0];
 const reduced=reduceMotion||systemReduced;
 const pageIndex=layout.pages.findIndex(p=>p.id===page.id);
 const widths=layout.pages.map(p=>p.grid.columns*pageCellWidth(p)+4);
 const previewScale=carouselWidth==null?1:Math.min(1,Math.max(0,carouselWidth-2)/Math.max(...widths));
 const offsetsX=widths.map((_,i)=>{
  let shift=0;
  for(let j=Math.min(i,pageIndex);j<Math.max(i,pageIndex);j++)shift+=(widths[j]/2+widths[j+1]/2+28)*Math.sign(i-pageIndex);
  return shift*previewScale;
 });
 useEffect(()=>{if(revision.current!==layout.revision){revision.current=layout.revision;baseline.current=config;setUndo([]);setMessage('');}},[layout.revision]);
 useEffect(()=>{const query=matchMedia('(prefers-reduced-motion: reduce)');const change=()=>setSystemReduced(query.matches);query.addEventListener('change',change);return()=>query.removeEventListener('change',change)},[]);
 useEffect(()=>{let alive=true;Promise.all([document.fonts.load(taskbarLabelFont,'CPU GPU'),document.fonts.load(taskbarDigitFont,'0123456789'),document.fonts.load(taskbarUnitFont,'% °C GHz'),document.fonts.load('400 13px "HarmonyOS Sans SC"','内存'),document.fonts.load(categoryFont,'CPU GPU 内存网络存储')]).then(()=>alive&&setFontsReady(true));native.request('devices.list').then(v=>alive&&setDevices((v?.items||[]).map(row=>row.device))).catch(e=>alive&&setError(e.message));return()=>{alive=false}},[]);
 useEffect(()=>{setSnapshot(null);tracks.current={};setSeries({})},[config.deviceId]);
 useEffect(()=>createTaskbarPreviewFeed(native,config.deviceId,next=>{
  setSnapshot(next);if(!next){tracks.current={};setSeries({});return;}
  let changed=false;
  for(const [id,v] of Object.entries(next.values||{})){
   if(v.stale||!Number.isFinite(v.value)){if(tracks.current[id]){delete tracks.current[id];changed=true}continue;}
   const last=tracks.current[id];if(last?.time===v.timeUtc&&last.unit===v.unit)continue;
   tracks.current[id]={time:v.timeUtc,unit:v.unit,values:[...(last?.unit===v.unit?last.values.slice(-12):[]),v.value]};changed=true;
  }
  if(changed)setSeries(Object.fromEntries(Object.entries(tracks.current).map(([id,t])=>[id,t.values])));
 },{minInterval:refreshMs}),[config.deviceId,refreshMs]);
 useLayoutEffect(()=>{
  const node=carousel.current;const measure=()=>setCarouselWidth(node.clientWidth);
  const observer=new ResizeObserver(measure);observer.observe(node);measure();return()=>observer.disconnect();
 },[]);
 useLayoutEffect(()=>{
  const next=new Map();preview.current?.querySelectorAll('[data-module]').forEach(node=>{
   const slot=node.closest('.tb42-slot'),r={x:slot.offsetLeft,y:slot.offsetTop},old=positions.current.get(node.dataset.module);next.set(node.dataset.module,r);
   if(!reduced&&old&&previousPage.current===page.id&&(Math.abs(old.x-r.x)>1||Math.abs(old.y-r.y)>1))node.animate([{transform:`translate(${old.x-r.x}px,${old.y-r.y}px)`,opacity:.66},{transform:'translate(0,0)',opacity:1}],{duration:380,easing:ease});
  });if(!reduced&&previousPage.current!==page.id)preview.current?.animate([{opacity:.25,filter:'blur(3px)',transform:'translateX(8px)'},{opacity:1,filter:'blur(0)',transform:'translateX(0)'}],{duration:360,easing:ease});positions.current=next;previousPage.current=page.id;
 },[page.id,page.slots,reduced]);
 function clearDrag(){drag.current=null;setDragging(false);setOver(null)}
 useEffect(()=>{function cancel(e){if(e.key!=='Escape'||!drag.current&&!moveFrom)return;e.preventDefault();e.stopPropagation();clearDrag();setMoveFrom(null)}window.addEventListener('keydown',cancel,true);return()=>window.removeEventListener('keydown',cancel,true)},[moveFrom]);
 const update=patch=>onChange({...config,...patch});
 function commit(action){
  const next=editLayout(layout,page.id,action);setMessage(next.message);if(next.focus)setSlotId(next.focus);
  if(next.layout!==layout){setUndo(items=>[...items.slice(-24),{layout,pageId:page.id,slotId:selected.id}]);update({layout:next.layout});setPageId(next.pageId);setSlotId(next.focus);confirmDelete.current=null;}
 }
 function choosePage(id){
  if(id===page.id||drag.current)return;
  if(preview.current?.contains(document.activeElement))stage.current?.focus({preventScroll:true});
  setPageId(id);setSlotId(null);setMoveFrom(null);setMessage('');confirmDelete.current=null;
 }
 function turnPage(direction){const target=layout.pages[pageIndex+direction];if(target)choosePage(target.id);}
 wheelNav.current={turnPage,count:layout.pages.length};
 useEffect(()=>{
  const node=stage.current;let last=-Infinity,remainder=0;
  function wheel(e){
   if(e.ctrlKey||drag.current||wheelNav.current.count<2)return;
   const delta=Math.abs(e.deltaX)>Math.abs(e.deltaY)?e.deltaX:e.deltaY;if(!delta)return;
   e.preventDefault();
   const now=performance.now();if(now-last<300)return;
   remainder=(Math.sign(remainder)===Math.sign(delta)?remainder:0)+delta*(e.deltaMode===1?16:e.deltaMode===2?80:1);
   if(Math.abs(remainder)<22)return;
   wheelNav.current.turnPage(Math.sign(remainder));last=now;remainder=0;
  }
  node.addEventListener('wheel',wheel,{passive:false});return()=>node.removeEventListener('wheel',wheel);
 },[]);
 function chooseSlot(id){if(moveFrom&&moveFrom!==id){commit({type:'swap',from:moveFrom,to:id});setMoveFrom(null)}else{setSlotId(id);setMessage('');const metric=page.slots.find(s=>s.id===id)?.module?.metricId;const index=groups.findIndex(([,ids])=>ids.includes(metric));if(index>=0){categoryDirection.current=index>=categoryIndex?1:-1;setCategory(groups[index][0]);setLibraryFocus({metric,request:performance.now()})}}}
 function undoEdit(){const last=undo.at(-1);if(!last)return;update({layout:{...last.layout,colors:layout.colors}});setPageId(last.pageId);setSlotId(last.slotId);setUndo(undo.slice(0,-1));setMessage('')}
 function start(event,source,ghost){event.stopPropagation();drag.current=source;setDragging(true);event.dataTransfer.setData(dragType,'widget');event.dataTransfer.effectAllowed='move';if(ghost)event.dataTransfer.setDragImage(ghost,Math.round(ghost.offsetWidth/2),Math.round(ghost.offsetHeight/2));}
 function dropTarget(event,slot,index){if(!drag.current)return;event.preventDefault();event.stopPropagation();const source=drag.current;clearDrag();if(source.kind==='library')commit({type:'library',metric:source.metric,part:source.part,variant:source.variant,to:slot.id,index});
  else if(source.from!==slot.id)commit({type:'swap',from:source.from,to:slot.id});else if(source.index!=null&&index!=null&&index<slot.module.parts.length&&source.index!==index)commit({type:'order',from:slot.id});}
 function accept(event,target){if(!drag.current)return;event.preventDefault();event.stopPropagation();event.dataTransfer.dropEffect='move';setOver(target)}
 function deletePage(){const confirmed=confirmDelete.current===page.id;commit({type:'delete',confirm:confirmed});confirmDelete.current=!confirmed&&page.slots.some(s=>s.module)?page.id:null;}
 const widget=(module,width=88,peer)=> <Readout colors={previewColors} module={module} peer={peer} snapshot={snapshot} points={series[module.metricId]} width={width} numberMotion={reduced?'direct':numberMotion} chartMotion={!reduced} typography={previewTypography}/>;
 const catalogueName=metric=>{
  const row=details[metric];if(!row)return metric;
  return row[3]?`${row[2]} · ${row[3]}`:row[2];
 };
 const title=module=>catalogueName(module.metricId);
 function tabTo(mode,focus){setEditorMode(mode);setTuningDraft(null);setColorDraft(null);if(focus)document.getElementById(tabsId+'-'+mode)?.focus();}
 function previewColumns(slide,cellWidth){
  const footprints=Array.from({length:slide.grid.columns},()=>[]);
  for(const slot of slide.slots){if(!slot.module)continue;const peer=displayModule(slide.slots.find(x=>x.column===slot.column&&x.row!==slot.row)?.module,slide.grid.columns);
   const range=taskbarPreviewFootprint(displayModule(slot.module,slide.grid.columns),peer,cellWidth,previewTypography);if(range)footprints[slot.column]?.push(range);
  }
  const ink=footprints.map(items=>items.length?{left:Math.min(...items.map(x=>x.left)),right:Math.max(...items.map(x=>x.right))}:null);
  const ranges=ink.map(range=>range??{left:0,right:24});
  const minGap=12,last=ranges.length-1;
  if(last<1)return [{x:0,opacity:1,blur:0,hidden:false}];
  const gaps=ranges.slice(1).map((range,i)=>cellWidth+range.left-ranges[i].right);
  const gap=Math.max(minGap,gaps.reduce((sum,g)=>sum+g,0)/last);
  const inkWidth=ranges.reduce((sum,range)=>sum+range.right-range.left,0)+last*gap;
  let edge=ranges[0].left-Math.max(0,inkWidth-(cellWidth*last+ranges[last].right-ranges[0].left))/2;
  return ranges.map((range,i)=>{const x=edge-range.left-i*cellWidth;edge+=range.right-range.left+gap;return {x,opacity:1,blur:0,hidden:false};});
 }
 const parts=displayModule(selected.module,page.grid.columns)?.parts??[],typeMetric=typeScope==='metric'?selected.module?.metricId:null;
 const offsets=typeMetric?layout.typography?.metrics?.[typeMetric]:layout.typography?.global;
 const draft=tuningDraft?.metric===typeMetric?tuningDraft:null;
 const previewTypography=draft?editLayout(layout,page.id,{type:'typography',metric:typeMetric,part:draft.part,value:draft.value}).layout.typography:layout.typography;
 const tuningKeys=tuningParts.map(([part])=>part);
 const canResetTuning=tuningKeys.some(part=>Number(offsets?.[part])!==0&&Number.isFinite(Number(offsets?.[part])));
 function resetTuning(){setTuningDraft(null);commit({type:'resetTypographyX',from:selected.id,metric:typeMetric});}
 function changeTuning(part,next){if((Number(offsets?.[part])||0)!==next)commit({type:'typography',from:selected.id,metric:typeMetric,part,value:next});}
 finishTuning.current=(part,next,cancel)=>{setTuningDraft(null);if(!cancel)changeTuning(part,next);};
 const categoryNav=label=><nav className="tb42-categories" aria-label={tr(label)} style={{'--tb-cat-index':categoryIndex}}><span className="tb42-category-marker" aria-hidden="true"/>{tr(groups.map(([name],index)=><button type="button" key={name} aria-label={tr(name)} aria-pressed={category===name} onClick={()=>chooseCategory(name,index)}><CategoryInk name={name} ready={fontsReady}/></button>))}</nav>;
 return <section className="taskbar-settings" data-fonts-ready={fontsReady} data-reduced={reduced} data-color-instant={colorDraft?.instant===true}>
  <section className="tb43-monitor-options" aria-labelledby={tabsId+'-monitor'}>
   <header className="tb43-monitor-head"><h3>{t("任务栏监测")}</h3><h4 id={tabsId+'-monitor'}>{t("监测与外观")}</h4></header>
   <div className="native-preferences-fields tb43-monitor-selects">
    <label className="field"><span>{t("监测设备")}</span><ThemedSelect aria-label={t("监测设备")} value={config.deviceId} onChange={e=>update({deviceId:e.target.value})}>
     {tr(!devices.some(d=>d.id===config.deviceId)&&<option value={config.deviceId}>{tr(config.deviceId==='local'?'本机':'原监测设备（不可用）')}</option>)}{devices.map(d=><option key={d.id} value={d.id}>{d.isLocal?t('本机'):d.name}</option>)}</ThemedSelect></label>
    <label className="field"><span>{t("数字变化")}</span><ThemedSelect aria-label={t("任务栏数字变化")} value={numberMotion} onChange={e=>update({numberMotion:e.target.value,motion:e.target.value!=='direct'})}>{tr(numberMotionOptions.map(([id,label])=><option key={id} value={id}>{tr(label)}</option>))}</ThemedSelect></label>
    <label className="field"><span>{t("更新间隔")}</span><ThemedSelect aria-label={t("更新间隔")} value={refreshMs} onChange={e=>update({refreshMs:Number(e.target.value)})}>{tr(taskbarRefreshIntervals.map(ms=><option key={ms} value={ms}>{tr(ms/1000)}{t(" 秒")}</option>))}</ThemedSelect></label>
   </div>
   <div className="tb43-checks">
    <label className="native-setting-check"><input type="checkbox" checked={config.enabled} onChange={e=>update({enabled:e.target.checked})}/>{t("显示监测")}</label>
    <label className="native-setting-check"><input type="checkbox" checked={config.clickDetails===true} onChange={e=>update({clickDetails:e.target.checked})}/>{t("点击打开资源详情")}</label>
  <div className="taskbar-auto-row">
   <label className="native-setting-check"><input type="checkbox" checked={config.autoPage} disabled={!canAutoPage} onChange={e=>update({autoPage:e.target.checked})}/>{t("自动切页")}</label>
   {tr(config.autoPage&&canAutoPage&&<ThemedSelect compact aria-label={t("自动切页间隔")} value={taskbarAutoPageIntervals.includes(config.intervalSeconds)?config.intervalSeconds:taskbarAutoPageIntervals[0]} onChange={e=>update({intervalSeconds:Number(e.target.value)})}>{tr(taskbarAutoPageIntervals.map(seconds=><option key={seconds} value={seconds}>{tr(seconds<60?`${seconds} 秒`:`${seconds/60} 分钟`)}</option>))}</ThemedSelect>)}
  </div>
</div>
   {tr(error&&<p className="native-error" role="alert">{tr(error)}</p>)}
  </section>
  <section ref={editorRef} className="tb42-editor" aria-label={t("任务栏布局编辑")}>
   <header className="tb42-heading tb42-preview-tools"><h4>{t("实时预览")}</h4><div className="tb42-stepper"><button type="button" aria-label={t("减少一页")} title={t("减少一页")} disabled={layout.pages.length<2} onClick={deletePage}><PageStepIcon/></button><button type="button" aria-label={t("增加一页")} title={t("增加一页")} disabled={layout.pages.length>=3} onClick={()=>commit({type:'add'})}><PageStepIcon add/></button></div></header>
   <div className="tb42-stage" ref={stage} tabIndex={0} role="group" aria-label={t("实时预览，左右方向键切换")} onKeyDown={e=>{if(!e.altKey&&!e.ctrlKey&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();turnPage(e.key==='ArrowLeft'?-1:1)}}}>
    <button type="button" className="tb42-arrow" aria-label={t("上一页预览")} title={t("上一页预览")} disabled={pageIndex===0} onClick={()=>turnPage(-1)}><ChevronLeft size={15} strokeWidth={1.6} aria-hidden="true"/></button>
    <div className="tb42-carousel" ref={carousel} style={{'--tb-scale':previewScale}}>
    {tr(layout.pages.map((slide,slideIndex)=>{const active=slide.id===page.id,cellWidth=pageCellWidth(slide),columnFrames=previewColumns(slide,cellWidth);return <div className="tb42-slide" key={slide.id} data-active={active} data-adjacent={Math.abs(slideIndex-pageIndex)===1} aria-hidden={!active||undefined} inert={active?undefined:''} style={{'--tb-shift':`${offsetsX[slideIndex]}px`}}>
    <div className="tb42-grid" ref={active?preview:null} data-background={false} style={{'--tb-columns':slide.grid.columns,'--tb-cell':`${cellWidth}px`}}>
     {tr(slide.slots.map((slot,i)=>{const module=displayModule(slot.module,slide.grid.columns),col=columnFrames[slot.column];return <div style={{'--tb-lane-x':`${col.x}px`,'--tb-lane-alpha':col.opacity,'--tb-lane-blur':`${col.blur}px`}} inert={col.hidden?'':undefined} aria-hidden={col.hidden||undefined} className={active?'tb42-slot':'tb42-ghost-slot'} key={slot.id} data-slot={slot.id} data-selected={selected.id===slot.id} data-over={over===slot.id} data-moving={moveFrom===slot.id} draggable={active&&!!module} onDragStart={e=>active&&module&&start(e,{kind:'slot',from:slot.id},e.currentTarget)} onDragEnd={clearDrag} onDragOver={e=>accept(e,slot.id)} onDrop={e=>dropTarget(e,slot)}>
      <button type="button" className="tb42-cell" aria-pressed={selected.id===slot.id} aria-label={tr(`位置 ${i+1}，${module?title(module):'空位置'}`)} onClick={()=>chooseSlot(slot.id)}>
       {tr(module?<span className="tb42-ink" data-module={module.id}>{tr(widget(module,cellWidth,displayModule(slide.slots.find(other=>other.column===slot.column&&other.row!==slot.row)?.module,slide.grid.columns)))}</span>:<span className="tb42-empty">＋</span>)}
      </button>
      {tr(active&&module&&<div className="tb42-parts">{tr((slide.grid.columns===3?[0]:[0,1]).map(index=>{
       const part=module.parts[index];if(!part&&!dragging)return null;return <span className="tb42-part" key={index} data-part={part??'empty'} title={tr(part?partLabels[part]:'添加第二项')} draggable={!!part} onDragStart={part?e=>start(e,{kind:'slot',from:slot.id,index},e.currentTarget.closest('.tb42-slot')):undefined} onDragEnd={clearDrag} onClick={()=>chooseSlot(slot.id)} onDragOver={e=>accept(e,slot.id)} onDrop={e=>dropTarget(e,slot,index)}/>;
      }))}</div>)}
     </div>}))}
    </div>
    </div>}))}
    </div>
    <button type="button" className="tb42-arrow" aria-label={t("下一页预览")} title={t("下一页预览")} disabled={pageIndex===layout.pages.length-1} onClick={()=>turnPage(1)}><ChevronRight size={15} strokeWidth={1.6} aria-hidden="true"/></button>
   </div>
   <div className="tb42-tools">
    <div className="tb42-grid-picker" role="group" aria-label={t("预览格数")}>{tr([[1,'一列'],[2,'两列'],[3,'三列']].map(([n,label])=><button type="button" key={n} data-cols={n} aria-label={tr(label)} title={tr(label)} aria-pressed={page.grid.columns===n} onClick={()=>commit({type:'grid',columns:n})}><span aria-hidden="true" className="tb42-grid-icon">{tr(Array.from({length:n*2},(_,i)=><i key={i}/>))}</span></button>))}</div>
    <nav className="tb42-pages" aria-label={t("预览页导航")} data-single={layout.pages.length===1}>{tr(layout.pages.map((p,i)=><button key={p.id} type="button" aria-label={tr(`第 ${i+1} 页`)} aria-current={p.id===page.id?'page':undefined} onClick={()=>choosePage(p.id)} onKeyDown={e=>{if(e.altKey&&['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();commit({type:'movePage',delta:e.key==='ArrowLeft'?-1:1})}}}/>))}</nav>
   </div>
   <div className="tb43-mode-bar">
    <div ref={modeRail} role="tablist" className="tb43-modes" data-reduced={reduced} aria-label={t("任务栏编辑方式")} onKeyDown={e=>{if(!['ArrowLeft','ArrowRight','Home','End'].includes(e.key))return;e.preventDefault();tabTo(moveTab(editorMode,e.key),true)}}>
     <span className="tb43-mode-indicator" aria-hidden="true"/>
     {tr(tabs.map(([mode,label])=><button type="button" key={mode} role="tab" id={tabsId+'-'+mode} aria-controls={tabsId+'-pane-'+mode} aria-selected={mode===editorMode} tabIndex={mode===editorMode?0:-1} onClick={()=>tabTo(mode,false)}>{tr(label)}</button>))}
    </div>
    <div className="tb42-edit-actions" hidden={editorMode!=='components'}>
     {tr(selected.module&&<><button type="button" aria-label={t("交换位置")} title={tr(moveFrom?'选目标位置':'交换位置')} aria-pressed={!!moveFrom} onClick={()=>setMoveFrom(moveFrom?null:selected.id)}><Grip size={14}/></button>{tr(parts.length===2&&<button type="button" aria-label={t("换序")} title={t("数字与图形换序")} onClick={()=>commit({type:'order',from:selected.id})}><ArrowLeftRight size={13}/></button>)}<button type="button" aria-label={t("移除")} title={t("移除选中的模块")} onClick={()=>commit({type:'remove',from:selected.id})}><X size={14}/></button></>)}
     <button type="button" aria-label={t("撤销")} title={t("撤销")} disabled={!undo.length} onClick={undoEdit}><Undo2 size={14}/></button><button type="button" aria-label={t("还原草稿")} title={t("还原草稿")} disabled={JSON.stringify(layout)===JSON.stringify(migrateLayout(savedValue??baseline.current))} onClick={()=>{baseline.current=savedValue??baseline.current;onChange(baseline.current);setPageId(migrateLayout(baseline.current).activePageId);setSlotId(null);setUndo([]);setMessage('已恢复上次保存的布局。')}}><RotateCcw size={13}/></button>
    </div>
   </div>
   <div className="tb43-editor-panes">
   <section className="tb42-library" role="tabpanel" id={tabsId+'-pane-components'} aria-labelledby={tabsId+'-components'} hidden={editorMode!=='components'} data-returning={dragging&&over==='library'} onDragOver={e=>{if(drag.current?.kind==='slot')accept(e,'library')}} onDrop={e=>{if(drag.current?.kind!=='slot')return;e.preventDefault();const source=drag.current;clearDrag();commit({type:'remove',from:source.from,index:source.index})}}>
    <div className="tb42-library-body">
     {tr(categoryNav('组件分类'))}
     <div className="tb42-shelf"><div className="tb42-palette-legend" aria-hidden="true"><Tag size={10}/><Activity size={11}/></div><div className="tb42-shelf-scroll" ref={shelfScroll} role="region" aria-label={tr(`${category}组件`)} tabIndex={0}><div className="tb42-shelf-rows" key={category} style={{'--tb-cat-direction':categoryDirection.current}}>{tr(groups[categoryIndex][1].map(metric=>{
      const item=layout.shelf?.[metric]??{metricId:metric,name:{variant:nameVariants(metric)[0]},parts:['number']};const metricName=catalogueName(metric);
      const source=(extra={})=>({kind:'library',metric,...extra});const insert=(extra={})=>commit({type:'library',metric,to:selected.id,...extra});
      return <div className="tb42-library-item" key={metric} data-metric={metric} data-located={libraryFocus?.metric===metric} aria-label={tr(metricName)}>
       <div className="tb42-library-names">{tr(nameVariants(metric).map(variant=><button type="button" key={variant} data-variant={variant} draggable title={`${tr(metricName)}: ${tr(variantLabel[variant])}`} aria-label={`${tr(metricName)} ${tr(variantLabel[variant])}`} onDragStart={e=>start(e,source({variant}))} onDragEnd={clearDrag} onClick={()=>insert({variant})}><Readout colors={previewColors} module={{...item,name:{variant},parts:[]}} snapshot={snapshot} mode="name"/></button>))}</div>
       <span className="tb42-palette-divider" aria-hidden="true"/>
       <div className="tb42-library-parts">{tr(Object.entries(partLabels).map(([part,label])=><button type="button" key={part} data-library-part={part} title={`${tr(metricName)}: ${tr(label)}`} aria-label={`${tr(metricName)} ${tr(label)}`} draggable onDragStart={e=>start(e,source({part}))} onDragEnd={clearDrag} onClick={()=>insert({part})}><Readout colors={previewColors} module={{...item,parts:[part]}} snapshot={snapshot} points={series[metric]} numberMotion={reduced?'direct':numberMotion} chartMotion={!reduced} mode="part"/></button>))}</div>
      </div>}))}</div></div></div>
    </div>
   </section>
   <section className="tb-color-panel" role="tabpanel" id={tabsId+'-pane-colors'} aria-labelledby={tabsId+'-colors'} hidden={editorMode!=='colors'}>
    {tr(editorMode==='colors'&&<ColorPane metrics={groups[categoryIndex][1]} categoryIndex={categoryIndex} navigation={categoryNav('颜色分类')} selected={libraryFocus?.metric??selected.module?.metricId} colorMap={colorMap} previewMap={previewColors} changeColor={(metric,part,value)=>update({layout:{...layout,colors:withColor(colorMap,metric,part,value)}})} previewColor={setColorDraft} reduced={reduced}/>)}
   </section>
   <section className="tb42-typography" role="tabpanel" id={tabsId+'-pane-tuning'} aria-labelledby={tabsId+'-tuning'} hidden={editorMode!=='tuning'}>
    <div className="tb42-tune-panel">
     <div className="tb43-tune-tools"><div className="tb42-tune-scope"><span>{t("应用范围")}</span><ThemedSelect compact aria-label={t("排版微调范围")} value={typeMetric?'metric':'global'} onChange={e=>setTypeScope(e.target.value)}><option value="global">{t("全部模块")}</option>{tr(selected.module&&<option value="metric">{tr(title(selected.module))}</option>)}</ThemedSelect></div><button type="button" className="tb43-tune-reset" aria-label={t("重置当前范围的排版微调")} disabled={!canResetTuning} onClick={resetTuning}><RotateCcw size={12} aria-hidden="true"/>{t("重置")}</button></div>
     <div className="tb42-tune-grid">
      {tr(tuningParts.map(([part,name])=><TypographyRail key={`${typeMetric??'global'}-${part}`} part={part} name={name} value={draft?.part===part?draft.value:Number(offsets?.[part])||0} onStep={changeTuning} onPreview={(part,value)=>setTuningDraft({metric:typeMetric,part,value})} onFinish={(...args)=>finishTuning.current(...args)}/>))}
     </div>
    </div>
   </section>
   </div>
   <p className="tb42-hint" role="status" hidden={!message&&!moveFrom}>{tr(moveFrom?'选择要交换的位置。':message)}</p>
  </section>
 </section>;
}
