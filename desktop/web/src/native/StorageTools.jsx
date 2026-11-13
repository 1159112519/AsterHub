import {t,tr,useLocale,locale} from '../i18n/index.js';
import {storageTaskFocus} from '../foundation/task-focus';
import React,{useState,useEffect,useLayoutEffect,useMemo,useRef,useSyncExternalStore,useId} from 'react';
import {Toolbar} from '../foundation/ui';
import {ToolNavigation} from '../foundation/ToolNavigation';
import {useDismissableLayer} from '../foundation/overlay';
import {animateDirectoryTransition} from '../foundation/directory-motion';
import {subscribeReducedMotion,reducedMotion} from '../motion';
import {isActiveTask,storageJobs,cleanupJobs,storageStatusResource,storageDeviceState} from './storage-jobs';
import {useFileTransfers,refreshFileTransfers} from './file-transfers';
import {isActiveTransfer} from './task-center-model';
import {HardDrive,ChartNoAxesCombined,FolderOpen,File,ArrowLeft,ArrowRight,ArrowUp,ChevronDown,ChevronRight,Search,LayoutGrid,List,Copy,ClipboardPaste,Trash2,RotateCcw,RefreshCw,Pause,Play,X,ScanLine} from 'lucide-react';
import {StorageScanDialog} from './StorageScanDialog';
import {StorageSizeFilter} from './StorageSizeFilter';
import {storageQueryMinimum} from './storage-size-filter';
import {storageTreeBounds,clampStorageTreeWidth} from './storage-device-state';
import {native} from './bridge';import {Button,FormDialog,Empty} from '../components';import {ThemedSelect} from '../themed-select';import {StoragePage} from './PeripheralPages';import {storageTiles,storageTileRect,commonStorageDirectory,planStorageNavigation,bytesText,tileColor} from './storage-layout';import './storage-tools.css';
const activeJob=isActiveTask;
const states={queued:'等待扫描',enumerating:'正在扫描',aggregating:'整理结果',paused:'已暂停',ready:'扫描完成',cancelled:'扫描已取消',failed:'扫描未完成'};
const kinds={folder:'文件夹',model:'模型文件',archive:'压缩文件',media:'图像与视频',data:'数据库与日志',file:'其他文件'};
export function tileTypography(w,h,name){const short=Math.min(w,h),pad=Math.min(18,Math.max(5,short*.085)),aw=Math.max(0,w-pad*2),ah=Math.max(0,h-pad*2),label=Math.max(14,Math.min(22,Math.sqrt(w*h)*.065));return {pad,label,value:Math.max(14,Math.min(24,label+2)),icon:Math.round(Math.max(16,Math.min(28,short*.15))),showName:aw>=64&&ah>=52,showValue:aw>=60&&ah>=82,showIcon:short>=24};}
export function StorageTools(props){useLocale();return <StorageWorkspace key={props.deviceId} {...props}/>;}
function StorageWorkspace({deviceId,device,sample,samples,settings,active=true,onToolChange}){useLocale();
 const [restored]=useState(()=>storageDeviceState.read(deviceId)||{});
 const restoredSnapshot=useRef(restored.snapshotId??null);
 const [treeWidth,setTreeWidth]=useState(()=>clampStorageTreeWidth(restored.treeWidth)),[treeAvailable,setTreeAvailable]=useState(1000),treeLayout=useRef(null),treeDrag=useRef(null),treeId=useId();
 const treeBounds=storageTreeBounds(treeAvailable),visibleTreeWidth=clampStorageTreeWidth(treeWidth,treeAvailable);
 function releaseTreePointer(event){const drag=treeDrag.current;if(!drag||event?.pointerId!==undefined&&event.pointerId!==drag.id)return;treeDrag.current=null;drag.node.removeAttribute('data-resizing');if(drag.node.hasPointerCapture(drag.id))drag.node.releasePointerCapture(drag.id);}
 function beginTreeResize(event){if(event.button!==0)return;event.preventDefault();resetTransition();releaseTreePointer();const node=event.currentTarget;node.setPointerCapture(event.pointerId);node.dataset.resizing='true';treeDrag.current={id:event.pointerId,node,x:event.clientX,width:visibleTreeWidth};}
 function moveTreeResize(event){const drag=treeDrag.current;if(drag?.id===event.pointerId)setTreeWidth(clampStorageTreeWidth(drag.width+event.clientX-drag.x,treeAvailable));}
 function keyTreeResize(event){let next;const step=event.shiftKey?48:16;if(event.key==='ArrowLeft')next=visibleTreeWidth-step;else if(event.key==='ArrowRight')next=visibleTreeWidth+step;else if(event.key==='Home')next=treeBounds.min;else if(event.key==='End')next=treeBounds.max;else return;event.preventDefault();setTreeWidth(clampStorageTreeWidth(next,treeAvailable));}
 const [tool,setTool]=useState(restored.tool??null),[status,setStatus]=useState(restored.status??null),[path,setPath]=useState(restored.path??''),[data,setData]=useState(restored.data??null),[nodeId,setNodeId]=useState(restored.nodeId??'0'),[layout,setLayout]=useState(restored.layout??'map'),[query,setQuery]=useState(restored.query??''),[kind,setKind]=useState(restored.kind??''),[minimum,setMinimum]=useState(restored.minimum??104857600),[customSize,setCustomSize]=useState(restored.customSize??null),[age,setAge]=useState(restored.age??0),[folders,setFolders]=useState(restored.folders??{}),[expanded,setExpanded]=useState(restored.expanded??new Set(['0'])),[history,setHistory]=useState(restored.history??{entries:[{id:'0',directory:true}],cursor:0}),[offset,setOffset]=useState(restored.offset??0),[selected,setSelected]=useState(new Set()),[clipboard,setClipboard]=useState(null),[error,setError]=useState(''),[notice,setNotice]=useState(restored.notice??''),[busy,setBusy]=useState(false),[loading,setLoading]=useState(false),[dialog,setDialog]=useState(null),[trash,setTrash]=useState([]),[hover,setHover]=useState(null),[menu,setMenu]=useState(null),[refresh,setRefresh]=useState(0),[transfer,setTransfer]=useState(restored.transfer??null),[size,setSize]=useState({w:800,h:440}),[mapTransition,setMapTransition]=useState(null),[navigating,setNavigating]=useState(false);
 const [aggregateView,setAggregateView]=useState(null);
 const mapPages=useRef(new Map()),navigationRevision=useRef(0),navigationResult=useRef(null),outPortal=useRef(),inPortal=useRef();
 const tilePalettes=useRef({}),tileLayouts=useRef(new WeakMap()),folderRequests=useRef(new Set()),folderCache=useRef(restored.folders||{}),generation=useRef(0),requestRevision=useRef(0),stage=useRef(),content=useRef(),transition=useRef(null),animation=useRef(null),hoverId=useRef(null),tooltipNode=useRef(null),contextRef=useRef(),statusRef=useRef(),toolRef=useRef();toolRef.current=tool;statusRef.current=status;
 const preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion),statusResource=useMemo(()=>storageStatusResource(deviceId),[deviceId]);
 const snapshot=status?.snapshot,snapshotId=snapshot?.id,job=status?.job,preparation=status?.preparation;
 const [recycleSelection,setRecycleSelection]=useState(new Set()),[optimistic,setOptimistic]=useState([]),[puffs,setPuffs]=useState([]);
 const optimisticSerial=useRef(0),puffTimer=useRef(null),seenCleanup=useRef(new Set());
 const cleanup=useSyncExternalStore(cleanupJobs.subscribe,cleanupJobs.getSnapshot);
 const deviceCleanup=useMemo(()=>(cleanup.data||[]).filter(task=>task.deviceId===deviceId),[cleanup.data,deviceId]);
 const hiddenIds=useMemo(()=>new Set([...optimistic.filter(task=>task.snapshot===snapshotId).flatMap(task=>task.ids),...deviceCleanup.filter(task=>task.action==='move'&&task.snapshot===snapshotId&&isActiveTask(task)).flatMap(task=>task.targets)]),[optimistic,deviceCleanup,snapshotId]);
 const recyclingIds=new Set(deviceCleanup.filter(task=>task.action!=='move'&&isActiveTask(task)).flatMap(task=>task.targets));
 const selectableTrash=trash.filter(item=>!recyclingIds.has(item.id)),selectedTrash=selectableTrash.filter(item=>recycleSelection.has(item.id));
 useEffect(()=>()=>clearTimeout(puffTimer.current),[]);
 useEffect(()=>{
  const terminal=deviceCleanup.filter(task=>!isActiveTask(task));
  const settled=terminal.filter(task=>!seenCleanup.current.has(task.id));
  const own=terminal.filter(task=>optimistic.some(entry=>entry.job===task.id));
  if(!settled.length&&!own.length)return;
  settled.forEach(task=>seenCleanup.current.add(task.id));
  const completed=new Set(own.flatMap(task=>task.snapshot!==snapshotId?[]:task.status==='completed'?task.targets:task.status==='partial'&&!task.error?task.targets.filter(id=>!task.failures.some(item=>item.id===id)):[]));
  if(completed.size){setData(old=>old?{...old,items:old.items.filter(item=>!completed.has(item.id))}:old);setFolders(old=>Object.fromEntries(Object.entries(old).map(([id,entry])=>[id,{...entry,items:entry.items.filter(item=>!completed.has(item.id))}])));}
  if(own.length){setOptimistic(old=>old.filter(entry=>!own.some(task=>task.id===entry.job)));}
  mapPages.current.clear();folderCache.current={};setRefresh(value=>value+1);
  if(dialog?.type==='recycle')refreshTrash().catch(e=>setError(e.message));
 },[deviceCleanup,optimistic,snapshotId,dialog?.type]);

 const aggregate=aggregateView?.node===nodeId&&aggregateView?.snapshot===snapshotId?aggregateView:null;
 useEffect(()=>{onToolChange?.(deviceId,tool)},[deviceId,tool,onToolChange]);
 const retainedView=useRef();retainedView.current={tool,status,path,data,nodeId,layout,query,kind,minimum,customSize,age,folders,expanded,history,offset,notice,transfer,treeWidth};
 useEffect(()=>()=>{storageDeviceState.save(deviceId,retainedView.current);generation.current++},[deviceId]);
 useEffect(()=>{storageDeviceState.save(deviceId,retainedView.current)},[deviceId,tool,path,layout,query,kind,minimum,customSize,age,treeWidth,nodeId,offset,expanded,history,snapshotId]);
 const call=(method,params={},timeout)=>native.request(method,{deviceId,...params},timeout??(method==='storage.query'?300000:undefined));
 useLayoutEffect(()=>{const node=treeLayout.current;if(!node)return;const update=()=>{const width=node.getBoundingClientRect().width;if(width>0)setTreeAvailable(width)};update();const observer=new ResizeObserver(update);observer.observe(node);return()=>observer.disconnect()},[tool,!!snapshotId]);
 useLayoutEffect(()=>{releaseTreePointer();return releaseTreePointer},[active,tool]);
 function resetTransition(){animation.current?.cancel();animation.current=null;transition.current=null;setMapTransition(null);}
 const focusTicket=useSyncExternalStore(storageTaskFocus.subscribe,()=>storageTaskFocus.read(deviceId));
 useEffect(()=>{if(!active||!focusTicket)return;resetTransition();setTool('analysis');setNotice('');storageTaskFocus.consume(deviceId,focusTicket)},[active,deviceId,focusTicket]);
 useEffect(()=>{
  generation.current++;navigationRevision.current++;setNavigating(false);resetTransition();if(!active)return;
  let alive=true;const rev=generation.current;
  const update=()=>{const state=statusResource.getSnapshot();if(!alive||rev!==generation.current)return;
   if(state.error){setError(state.error.message);return}const next=state.data;if(!next)return;
   setStatus(old=>{const sameRestoredSnapshot=next.snapshot?.id&&next.snapshot.id===restoredSnapshot.current;restoredSnapshot.current=null;if(next.snapshot?.id!==old?.snapshot?.id&&!sameRestoredSnapshot){mapPages.current.clear();navigationRevision.current++;setNavigating(false);resetTransition();setData(null);setNodeId('0');setOffset(0);setSelected(new Set());setFolders({});folderCache.current={};folderRequests.current.clear();setExpanded(new Set(['0']));setHistory({entries:[{id:'0',directory:true}],cursor:0})}return next});setPath(p=>p||next.snapshot?.root||next.job?.root||'');
  };const off=statusResource.subscribe(update);update();
  return()=>{alive=false;off();resetTransition()};
 },[deviceId,statusResource,active]);
 // A viewed graph gets an explicit lease. Status polling elsewhere must never pin it.
 useEffect(()=>{
  let visible=true,changed=false,alive=true;const controller=new AbortController();
  const refresh=()=>native.request('storage.lease',{deviceId,active:active&&visible&&(tool==='analysis'||tool==='large')}).catch(()=>{});
  const off=native.subscribe(value=>{changed=true;visible=value===true;refresh()},'window-telemetry');
  native.request('window.telemetry',{}, {signal:controller.signal}).then(value=>{if(alive&&!changed){visible=value===true;refresh()}}).catch(()=>{});
  refresh();const timer=setInterval(refresh,15000);
  return()=>{alive=false;controller.abort();off();clearInterval(timer);native.request('storage.lease',{deviceId,active:false}).catch(()=>{})};
 },[active,tool,deviceId]);
 useEffect(()=>{if(preference)resetTransition()},[preference]);
 useEffect(()=>{setHover(null);setMenu(null);if(!active)resetTransition();document.documentElement.dataset.storageTool=active&&tool?'open':'closed';return()=>{document.documentElement.dataset.storageTool='closed'}},[active,tool]);
 useEffect(()=>{
  if(!active||!tool||!snapshotId)return;const revision=++requestRevision.current,rev=generation.current;let cancelled=false;
  const cached=navigationResult.current;if(cached&&cached.snapshotId===snapshotId&&cached.nodeId===nodeId&&tool==='analysis'&&!query&&!kind&&!aggregate&&offset===0){navigationResult.current=null;setData(cached.result);setLoading(false);return;}
  const timer=setTimeout(async()=>{setLoading(true);try{const result=await call('storage.query',{snapshotId,nodeId,mode:tool==='large'?'large':'tree',query,kind,minBytes:tool==='large'?storageQueryMinimum(minimum):0,olderThanDays:tool==='large'?age:0,offset,excludeIds:aggregate?.ids});if(cancelled||revision!==requestRevision.current||rev!==generation.current)return;setData(result);if(tool==='analysis'&&!query&&!kind&&!aggregate&&offset===0)cacheMapPage(result);setError('');setSelected(new Set())}catch(e){if(!cancelled&&rev===generation.current){setError(e.message);resetTransition()}}finally{if(!cancelled&&revision===requestRevision.current)setLoading(false)}},query?150:0);
  return()=>{cancelled=true;clearTimeout(timer)};
 },[deviceId,active,tool,snapshotId,nodeId,query,kind,minimum,age,offset,refresh,aggregate]);
 const {jobs:fileJobs}=useFileTransfers(Boolean(transfer&&isActiveTransfer(transfer.status)));
 useEffect(()=>{if(!transfer||!isActiveTransfer(transfer.status))return;const next=fileJobs.find(job=>job.id===transfer.id);if(!next||next===transfer)return;setTransfer(next);if(!isActiveTransfer(next.status))setNotice(next.status==='completed'?'复制完成，重新扫描可查看新文件':next.error||'复制任务已结束');},[transfer,fileJobs]);
 useLayoutEffect(()=>{const node=stage.current;if(!node)return;let previous=null;const observer=new ResizeObserver(entries=>{const rect=entries[0].contentRect,next={w:rect.width,h:rect.height};if(previous&&(Math.abs(next.w-previous.w)>.5||Math.abs(next.h-previous.h)>.5)&&transition.current)resetTransition();previous=next;setSize(next);setHover(null)});observer.observe(node);return()=>observer.disconnect()},[tool,layout,!!snapshotId]);
 useLayoutEffect(()=>{
  const pending=mapTransition;if(!pending||!content.current||!stage.current)return;
  animation.current=animateDirectoryTransition({content:content.current,stage:stage.current,shade:content.current.nextElementSibling,pending,outgoing:outPortal.current,incoming:inPortal.current,onComplete:()=>{if(transition.current===pending){transition.current=null;animation.current=null;setMapTransition(null)}}});
  return()=>{animation.current?.cancel();animation.current=null;};
 },[mapTransition]);
 useDismissableLayer({open:Boolean(menu),refs:[contextRef],onDismiss:()=>setMenu(null)});
 useEffect(()=>{if(!menu)return;const dismiss=()=>setMenu(null);window.addEventListener('resize',dismiss);return()=>window.removeEventListener('resize',dismiss)},[menu]);
 async function action(work){setBusy(true);setError('');const rev=generation.current;try{await work()}catch(e){if(rev===generation.current)setError(e.message)}finally{if(rev===generation.current)setBusy(false)}}
 async function scan(chosenPath){await action(async()=>{const result=await call('storage.scan',{path:chosenPath});setPath(chosenPath);setStatus(s=>({...s,job:result}));storageJobs.refresh();setNotice('');setDialog(null)})}
 async function control(actionName){await action(async()=>{const result=await call('storage.control',{id:job.id,action:actionName});setStatus(s=>({...s,job:result}))})}
 function cacheMapPage(result){if(!result?.node)return;const key=result.snapshotId+':'+result.node.id;mapPages.current.delete(key);mapPages.current.set(key,result);while(mapPages.current.size>24)mapPages.current.delete(mapPages.current.keys().next().value);}
 async function mapPage(id){const key=snapshotId+':'+id;if(mapPages.current.has(key))return mapPages.current.get(key);const result=await call('storage.query',{snapshotId,nodeId:id,mode:'tree',offset:0});cacheMapPage(result);return result;}
 async function navigate(node,record=true,travelIndex=null,sourceKind='tile'){
  if(!node?.directory||node.id===nodeId||navigating)return;setMenu(null);setHover(null);resetTransition();requestRevision.current++;
  const rev=generation.current,nav=++navigationRevision.current,from=data,sid=snapshotId;
  setNavigating(true);setError('');
  try{
   const target=await mapPage(node.id);if(rev!==generation.current||nav!==navigationRevision.current||statusRef.current?.snapshot?.id!==sid)return;
   let pending=null;
   if(tool==='analysis'&&layout==='map'&&stage.current&&from&&!settings.reduced&&!preference){
    if(sourceKind==='tree'){pending={direction:'tree',layouts:{before:tilesFor(from),after:tilesFor(target)}};transition.current=pending;setMapTransition(pending);}else{
    const common=commonStorageDirectory(from,target);const parent=common===from.node.id?from:common===target.node.id?target:common?await mapPage(common):null;
    if(rev!==generation.current||nav!==navigationRevision.current)return;
    const source=stage.current.querySelector(`[data-tile="${CSS.escape(node.id)}"]`),stageRect=stage.current.getBoundingClientRect(),sourceRect=source?.getBoundingClientRect();
    const parentTiles=tilesFor(parent);
    pending=planStorageNavigation({from,to:target,parent,tiles:parentTiles,actualSource:sourceRect?{x:sourceRect.left-stageRect.left,y:sourceRect.top-stageRect.top,width:sourceRect.width,height:sourceRect.height}:null});
    if(pending){pending.layouts={parent:parentTiles,before:pending.before?tilesFor(from):[],after:pending.after?tilesFor(target):[]};transition.current=pending;setMapTransition(pending);}
    }
   }
   navigationResult.current={snapshotId:sid,nodeId:node.id,result:target};setData(target);setAggregateView(null);setNodeId(node.id);setOffset(0);setQuery('');setKind('');setSelected(new Set());
   if(record)setHistory(h=>({entries:[...h.entries.slice(0,h.cursor+1),node],cursor:h.cursor+1}));else if(travelIndex!==null)setHistory(h=>({...h,cursor:travelIndex}));
  }catch(e){if(rev===generation.current){setError(e.message);resetTransition();}}
  finally{if(rev===generation.current&&nav===navigationRevision.current)setNavigating(false);}
 }
 const displayData=useMemo(()=>data&&hiddenIds.size?{...data,items:data.items.filter(item=>!hiddenIds.has(item.id)),total:Math.max(0,data.total-data.items.filter(item=>hiddenIds.has(item.id)).length),bytes:Math.max(0,data.bytes-data.items.reduce((bytes,item)=>bytes+(hiddenIds.has(item.id)?item.bytes:0),0))}:data,[data,hiddenIds]);
 const currentItems=displayData?.items||[],selection=currentItems.filter(n=>selected.has(n.id));
 async function loadFolders(id,append=false){
  if(!snapshotId||folderRequests.current.has(id)||folderCache.current[id]&&!append)return;
  folderRequests.current.add(id);const rev=generation.current,sid=snapshotId;
  try{const old=folderCache.current[id],result=await call('storage.query',{snapshotId:sid,nodeId:id,offset:append?(old?.items.length||0):0});
   if(rev!==generation.current||statusRef.current?.snapshot?.id!==sid)return;
   const next={...result,items:append?[...(old?.items||[]),...result.items]:result.items};folderCache.current={...folderCache.current,[id]:next};setFolders(folderCache.current);
  }catch(e){if(rev===generation.current)setError(e.message)}finally{folderRequests.current.delete(id)}
 }
 useEffect(()=>{if(!active||tool!=='analysis'||!snapshotId)return;loadFolders('0');const ids=(data?.snapshotId===snapshotId?data.crumbs:[]).map(n=>n.id);setExpanded(old=>new Set([...old,...ids]));ids.forEach(id=>loadFolders(id));},[active,tool,snapshotId,data?.node.id,refresh]);
 function travel(delta){const i=history.cursor+delta;if(i>=0&&i<history.entries.length)navigate(history.entries[i],false,i)}
 function folderRow(n,depth=0){const branch=folders[n.id],isExpanded=n.directory&&expanded.has(n.id);return <React.Fragment key={n.id}>
  <div className={'storage-tree-row'+(nodeId===n.id?' active':'')} data-tree-node={n.id} style={{paddingLeft:Math.min(depth,6)*10}}>
   {tr(n.directory?<button className="storage-tree-toggle" aria-label={tr((isExpanded?'收起 ':'展开 ')+n.name)} aria-expanded={isExpanded} onClick={()=>{setExpanded(old=>{const next=new Set(old);next.has(n.id)?next.delete(n.id):next.add(n.id);return next});loadFolders(n.id)}}>{tr(isExpanded?<ChevronDown size={13}/>:<ChevronRight size={13}/>)}</button>:<span className="storage-tree-toggle-spacer" aria-hidden="true"/>)}
   <button className="storage-tree-folder" onClick={()=>n.directory?navigate(n,true,null,'tree'):setDialog({type:'detail',item:n})} title={n.path} aria-label={tr((n.directory?'文件夹 ':'文件 ')+n.name)}>{tr(n.directory?<FolderOpen size={17}/>:<File size={17}/>)} <span className="control-label">{n.name}</span><small className="control-label">{tr(bytesText(n.bytes))}</small></button>
  </div>
  {tr(isExpanded&&branch?.items.filter(child=>!hiddenIds.has(child.id)).map(child=>folderRow(child,depth+1)))}
  {tr(isExpanded&&branch&&branch.total>branch.items.length&&<button className="storage-tree-more" onClick={()=>loadFolders(n.id,true)}><span className="control-label">{t("显示更多…")}</span></button>)}
 </React.Fragment>}

 function tilesFor(result){
  if(!result)return [];
  let entry=tileLayouts.current.get(result);
  if(!entry||entry.w!==size.w||entry.h!==size.h){
   entry={w:size.w,h:size.h,tiles:storageTiles(result,size.w,size.h,tilePalettes.current)};
   tileLayouts.current.set(result,entry);
  }
  return entry.tiles;
 }
 const tiles=tilesFor(displayData),other=tiles.find(tile=>tile.aggregate);
 function showFileList(){resetTransition();setHover(null);setOffset(0);setData(null);setLoading(true);setAggregateView({node:nodeId,snapshot:snapshotId,ids:tiles.filter(tile=>!tile.aggregate).map(tile=>tile.id)});setLayout('list')}
 function showMap(){resetTransition();setOffset(0);if(aggregate){setData(mapPages.current.get(snapshotId+':'+nodeId)||null);setAggregateView(null)}setLayout('map')}
 function mapTiles(items,decorative=false){return items.map(n=>{
  const rect=storageTileRect(n),type=tileTypography(rect.width,rect.height,n.name),short=Math.min(rect.width,rect.height);
  if(short<1)return null;
  const glyph=n.directory?<FolderOpen size={type.icon}/>:<File size={type.icon}/>;
  const shape=short<9?' micro':(rect.width>=72&&rect.height>=24&&n.directory?' directory-mark':'');
  const Tag=decorative?'div':'button';
  const label=(n.aggregate?n.name:(n.directory?'文件夹 ':'文件 ')+n.name)+' · '+bytesText(n.bytes)+(n.aggregate?' · '+n.aggregateCount.toLocaleString(locale())+' 项':'');
  const interaction=decorative?{}:{type:'button',tabIndex:mapTransition?-1:0,'aria-label':label,onClick:e=>{
   if(mapTransition||navigating)return;
   if(n.aggregate)showFileList();else if(n.directory&&!e.ctrlKey&&!e.metaKey)navigate(n);else toggle(n,e.ctrlKey||e.metaKey);
  },onContextMenu:e=>!mapTransition&&!n.aggregate&&itemMenu(e,n),onPointerMove:e=>!mapTransition&&tooltip(e,n),onFocus:e=>!mapTransition&&tooltip(e,n),onBlur:()=>setHover(null)};
  return <Tag key={n.id} data-tile={n.id} className={'storage-tile color-'+(n.color||tileColor(n))+(n.directory?' directory':' file')+shape+(!decorative&&selected.has(n.id)?' selected':'')}
   style={{left:rect.x,top:rect.y,width:rect.width,height:rect.height,padding:short<24?0:type.pad,borderRadius:Math.min(22,short*.2)}} {...interaction}>
   {tr((type.showIcon||type.showName||type.showValue)&&<span className="storage-tile-content">{tr(type.showIcon&&glyph)}{type.showName&&<span className="storage-tile-name" style={{fontSize:type.label}}>{n.name}</span>}{tr(type.showValue&&<span className="storage-tile-value" style={{fontSize:type.value}}>{tr(bytesText(n.bytes))}</span>)}{tr(n.aggregate&&type.showValue&&rect.height-type.pad*2>=130&&<span className="storage-tile-count">{tr(n.aggregateCount.toLocaleString(locale()))}{t(" 项")}</span>)}</span>)}
   {tr(!n.aggregate&&rect.width>130&&rect.height>155&&<span className="storage-tile-kind">{tr(n.directory?'文件夹':'文件')}</span>)}
  </Tag>
 })}
 function portal(entry,ref,direction){
  if(!entry)return null;
  const type=tileTypography(entry.rect.width,entry.rect.height,entry.source.name);
  return <div ref={ref} aria-hidden="true" className={'storage-map-portal color-'+entry.color} data-direction={direction} data-map-node={entry.result.node.id}>
   <div className="storage-portal-content" style={{width:size.w,height:size.h}}>{tr(mapTiles(mapTransition.layouts[direction==='in'?'after':'before'],true))}</div>
   <div className="storage-portal-face" style={{'--portal-name-size':type.label+'px','--portal-value-size':type.value+'px'}}>{tr(type.showIcon&&<FolderOpen size={type.icon}/>)} {type.showName&&<strong>{entry.source.name}</strong>}{tr(type.showValue&&<b>{tr(bytesText(entry.source.bytes))}</b>)}</div>
  </div>
 }
 function toggle(n,multiple=true){setSelected(old=>{const next=multiple?new Set(old):new Set();if(next.has(n.id))next.delete(n.id);else next.add(n.id);return next})}
 function copy(items=selection){if(!items.length)return;setClipboard({deviceId,snapshotId,ids:items.map(n=>n.id),names:items.map(n=>n.name)});setNotice(`已复制 ${items.length} 项`);setMenu(null)}
 async function paste(){if(!clipboard||clipboard.deviceId!==deviceId||clipboard.snapshotId!==snapshotId){setClipboard(null);setNotice('扫描已更新，请重新复制');return}await action(async()=>{const plan=await call('storage.copy',{snapshotId,ids:clipboard.ids,targetId:nodeId},120000);setDialog({type:'copy',plan})})}
 async function askTrash(items=selection){
  const eligible=items.filter(item=>!item.aggregate&&!hiddenIds.has(item.id));if(!eligible.length)return;
  const ids=eligible.map(item=>item.id),key=++optimisticSerial.current,rev=generation.current;
  setMenu(null);setHover(null);setSelected(new Set());setError('');
  if(!settings.reduced&&!preference){
   const positions=tiles.filter(tile=>ids.includes(tile.id)).slice(0,10).map(tile=>{const rect=storageTileRect(tile);return {x:rect.x+rect.width/2,y:rect.y+rect.height/2,folder:tile.directory};});
   if(positions.length){clearTimeout(puffTimer.current);setPuffs(positions.map((point,i)=>({...point,key:key+'-'+i})));puffTimer.current=setTimeout(()=>setPuffs([]),540);}
  }
  setOptimistic(old=>[...old,{key,ids,snapshot:snapshotId}]);
  try{
   const task=await call('storage.trash',{snapshotId,ids});
   if(rev===generation.current)setOptimistic(old=>old.map(entry=>entry.key===key?{...entry,job:task.id}:entry));
   cleanupJobs.refresh();
  }catch(e){if(rev===generation.current){setOptimistic(old=>old.filter(entry=>entry.key!==key));setError(e.message);}}
 }
 async function refreshTrash(){const rev=generation.current,entries=await call('storage.trash.list');if(rev!==generation.current)return;setTrash(entries);setRecycleSelection(old=>new Set([...old].filter(id=>entries.some(item=>item.id===id))))}
 async function showTrash(){await action(async()=>{await refreshTrash();setRecycleSelection(new Set());setDialog({type:'recycle'})})}
 async function changeTrash(actionName,items){
  if(!items.length)return;
  const rev=generation.current;setDialog(null);setRecycleSelection(new Set());setError('');
  try{await call('storage.trash.'+actionName,{ids:items.map(item=>item.id),confirm:actionName==='delete'});cleanupJobs.refresh();}
  catch(e){if(rev===generation.current)setError(e.message);}
 }
 function toggleRecycleItem(id){setRecycleSelection(old=>{const next=new Set(old);next.has(id)?next.delete(id):next.add(id);return next;});}
 function selectAllRecycle(){setRecycleSelection(new Set(selectedTrash.length===selectableTrash.length?[]:selectableTrash.map(item=>item.id)));}
 const confirmTrash=(items,clear=false)=>setDialog({type:'delete',items,clear});
 function tooltip(e,n){const rect=e.currentTarget.getBoundingClientRect(),x=e.clientX||rect.left+rect.width/2,y=e.clientY||rect.top+rect.height/2;if(hoverId.current!==n.id||!tooltipNode.current){hoverId.current=n.id;setHover({n,x,y})}else{tooltipNode.current.style.left=Math.max(12,Math.min(x+14,innerWidth-312))+'px';tooltipNode.current.style.top=Math.max(12,Math.min(y+16,innerHeight-122))+'px'}}
 function itemMenu(e,n){e.preventDefault();setSelected(new Set([n.id]));setHover(null);setMenu({n,x:Math.min(e.clientX,innerWidth-230),y:Math.min(e.clientY,innerHeight-180)})}
 const table=()=> <div className="storage-file-table">{tr(aggregate&&<div className="storage-other-heading"><strong>{t("其他文件")}</strong><Button onClick={showMap}>{t("返回面积图")}</Button></div>)}<div className="storage-table-head"><span>{t("文件 / 文件夹")}</span><span>{t("大小")}</span><span>{t("修改时间")}</span></div>{tr(currentItems.map(n=><div className={'storage-file-row'+(selected.has(n.id)?' selected':'')} key={n.id} onContextMenu={e=>itemMenu(e,n)}><input type="checkbox" checked={selected.has(n.id)} aria-label={tr('选择 '+n.name)} onChange={()=>toggle(n)}/><button className="storage-file-name" onClick={()=>n.directory?navigate(n):setDialog({type:'detail',item:n})} title={n.path}>{tr(n.directory?<FolderOpen size={19}/>:<File size={19}/>)}<span className="control-label"><strong>{n.name}</strong>{tool==='large'&&<small>{n.path}</small>}</span></button><span>{tr(bytesText(n.bytes))}</span><time>{tr(new Date(n.modified).toLocaleDateString(locale()))}</time></div>))}{tr(!currentItems.length&&!loading&&!isActiveTask(preparation)&&<Empty icon={FolderOpen} title={t("没有符合条件的文件")} description={t("调整筛选条件或扫描其他文件夹")}/>)}<div className="storage-pagination"><span>{tr(displayData?.total||0)}{t(" 项 · ")}{tr(bytesText(displayData?.bytes||0))}</span><Button disabled={offset===0} onClick={()=>setOffset(Math.max(0,offset-200))}>{t("上一页")}</Button><span>{tr(Math.floor(offset/200)+1)}</span><Button disabled={offset+200>=(data?.total||0)} onClick={()=>setOffset(offset+200)}>{t("下一页")}</Button></div></div>;
 return <section className="storage-tools" hidden={!active}>
 <div className="page-heading storage-tool-heading"><div><div className="eyebrow">{tr((device?.isLocal??(deviceId==='local'))?'本机':'SSH')}{device?.host&&' · '+device.host}</div><h1>{t("存储")}</h1></div></div>
 <ToolNavigation actions={tool&&<div className="storage-heading-actions"><Button disabled={busy} onClick={showTrash} icon={RotateCcw}>{t("回收区")}</Button><Button disabled={busy} onClick={()=>{setError('');setDialog({type:'scan'})}} icon={ScanLine}>{t("选择扫描范围")}</Button>{tr(snapshot?.root&&<Button disabled={busy||activeJob(job)} icon={ScanLine} title={tr(snapshot.root)} onClick={()=>scan(snapshot.root)}>{t("重新扫描")}</Button>)}</div>} label={t("存储页面")} items={[["monitor","运行监测"],["analysis","空间分析"],["large","大文件清理"]]} value={tool||'monitor'} onChange={next=>{if(next==='monitor'){resetTransition();setTool(null)}else{resetTransition();setTool(next);setNotice('')}}}/>
 <div className="foundation-tool-view" data-tool-view={tool||'monitor'}>
 {tr(preparation&&['queued','running','failed'].includes(preparation.status)&&<div className="storage-inline-task" role="status"><ScanLine size={18}/><span>{tr(preparation.current)}{tr(preparation.enumerated>0?` · 已处理 ${preparation.enumerated.toLocaleString()} 项`:'')}{tr(preparation.status==='failed'?` · ${preparation.error}`:' · 可继续使用其他功能')}</span></div>)}
 {tr(!tool?<><StoragePage sample={sample||{}} samples={samples||[]}/>{tr(activeJob(job)&&<div className="storage-inline-task"><ScanLine size={18}/><span>{tr(states[job.status])}{t(" · 已发现 ")}{tr(job.enumerated.toLocaleString())}{t(" 项")}</span><Button onClick={()=>setTool('analysis')}>{t("查看")}</Button></div>)}</>:<>
 {tr(activeJob(job)||job?.status==='failed'||job?.status==='cancelled'?<div className="storage-scan-progress" role="status"><span><i className={activeJob(job)?'running':''}/>{tr(states[job.status])}{tr(activeJob(job)&&` · ${job.enumerated.toLocaleString()} 项 · ${job.seconds} 秒`)}</span><span title={tr(job.current)}>{tr(job.error||job.current||(job.status==='cancelled'?'已保留上次完整结果':''))}</span><div>{tr(['enumerating','paused'].includes(job.status)&&<Button disabled={busy} onClick={()=>control(job.status==='paused'?'resume':'pause')}>{tr(job.status==='paused'?<Play size={16}/>:<Pause size={16}/>)} {tr(job.status==='paused'?'继续':'暂停')}</Button>)}{tr(activeJob(job)&&<Button disabled={busy} onClick={()=>control('cancel')}>{t("取消扫描")}</Button>)}</div>{tr(activeJob(job)&&<div className={'storage-scan-track '+job.status} role="progressbar" aria-label={tr(states[job.status])} aria-valuetext={tr(job.status==='aggregating'?'正在整理扫描结果':`已扫描 ${job.enumerated.toLocaleString()} 项，${bytesText(job.bytes||0)}`)}><b/></div>)}</div>:null)}
 {tr(snapshot&&<>
 <section className="storage-explorer" aria-label={tr(tool==='analysis'?'磁盘空间分析工作区':'大文件清理工作区')}>
  <div className="storage-explorer-toolbar">
   {tr(tool==='analysis'?<>
    <div className="storage-nav-buttons"><button aria-label={t("后退")} disabled={history.cursor===0||loading||navigating||Boolean(mapTransition)} onClick={()=>travel(-1)}><ArrowLeft size={18}/></button><button aria-label={t("前进")} disabled={history.cursor>=history.entries.length-1||loading||navigating||Boolean(mapTransition)} onClick={()=>travel(1)}><ArrowRight size={18}/></button><button aria-label={t("返回上一级")} disabled={!data?.node.parent||loading||navigating||Boolean(mapTransition)} onClick={()=>navigate(data.crumbs.at(-2))}><ArrowUp size={18}/></button></div>
    <nav className="storage-breadcrumbs" aria-label={t("当前路径")}>{(data?.crumbs||[{id:'0',name:snapshot.root,directory:true}]).map((n,i)=><React.Fragment key={n.id}>{tr(i>0&&<ChevronRight size={14}/>)}<button title={n.path} aria-current={n.id===nodeId?'location':undefined} onClick={()=>navigate(n)}>{tr(i===0&&<HardDrive size={16}/>)}<span className="control-label">{n.name}</span></button></React.Fragment>)}</nav>
   </>:<div className="storage-large-filters">
    <StorageSizeFilter minimum={minimum} customSize={customSize} onChange={(bytes,custom)=>{if(custom)setCustomSize(custom);setOffset(0);setMinimum(bytes)}}/>
    <ThemedSelect aria-label={t("文件类型")} value={kind} onChange={e=>{setOffset(0);setKind(e.target.value)}}><option value="">{t("全部类型")}</option>{tr(Object.entries(kinds).filter(([k])=>k!=='folder').map(([k,n])=><option key={k} value={k}>{tr(n)}</option>))}</ThemedSelect>
    <ThemedSelect aria-label={t("修改时间")} value={age} onChange={e=>{setOffset(0);setAge(Number(e.target.value))}}>{tr([[0,'全部时间'],[30,'30 天前'],[90,'90 天前'],[180,'180 天前']].map(([v,t])=><option key={v} value={v}>{tr(t)}</option>))}</ThemedSelect>
   </div>)}
   <label className="storage-search"><Search size={17}/><input value={query} placeholder={t("搜索文件")} aria-label={t("搜索文件")} onChange={e=>{resetTransition();setOffset(0);setQuery(e.target.value)}}/>{tr(query&&<button aria-label={t("清除搜索")} onClick={()=>setQuery('')}><X size={14}/></button>)}</label>
  </div>
  <div className="storage-explorer-actions">
   <Toolbar label={t("文件操作")} className="storage-file-actions"><Button disabled={busy||!selection.length} onClick={()=>copy()} icon={Copy}>{t("复制")}</Button><Button disabled={busy||!clipboard||tool==='large'} onClick={paste} icon={ClipboardPaste}>{t("粘贴")}</Button><span className="storage-action-divider"/><Button disabled={busy||!selection.length} onClick={()=>askTrash()} icon={Trash2}>{t("移入回收区")}</Button></Toolbar>
   <div className="storage-view-controls"><span role="status">{tr(isActiveTask(preparation)?preparation.current:loading||navigating?'正在读取…':selection.length?`已选择 ${selection.length} 项`:'')}</span>{tr(tool==='analysis'&&<div className="storage-layout-switch"><button aria-label={t("面积图")} aria-pressed={layout==='map'} onClick={showMap}><LayoutGrid size={18}/></button><button aria-label={t("文件列表")} aria-pressed={layout==='list'} onClick={()=>{resetTransition();setLayout('list')}}><List size={18}/></button></div>)}</div>
  </div>
  {tr(tool==='analysis'?<div ref={treeLayout} className={"storage-analysis-layout"+(treeAvailable<446?" storage-tree-narrow":"")} style={{'--storage-tree-width':visibleTreeWidth+'px'}}>
   <aside id={treeId} className="storage-folder-list" aria-label={t("文件夹目录树")}><div className="storage-folder-title">{t("文件与文件夹")}</div>{tr(folders['0']?folderRow({...folders['0'].node,name:folders['0'].node.name||snapshot.root}):<span className="storage-tree-loading">{t("读取目录…")}</span>)}</aside>
   <div className="storage-tree-separator" role="separator" tabIndex={0} aria-label={t("调整文件夹目录树宽度")} aria-orientation="vertical" aria-controls={treeId} aria-valuemin={treeBounds.min} aria-valuemax={treeBounds.max} aria-valuenow={Math.round(visibleTreeWidth)} aria-valuetext={tr(Math.round(visibleTreeWidth)+' 像素')} onPointerDown={beginTreeResize} onPointerMove={moveTreeResize} onPointerUp={releaseTreePointer} onPointerCancel={releaseTreePointer} onLostPointerCapture={releaseTreePointer} onKeyDown={keyTreeResize}/>
   <div className="storage-map-column">
    {tr(layout==='map'?<><div ref={stage} className={'storage-map-stage'+(mapTransition?' animating':'')} aria-busy={Boolean(mapTransition||navigating)} onPointerLeave={()=>setHover(null)}>{tr(mapTransition?.direction==='tree'?<><div ref={content} className="storage-map-content storage-map-tree-in" aria-hidden="true">{tr(mapTiles(mapTransition.layouts.after,true))}</div><div className="storage-map-content storage-map-tree-out" aria-hidden="true">{tr(mapTiles(mapTransition.layouts.before,true))}</div></>:mapTransition?<><div ref={content} className="storage-map-content storage-map-underlay" aria-hidden="true" data-map-node={mapTransition.parent.node.id}>{tr(mapTiles(mapTransition.layouts.parent,true))}</div><div className="storage-map-shade" aria-hidden="true"/>{tr(portal(mapTransition.before,outPortal,'out'))}{tr(portal(mapTransition.after,inPortal,'in'))}</>:<div ref={content} className="storage-map-content settled" data-map-node={data?.node.id}>{tr(mapTiles(tiles))}{tr(puffs.map(puff=><div key={puff.key} className="storage-dash" aria-hidden="true" style={{left:puff.x,top:puff.y}}><div className="storage-dash-cloud"><i/><i/><i/><i/><b/><b/></div><span>{tr(puff.folder?<FolderOpen size={22}/>:<File size={22}/>)}</span></div>))}{tr(!tiles.length&&!loading&&!isActiveTask(preparation)&&<Empty icon={FolderOpen} title={t("此处没有可显示的文件")} description={t("尝试文件列表或调整搜索")}/>)}</div>)}</div><div className="storage-map-caption"><span>{tr(displayData?.total||0)}{t(" 项 · ")}{tr(bytesText(displayData?.bytes||0))}</span><span>{t("点击进入文件夹 · 右键操作")}</span></div></>:table())}
   </div>
  </div>:<div className="storage-large-results">{tr(table())}</div>)}
 </section>
 {tr(snapshot.partial&&<div className="storage-partial-note">{tr(snapshot.entryLimitReached&&<div>{t("已达到 ")}{tr(snapshot.entryLimit?`${snapshot.entryLimit.toLocaleString()} 项上限`:'扫描条目上限')}{t("，仅显示已扫描的部分。可按文件夹分别扫描。")}</div>)}{tr(snapshot.accessDenied>0&&<div>{tr(snapshot.accessDenied)}{t(" 个目录因权限不足无法读取。")}</div>)}{tr(snapshot.skipped>0&&<div>{t("另有 ")}{tr(snapshot.skipped)}{t(" 处路径、链接或系统目录未纳入统计。")}</div>)}<div>{t("大小按文件逻辑大小计算。")}</div></div>)}
 </>)}
 {tr(!snapshot&&!activeJob(job)&&<Empty icon={ChartNoAxesCombined} title={t("还没有扫描结果")} description={t("扫描在后台进行，期间可以继续使用其他页面。")}/>)}
 </>)}
 </div>

 {tr(error&&<div className="storage-message error" role="alert">{tr(error)}<button aria-label={t("关闭错误提示")} onClick={()=>setError('')}><X size={16}/></button></div>)}{tr(notice&&<div className="storage-message" role="status">{tr(notice)}<button aria-label={t("关闭提示")} onClick={()=>setNotice('')}><X size={16}/></button></div>)}
 {tr(transfer&&['queued','running'].includes(transfer.status)&&<div className="storage-inline-task"><Copy size={17}/><span>{t("正在复制 ")}{tr(transfer.filesDone)} / {tr(transfer.files)}{t(" 项 · ")}{tr(bytesText(transfer.done))} / {tr(bytesText(transfer.bytes))}</span><Button onClick={()=>action(()=>call('files.cancel',{id:transfer.id}))}>{t("取消复制")}</Button></div>)}
 {tr(hover&&!dialog&&!menu&&<div ref={tooltipNode} className="storage-tooltip" role="tooltip" style={{left:Math.max(12,Math.min(hover.x+14,innerWidth-312)),top:Math.max(12,Math.min(hover.y+16,innerHeight-122))}}><strong>{hover.n.name}</strong><span>{tr(bytesText(hover.n.bytes))} · {tr((hover.n.bytes/(data?.bytes||1)*100).toFixed(1))}%</span><small>{tr(hover.n.path||'查看文件列表以浏览其余项目')}</small></div>)}
 {tr(menu&&<div ref={contextRef} className="storage-context" role="menu" style={{left:menu.x,top:menu.y}}><button role="menuitem" onClick={()=>copy([menu.n])}><Copy size={17}/><span className="control-label">{t("复制")}</span></button><button role="menuitem" disabled={!clipboard} onClick={()=>{setMenu(null);paste()}}><ClipboardPaste size={17}/><span className="control-label">{t("粘贴到当前文件夹")}</span></button><button role="menuitem" onClick={()=>askTrash([menu.n])}><Trash2 size={17}/><span className="control-label">{t("移入回收区")}</span></button><button role="menuitem" onClick={()=>{setDialog({type:'detail',item:menu.n});setMenu(null)}}><span className="control-label">{t("查看详情")}</span></button></div>)}
 {tr(dialog?.type==='scan'&&<StorageScanDialog deviceId={deviceId} initialPath={path} busy={busy} blocked={activeJob(job)} submitError={error} onClose={()=>setDialog(null)} onScan={scan}/>)}
 {tr(dialog?.type==='copy'&&<FormDialog title={t("复制到当前文件夹？")} onClose={()=>!busy&&setDialog(null)}><p>{dialog.plan.names.join(', ')} · {tr(bytesText(dialog.plan.bytes))}</p><p>{t("同名文件将保留两份，不覆盖已有文件。")}</p><div className="dialog-actions"><Button disabled={busy} onClick={()=>{call('files.cancel',{id:dialog.plan.id});setDialog(null)}}>{t("取消")}</Button><Button tone="primary" disabled={busy} onClick={()=>action(async()=>{const result=await call('files.start',{id:dialog.plan.id,overwrite:false});setTransfer(result);setDialog(null);await refreshFileTransfers()})}>{t("开始复制")}</Button></div></FormDialog>)}
 {tr(dialog?.type==='recycle'&&<FormDialog title={t("AsterHub 回收区")} wide onClose={()=>setDialog(null)}>
  <div className="storage-recycle-toolbar"><label className="native-setting-check"><input type="checkbox" checked={selectableTrash.length>0&&selectedTrash.length===selectableTrash.length} ref={el=>{if(el)el.indeterminate=selectedTrash.length>0&&selectedTrash.length<selectableTrash.length}} disabled={!selectableTrash.length} onChange={selectAllRecycle}/>{t("全选")}{tr(selectedTrash.length>0&&<span>{t(" · 已选 ")}{tr(selectedTrash.length)}{t(" 项")}</span>)}</label><Button disabled={!selectableTrash.length||recyclingIds.size>0} onClick={()=>confirmTrash(selectableTrash,true)} icon={Trash2}>{t("清空回收区")}</Button></div>
  <div className="storage-recycle-list">{tr(trash.map(n=><label key={n.id} className="storage-recycle-item" data-pending={recyclingIds.has(n.id)}><input type="checkbox" aria-label={tr('选择 '+n.name)} disabled={recyclingIds.has(n.id)} checked={recycleSelection.has(n.id)} onChange={()=>toggleRecycleItem(n.id)}/>{tr(n.directory?<FolderOpen size={17}/>:<File size={17}/>)}<span><strong title={n.name}>{n.name}</strong><small title={n.path}>{n.path}</small></span><small>{tr(recyclingIds.has(n.id)?'处理中':bytesText(n.bytes))}</small></label>))}{tr(!trash.length&&<p>{t("回收区为空")}</p>)}</div>
  <div className="dialog-actions"><Button onClick={()=>setDialog(null)}>{t("完成")}</Button><Button disabled={!selectedTrash.length} onClick={()=>changeTrash('restore',selectedTrash)} icon={RotateCcw}>{t("恢复所选")}</Button><Button tone="danger" disabled={!selectedTrash.length} onClick={()=>confirmTrash(selectedTrash)}>{t("删除所选")}</Button></div>
 </FormDialog>)}
 {tr(dialog?.type==='delete'&&<FormDialog title={tr(dialog.clear?'清空回收区？':'永久删除所选项目？')} onClose={()=>setDialog({type:'recycle'})}><p>{t("将从当前设备永久删除 ")}{tr(dialog.items.length)}{t(" 项。此操作无法撤销。")}</p><div className="dialog-actions"><Button onClick={()=>setDialog({type:'recycle'})}>{t("取消")}</Button><Button tone="danger" onClick={()=>changeTrash('delete',dialog.items)}>{t("永久删除")}</Button></div></FormDialog>)}
 {tr(dialog?.type==='detail'&&<FormDialog title={t("文件详情")} onClose={()=>setDialog(null)}><div className="storage-dialog-files"><strong>{dialog.item.name}</strong><span>{dialog.item.path}</span><p>{tr(bytesText(dialog.item.bytes))} · {tr(kinds[dialog.item.kind])}</p><span>{tr(new Date(dialog.item.modified).toLocaleString(locale()))}</span></div><div className="dialog-actions"><Button onClick={()=>{copy([dialog.item]);setDialog(null)}}>{t("复制")}</Button><Button onClick={()=>setDialog(null)}>{t("关闭")}</Button></div></FormDialog>)}
 </section>;
}
