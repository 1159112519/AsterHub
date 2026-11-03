import React,{useState,useEffect,useLayoutEffect,useMemo,useRef,useSyncExternalStore,useId} from 'react';
import {Toolbar} from '../foundation/ui';
import {ToolNavigation} from '../foundation/ToolNavigation';
import {useDismissableLayer} from '../foundation/overlay';
import {animateDirectoryTransition} from '../foundation/directory-motion';
import {subscribeReducedMotion,reducedMotion} from '../motion';
import {isActiveTask,storageJobs,storageStatusResource,storageDeviceState} from './storage-jobs';
import {HardDrive,ChartNoAxesCombined,FolderOpen,File,ArrowLeft,ArrowRight,ArrowUp,ChevronDown,ChevronRight,Search,LayoutGrid,List,Copy,ClipboardPaste,Trash2,RotateCcw,RefreshCw,Pause,Play,X,ScanLine} from 'lucide-react';
import {StorageScanDialog} from './StorageScanDialog';
import {storageTreeBounds,clampStorageTreeWidth} from './storage-device-state';
import {native} from './bridge';import {Button,FormDialog,Empty} from '../components';import {ThemedSelect} from '../themed-select';import {StoragePage} from './PeripheralPages';import {storageTiles,storageTileRect,commonStorageDirectory,planStorageNavigation,bytesText,tileColor} from './storage-layout';import './storage-tools.css';
const activeJob=isActiveTask;
const states={queued:'等待扫描',enumerating:'正在扫描',aggregating:'整理结果',paused:'已暂停',ready:'扫描完成',cancelled:'扫描已取消',failed:'扫描未完成'};
const kinds={folder:'文件夹',model:'模型文件',archive:'压缩文件',media:'图像与视频',data:'数据库与日志',file:'其他文件'};
export function tileTypography(w,h,name){const short=Math.min(w,h),pad=Math.min(18,Math.max(5,short*.085)),aw=Math.max(0,w-pad*2),ah=Math.max(0,h-pad*2),label=Math.max(14,Math.min(22,Math.sqrt(w*h)*.065));return {pad,label,value:Math.max(14,Math.min(24,label+2)),icon:Math.round(Math.max(16,Math.min(28,short*.15))),showName:aw>=64&&ah>=52,showValue:aw>=60&&ah>=82,showIcon:short>=24};}
export function StorageTaskBadge({onOpen}){
 const state=useSyncExternalStore(storageJobs.subscribe,storageJobs.getSnapshot),jobs=(state.data||[]).filter(activeJob);
 return jobs.length>0&&<button className="nav-item storage-task-badge" onClick={()=>onOpen(jobs[0].deviceId)}><ScanLine size={17}/><span>扫描进行中 <small>{jobs.length} 个任务</small></span></button>;
}
export function StorageTools(props){return <StorageWorkspace key={props.deviceId} {...props}/>;}
function StorageWorkspace({deviceId,sample,samples,settings,active=true,onToolChange}){
 const [restored]=useState(()=>storageDeviceState.read(deviceId)||{});
 const [treeWidth,setTreeWidth]=useState(()=>clampStorageTreeWidth(restored.treeWidth)),[treeAvailable,setTreeAvailable]=useState(1000),treeLayout=useRef(null),treeDrag=useRef(null),treeId=useId();
 const treeBounds=storageTreeBounds(treeAvailable),visibleTreeWidth=clampStorageTreeWidth(treeWidth,treeAvailable);
 function releaseTreePointer(event){const drag=treeDrag.current;if(!drag||event?.pointerId!==undefined&&event.pointerId!==drag.id)return;treeDrag.current=null;drag.node.removeAttribute('data-resizing');if(drag.node.hasPointerCapture(drag.id))drag.node.releasePointerCapture(drag.id);}
 function beginTreeResize(event){if(event.button!==0)return;event.preventDefault();resetTransition();releaseTreePointer();const node=event.currentTarget;node.setPointerCapture(event.pointerId);node.dataset.resizing='true';treeDrag.current={id:event.pointerId,node,x:event.clientX,width:visibleTreeWidth};}
 function moveTreeResize(event){const drag=treeDrag.current;if(drag?.id===event.pointerId)setTreeWidth(clampStorageTreeWidth(drag.width+event.clientX-drag.x,treeAvailable));}
 function keyTreeResize(event){let next;const step=event.shiftKey?48:16;if(event.key==='ArrowLeft')next=visibleTreeWidth-step;else if(event.key==='ArrowRight')next=visibleTreeWidth+step;else if(event.key==='Home')next=treeBounds.min;else if(event.key==='End')next=treeBounds.max;else return;event.preventDefault();setTreeWidth(clampStorageTreeWidth(next,treeAvailable));}
 const [tool,setTool]=useState(restored.tool??null),[status,setStatus]=useState(restored.status??null),[path,setPath]=useState(restored.path??''),[data,setData]=useState(restored.data??null),[nodeId,setNodeId]=useState(restored.nodeId??'0'),[layout,setLayout]=useState(restored.layout??'map'),[query,setQuery]=useState(restored.query??''),[kind,setKind]=useState(restored.kind??''),[minimum,setMinimum]=useState(restored.minimum??104857600),[age,setAge]=useState(restored.age??0),[folders,setFolders]=useState(restored.folders??{}),[expanded,setExpanded]=useState(restored.expanded??new Set(['0'])),[history,setHistory]=useState(restored.history??{entries:[{id:'0',directory:true}],cursor:0}),[offset,setOffset]=useState(restored.offset??0),[selected,setSelected]=useState(new Set()),[clipboard,setClipboard]=useState(null),[error,setError]=useState(''),[notice,setNotice]=useState(restored.notice??''),[busy,setBusy]=useState(false),[loading,setLoading]=useState(false),[dialog,setDialog]=useState(null),[unused,setUnused]=useState(false),[trash,setTrash]=useState([]),[hover,setHover]=useState(null),[menu,setMenu]=useState(null),[refresh,setRefresh]=useState(0),[transfer,setTransfer]=useState(restored.transfer??null),[size,setSize]=useState({w:800,h:440}),[mapTransition,setMapTransition]=useState(null),[navigating,setNavigating]=useState(false);
 const mapPages=useRef(new Map()),navigationRevision=useRef(0),navigationResult=useRef(null),outPortal=useRef(),inPortal=useRef();
 const tilePalettes=useRef({}),folderRequests=useRef(new Set()),folderCache=useRef(restored.folders||{}),generation=useRef(0),requestRevision=useRef(0),stage=useRef(),content=useRef(),transition=useRef(null),animation=useRef(null),hoverId=useRef(null),tooltipNode=useRef(null),contextRef=useRef(),statusRef=useRef(),toolRef=useRef();toolRef.current=tool;statusRef.current=status;
 const preference=useSyncExternalStore(subscribeReducedMotion,reducedMotion),statusResource=useMemo(()=>storageStatusResource(deviceId),[deviceId]);
 const snapshot=status?.snapshot,snapshotId=snapshot?.id,job=status?.job;
 useEffect(()=>{onToolChange?.(deviceId,tool)},[deviceId,tool,onToolChange]);
 const retainedView=useRef();retainedView.current={tool,status,path,data,nodeId,layout,query,kind,minimum,age,folders,expanded,history,offset,notice,transfer,treeWidth};
 useEffect(()=>()=>{storageDeviceState.save(deviceId,retainedView.current);generation.current++},[deviceId]);
 const call=(method,params={},timeout)=>native.request(method,{deviceId,...params},timeout);
 useLayoutEffect(()=>{const node=treeLayout.current;if(!node)return;const update=()=>{const width=node.getBoundingClientRect().width;if(width>0)setTreeAvailable(width)};update();const observer=new ResizeObserver(update);observer.observe(node);return()=>observer.disconnect()},[tool,!!snapshotId]);
 useLayoutEffect(()=>{releaseTreePointer();return releaseTreePointer},[active,tool]);
 function resetTransition(){animation.current?.cancel();animation.current=null;transition.current=null;setMapTransition(null);}
 useEffect(()=>{
  generation.current++;navigationRevision.current++;setNavigating(false);resetTransition();if(!active)return;
  let alive=true;const rev=generation.current;
  const update=()=>{const state=statusResource.getSnapshot();if(!alive||rev!==generation.current)return;
   if(state.error){setError(state.error.message);return}const next=state.data;if(!next)return;
   setStatus(old=>{if(next.snapshot?.id!==old?.snapshot?.id){mapPages.current.clear();navigationRevision.current++;setNavigating(false);resetTransition();setData(null);setNodeId('0');setOffset(0);setSelected(new Set());setFolders({});folderCache.current={};folderRequests.current.clear();setExpanded(new Set(['0']));setHistory({entries:[{id:'0',directory:true}],cursor:0})}return next});setPath(p=>p||next.snapshot?.root||next.job?.root||'');
  };const off=statusResource.subscribe(update);update();
  return()=>{alive=false;off();resetTransition()};
 },[deviceId,statusResource,active]);
 useEffect(()=>{if(preference)resetTransition()},[preference]);
 useEffect(()=>{setHover(null);setMenu(null);if(!active)resetTransition();document.documentElement.dataset.storageTool=active&&tool?'open':'closed';return()=>{document.documentElement.dataset.storageTool='closed'}},[active,tool]);
 useEffect(()=>{
  if(!active||!tool||!snapshotId)return;const revision=++requestRevision.current,rev=generation.current;let cancelled=false;
  const cached=navigationResult.current;if(cached&&cached.snapshotId===snapshotId&&cached.nodeId===nodeId&&tool==='analysis'&&!query&&!kind&&offset===0){navigationResult.current=null;setData(cached.result);setLoading(false);return;}
  const timer=setTimeout(async()=>{setLoading(true);try{const result=await call('storage.query',{snapshotId,nodeId,mode:tool==='large'?'large':'tree',query,kind,minBytes:tool==='large'?minimum:0,olderThanDays:tool==='large'?age:0,offset});if(cancelled||revision!==requestRevision.current||rev!==generation.current)return;setData(result);if(tool==='analysis'&&!query&&!kind&&offset===0)cacheMapPage(result);setError('');setSelected(new Set())}catch(e){if(!cancelled&&rev===generation.current){setError(e.message);resetTransition()}}finally{if(!cancelled&&revision===requestRevision.current)setLoading(false)}},query?150:0);
  return()=>{cancelled=true;clearTimeout(timer)};
 },[deviceId,active,tool,snapshotId,nodeId,query,kind,minimum,age,offset,refresh]);
 useEffect(()=>{if(!transfer)return;let live=true;const timer=setInterval(async()=>{try{const jobs=await call('files.transfers');const next=jobs.find(x=>x.id===transfer.id);if(live&&next){setTransfer(next);if(!['queued','running'].includes(next.status)){setNotice(next.status==='complete'||next.status==='completed'?'复制完成，重新扫描可查看新文件':next.error||'复制任务已结束');clearInterval(timer)}}}catch{}},700);return()=>{live=false;clearInterval(timer)}},[transfer?.id,deviceId]);
 useLayoutEffect(()=>{const node=stage.current;if(!node)return;let previous=null;const observer=new ResizeObserver(entries=>{const rect=entries[0].contentRect,next={w:rect.width,h:rect.height};if(previous&&(Math.abs(next.w-previous.w)>.5||Math.abs(next.h-previous.h)>.5)&&transition.current)resetTransition();previous=next;setSize(next);setHover(null)});observer.observe(node);return()=>observer.disconnect()},[tool,layout,!!snapshotId]);
 useLayoutEffect(()=>{
  const pending=mapTransition;if(!pending||!content.current||!stage.current)return;
  animation.current=animateDirectoryTransition({content:content.current,stage:stage.current,pending,outgoing:outPortal.current,incoming:inPortal.current,onComplete:()=>{if(transition.current===pending){transition.current=null;animation.current=null;setMapTransition(null)}}});
  return()=>{animation.current?.cancel();animation.current=null;};
 },[mapTransition]);
 useDismissableLayer({open:Boolean(menu),refs:[contextRef],onDismiss:()=>setMenu(null)});
 useEffect(()=>{if(!menu)return;const dismiss=()=>setMenu(null);window.addEventListener('resize',dismiss);return()=>window.removeEventListener('resize',dismiss)},[menu]);
 async function action(work){setBusy(true);setError('');const rev=generation.current;try{await work()}catch(e){if(rev===generation.current)setError(e.message)}finally{if(rev===generation.current)setBusy(false)}}
 async function scan(chosenPath){await action(async()=>{const result=await call('storage.scan',{path:chosenPath});setPath(chosenPath);setStatus(s=>({...s,job:result}));setNotice('');setDialog(null)})}
 async function control(actionName){await action(async()=>{const result=await call('storage.control',{id:job.id,action:actionName});setStatus(s=>({...s,job:result}))})}
 function cacheMapPage(result){if(!result?.node)return;const key=result.snapshotId+':'+result.node.id;mapPages.current.delete(key);mapPages.current.set(key,result);while(mapPages.current.size>24)mapPages.current.delete(mapPages.current.keys().next().value);}
 async function mapPage(id){const key=snapshotId+':'+id;if(mapPages.current.has(key))return mapPages.current.get(key);const result=await call('storage.query',{snapshotId,nodeId:id,mode:'tree',offset:0});cacheMapPage(result);return result;}
 async function navigate(node,record=true,travelIndex=null){
  if(!node?.directory||node.id===nodeId||navigating)return;setMenu(null);setHover(null);resetTransition();requestRevision.current++;
  const rev=generation.current,nav=++navigationRevision.current,from=data,sid=snapshotId;
  setNavigating(true);setError('');
  try{
   const target=await mapPage(node.id);if(rev!==generation.current||nav!==navigationRevision.current||statusRef.current?.snapshot?.id!==sid)return;
   let pending=null;
   if(tool==='analysis'&&layout==='map'&&stage.current&&from&&!settings.reduced&&!preference){
    const common=commonStorageDirectory(from,target);const parent=common===from.node.id?from:common===target.node.id?target:common?await mapPage(common):null;
    if(rev!==generation.current||nav!==navigationRevision.current)return;
    const source=stage.current.querySelector(`[data-tile="${CSS.escape(node.id)}"]`),stageRect=stage.current.getBoundingClientRect(),sourceRect=source?.getBoundingClientRect();
    pending=planStorageNavigation({from,to:target,parent,tiles:storageTiles(parent,size.w,size.h,tilePalettes.current),actualSource:sourceRect?{x:sourceRect.left-stageRect.left,y:sourceRect.top-stageRect.top,width:sourceRect.width,height:sourceRect.height}:null});
    if(pending){pending.layouts={parent:storageTiles(parent,size.w,size.h,tilePalettes.current),before:pending.before?storageTiles(from,size.w,size.h,tilePalettes.current):[],after:pending.after?storageTiles(target,size.w,size.h,tilePalettes.current):[]};transition.current=pending;setMapTransition(pending);}
   }
   navigationResult.current={snapshotId:sid,nodeId:node.id,result:target};setData(target);setNodeId(node.id);setOffset(0);setQuery('');setKind('');setSelected(new Set());
   if(record)setHistory(h=>({entries:[...h.entries.slice(0,h.cursor+1),node],cursor:h.cursor+1}));else if(travelIndex!==null)setHistory(h=>({...h,cursor:travelIndex}));
  }catch(e){if(rev===generation.current){setError(e.message);resetTransition();}}
  finally{if(rev===generation.current&&nav===navigationRevision.current)setNavigating(false);}
 }
 const currentItems=data?.items||[],selection=currentItems.filter(n=>selected.has(n.id));
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
   {n.directory?<button className="storage-tree-toggle" aria-label={(isExpanded?'收起 ':'展开 ')+n.name} aria-expanded={isExpanded} onClick={()=>{setExpanded(old=>{const next=new Set(old);next.has(n.id)?next.delete(n.id):next.add(n.id);return next});loadFolders(n.id)}}>{isExpanded?<ChevronDown size={13}/>:<ChevronRight size={13}/>}</button>:<span className="storage-tree-toggle-spacer" aria-hidden="true"/>}
   <button className="storage-tree-folder" onClick={()=>n.directory?navigate(n):setDialog({type:'detail',item:n})} title={n.path} aria-label={(n.directory?'文件夹 ':'文件 ')+n.name}>{n.directory?<FolderOpen size={17}/>:<File size={17}/>} <span>{n.name}</span><small>{bytesText(n.bytes)}</small></button>
  </div>
  {isExpanded&&branch?.items.map(child=>folderRow(child,depth+1))}
  {isExpanded&&branch&&branch.total>branch.items.length&&<button className="storage-tree-more" onClick={()=>loadFolders(n.id,true)}>显示更多…</button>}
 </React.Fragment>}

 const tiles=useMemo(()=>storageTiles(data,size.w,size.h,tilePalettes.current),[data,size]);
 function mapTiles(items,relative=false){return items.map(n=>{const rect=storageTileRect(n),type=tileTypography(rect.width,rect.height,n.name),position=relative?{left:rect.x/size.w*100+'%',top:rect.y/size.h*100+'%',width:rect.width/size.w*100+'%',height:rect.height/size.h*100+'%'}:{left:rect.x,top:rect.y,width:rect.width,height:rect.height};return <button key={n.id} data-tile={n.id} className={'storage-tile color-'+(n.color||tileColor(n))+(n.directory?' directory':' file')+(selected.has(n.id)?' selected':'')} style={{...position,padding:type.pad,borderRadius:Math.min(22,Math.min(rect.width,rect.height)*.2)}} tabIndex={mapTransition?-1:0} onClick={e=>{if(mapTransition||navigating)return;n.aggregate?setLayout('list'):n.directory&&!e.ctrlKey&&!e.metaKey?navigate(n):toggle(n,e.ctrlKey||e.metaKey)}} onContextMenu={e=>!mapTransition&&!n.aggregate&&itemMenu(e,n)} onPointerMove={e=>!mapTransition&&tooltip(e,n)} onFocus={e=>!mapTransition&&tooltip(e,n)} onBlur={()=>setHover(null)} aria-label={(n.directory?'文件夹 ':'文件 ')+n.name+' · '+bytesText(n.bytes)}><span className="storage-tile-content">{type.showIcon&&(n.directory?<FolderOpen size={type.icon}/>:<File size={type.icon}/>)}{type.showName&&<span className="storage-tile-name" style={{fontSize:type.label}}>{n.name}</span>}{type.showValue&&<span className="storage-tile-value" style={{fontSize:type.value}}>{bytesText(n.bytes)}</span>}</span>{rect.width>130&&rect.height>155&&<span className="storage-tile-kind">{n.directory?'文件夹':'文件'}</span>}</button>})}
 function portal(entry,ref,direction){if(!entry)return null;const type=tileTypography(entry.rect.width,entry.rect.height,entry.source.name);return <div ref={ref} className={'storage-map-portal color-'+entry.color} data-direction={direction} data-map-node={entry.result.node.id}><div className="storage-portal-content">{mapTiles(mapTransition.layouts[direction==='in'?'after':'before'],true)}</div><div className="storage-portal-face" style={{'--portal-name-size':type.label+'px','--portal-value-size':type.value+'px'}}>{type.showIcon&&<FolderOpen size={type.icon}/>}<strong>{entry.source.name}</strong>{type.showValue&&<b>{bytesText(entry.source.bytes)}</b>}</div></div>}
 function toggle(n,multiple=true){setSelected(old=>{const next=multiple?new Set(old):new Set();if(next.has(n.id))next.delete(n.id);else next.add(n.id);return next})}
 function copy(items=selection){if(!items.length)return;setClipboard({deviceId,snapshotId,ids:items.map(n=>n.id),names:items.map(n=>n.name)});setNotice(`已复制 ${items.length} 项`);setMenu(null)}
 async function paste(){if(!clipboard||clipboard.deviceId!==deviceId||clipboard.snapshotId!==snapshotId){setClipboard(null);setNotice('扫描已更新，请重新复制');return}await action(async()=>{const plan=await call('storage.copy',{snapshotId,ids:clipboard.ids,targetId:nodeId},120000);setDialog({type:'copy',plan})})}
 function askTrash(items=selection){const files=items.filter(n=>!n.directory&&!n.aggregate);if(!files.length){setNotice('进入文件夹后选择要整理的文件');return}setUnused(false);setDialog({type:'trash',items:files});setMenu(null)}
 async function recycle(){const ids=dialog.items.map(n=>n.id);await action(async()=>{const result=await call('storage.trash',{snapshotId,ids,confirm:true,confirmUnused:unused},120000);setDialog(null);setSelected(new Set());mapPages.current.clear();setRefresh(v=>v+1);setNotice(result.moved.length?`已移入回收区 ${result.moved.length} 项，仍占用磁盘空间`:'文件已保留');if(result.failed.length)setError(result.failed.map(n=>n.name+'：'+n.error).join('；'))})}
 async function showTrash(){await action(async()=>{setTrash(await call('storage.trash.list'));setDialog({type:'recycle'})})}
 function tooltip(e,n){const rect=e.currentTarget.getBoundingClientRect(),x=e.clientX||rect.left+rect.width/2,y=e.clientY||rect.top+rect.height/2;if(hoverId.current!==n.id||!tooltipNode.current){hoverId.current=n.id;setHover({n,x,y})}else{tooltipNode.current.style.left=Math.max(12,Math.min(x+14,innerWidth-312))+'px';tooltipNode.current.style.top=Math.max(12,Math.min(y+16,innerHeight-122))+'px'}}
 function itemMenu(e,n){e.preventDefault();setSelected(new Set([n.id]));setHover(null);setMenu({n,x:Math.min(e.clientX,innerWidth-230),y:Math.min(e.clientY,innerHeight-180)})}
 const table=()=> <div className="storage-file-table"><div className="storage-table-head"><span>文件 / 文件夹</span><span>大小</span><span>修改时间</span></div>{currentItems.map(n=><div className={'storage-file-row'+(selected.has(n.id)?' selected':'')} key={n.id} onContextMenu={e=>itemMenu(e,n)}><input type="checkbox" checked={selected.has(n.id)} aria-label={'选择 '+n.name} onChange={()=>toggle(n)}/><button className="storage-file-name" onClick={()=>n.directory?navigate(n):setDialog({type:'detail',item:n})} title={n.path}>{n.directory?<FolderOpen size={19}/>:<File size={19}/>}<span><strong>{n.name}</strong>{tool==='large'&&<small>{n.path}</small>}</span></button><span>{bytesText(n.bytes)}</span><time>{new Date(n.modified).toLocaleDateString('zh-CN')}</time></div>)}{!currentItems.length&&<Empty icon={FolderOpen} title="没有符合条件的文件" description="调整筛选条件或扫描其他文件夹"/>}<div className="storage-pagination"><span>{data?.total||0} 项 · {bytesText(data?.bytes||0)}</span><Button disabled={offset===0} onClick={()=>setOffset(Math.max(0,offset-200))}>上一页</Button><span>{Math.floor(offset/200)+1}</span><Button disabled={offset+200>=(data?.total||0)} onClick={()=>setOffset(offset+200)}>下一页</Button></div></div>;
 return <section className="storage-tools" hidden={!active}>
 <div className="page-heading"><div><div className="eyebrow">资源监控 / 工具</div><h1>存储</h1></div>{tool&&<div className="storage-heading-actions"><Button disabled={busy} onClick={showTrash} icon={RotateCcw}>回收区</Button><Button disabled={busy} onClick={()=>{setError('');setDialog({type:'scan'})}} icon={ScanLine}>选择扫描范围</Button></div>}</div>
 <ToolNavigation label="存储页面" items={[["monitor","运行监测"],["analysis","空间分析"],["large","大文件清理"]]} value={tool||'monitor'} onChange={next=>{if(next==='monitor'){resetTransition();setTool(null)}else{resetTransition();setTool(next);setNotice('')}}}/>
 <div className="foundation-tool-view" data-tool-view={tool||'monitor'}>
 {!tool?<><StoragePage sample={sample||{}} samples={samples||[]}/>{activeJob(job)&&<div className="storage-inline-task"><ScanLine size={18}/><span>{states[job.status]} · 已发现 {job.enumerated.toLocaleString()} 项</span><Button onClick={()=>setTool('analysis')}>查看</Button></div>}</>:<>
 {activeJob(job)||job?.status==='failed'||job?.status==='cancelled'?<div className="storage-scan-progress" role="status"><span><i className={activeJob(job)?'running':''}/>{states[job.status]}{activeJob(job)&&` · ${job.enumerated.toLocaleString()} 项 · ${job.seconds} 秒`}</span><span title={job.current}>{job.error||job.current||(job.status==='cancelled'?'已保留上次完整结果':'')}</span><div>{['enumerating','paused'].includes(job.status)&&<Button disabled={busy} onClick={()=>control(job.status==='paused'?'resume':'pause')}>{job.status==='paused'?<Play size={16}/>:<Pause size={16}/>} {job.status==='paused'?'继续':'暂停'}</Button>}{activeJob(job)&&<Button disabled={busy} onClick={()=>control('cancel')}>取消扫描</Button>}</div></div>:null}
 {snapshot&&<>
 <section className="storage-explorer" aria-label={tool==='analysis'?'磁盘空间分析工作区':'大文件清理工作区'}>
  <div className="storage-explorer-toolbar">
   {tool==='analysis'?<>
    <div className="storage-nav-buttons"><button aria-label="后退" disabled={history.cursor===0||loading||navigating||Boolean(mapTransition)} onClick={()=>travel(-1)}><ArrowLeft size={18}/></button><button aria-label="前进" disabled={history.cursor>=history.entries.length-1||loading||navigating||Boolean(mapTransition)} onClick={()=>travel(1)}><ArrowRight size={18}/></button><button aria-label="返回上一级" disabled={!data?.node.parent||loading||navigating||Boolean(mapTransition)} onClick={()=>navigate(data.crumbs.at(-2))}><ArrowUp size={18}/></button></div>
    <nav className="storage-breadcrumbs" aria-label="当前路径">{(data?.crumbs||[{id:'0',name:snapshot.root,directory:true}]).map((n,i)=><React.Fragment key={n.id}>{i>0&&<ChevronRight size={14}/>}<button title={n.path} aria-current={n.id===nodeId?'location':undefined} onClick={()=>navigate(n)}>{i===0&&<HardDrive size={16}/>}<span>{n.name}</span></button></React.Fragment>)}</nav>
   </>:<div className="storage-large-filters">
    <ThemedSelect aria-label="文件大小" value={minimum} onChange={e=>{setOffset(0);setMinimum(Number(e.target.value))}}>{[[0,'全部大小'],[104857600,'大于 100 MiB'],[1073741824,'大于 1 GiB'],[10737418240,'大于 10 GiB']].map(([v,t])=><option key={v} value={v}>{t}</option>)}</ThemedSelect>
    <ThemedSelect aria-label="文件类型" value={kind} onChange={e=>{setOffset(0);setKind(e.target.value)}}><option value="">全部类型</option>{Object.entries(kinds).filter(([k])=>k!=='folder').map(([k,n])=><option key={k} value={k}>{n}</option>)}</ThemedSelect>
    <ThemedSelect aria-label="修改时间" value={age} onChange={e=>{setOffset(0);setAge(Number(e.target.value))}}>{[[0,'全部时间'],[30,'30 天前'],[90,'90 天前'],[180,'180 天前']].map(([v,t])=><option key={v} value={v}>{t}</option>)}</ThemedSelect>
   </div>}
   <label className="storage-search"><Search size={17}/><input value={query} placeholder="搜索文件" aria-label="搜索文件" onChange={e=>{resetTransition();setOffset(0);setQuery(e.target.value)}}/>{query&&<button aria-label="清除搜索" onClick={()=>setQuery('')}><X size={14}/></button>}</label>
  </div>
  <div className="storage-explorer-actions">
   <Toolbar label="文件操作" className="storage-file-actions"><Button disabled={busy||!selection.length} onClick={()=>copy()} icon={Copy}>复制</Button><Button disabled={busy||!clipboard||tool==='large'} onClick={paste} icon={ClipboardPaste}>粘贴</Button><span className="storage-action-divider"/><Button disabled={busy||!selection.some(n=>!n.directory)} onClick={()=>askTrash()} icon={Trash2}>移入回收区</Button></Toolbar>
   <div className="storage-view-controls"><span role="status">{loading||navigating?'正在读取…':selection.length?`已选择 ${selection.length} 项`:''}</span>{tool==='analysis'&&<div className="storage-layout-switch"><button aria-label="面积图" aria-pressed={layout==='map'} onClick={()=>{resetTransition();setOffset(0);setLayout('map')}}><LayoutGrid size={18}/></button><button aria-label="文件列表" aria-pressed={layout==='list'} onClick={()=>{resetTransition();setLayout('list')}}><List size={18}/></button></div>}</div>
  </div>
  {tool==='analysis'?<div ref={treeLayout} className={"storage-analysis-layout"+(treeAvailable<446?" storage-tree-narrow":"")} style={{'--storage-tree-width':visibleTreeWidth+'px'}}>
   <aside id={treeId} className="storage-folder-list" aria-label="文件夹目录树"><div className="storage-folder-title">文件与文件夹</div>{folders['0']?folderRow({...folders['0'].node,name:folders['0'].node.name||snapshot.root}):<span className="storage-tree-loading">读取目录…</span>}</aside>
   <div className="storage-tree-separator" role="separator" tabIndex={0} aria-label="调整文件夹目录树宽度" aria-orientation="vertical" aria-controls={treeId} aria-valuemin={treeBounds.min} aria-valuemax={treeBounds.max} aria-valuenow={Math.round(visibleTreeWidth)} aria-valuetext={Math.round(visibleTreeWidth)+' 像素'} onPointerDown={beginTreeResize} onPointerMove={moveTreeResize} onPointerUp={releaseTreePointer} onPointerCancel={releaseTreePointer} onLostPointerCapture={releaseTreePointer} onKeyDown={keyTreeResize}/>
   <div className="storage-map-column">
    {layout==='map'?<><div ref={stage} className={'storage-map-stage'+(mapTransition?' animating':'')} aria-busy={Boolean(mapTransition||navigating)} onPointerLeave={()=>setHover(null)}>{mapTransition?<><div ref={content} className="storage-map-content storage-map-underlay" data-map-node={mapTransition.parent.node.id}>{mapTiles(mapTransition.layouts.parent)}</div>{portal(mapTransition.before,outPortal,'out')}{portal(mapTransition.after,inPortal,'in')}</>:<div ref={content} className="storage-map-content settled" data-map-node={data?.node.id}>{mapTiles(tiles)}{!tiles.length&&!loading&&<Empty icon={FolderOpen} title="此处没有可显示的文件" description="尝试文件列表或调整搜索"/>}</div>}</div><div className="storage-map-caption"><span>{data?.total||0} 项 · {bytesText(data?.bytes||0)}</span><span>点击进入文件夹 · 右键操作</span></div></>:table()}
   </div>
  </div>:<div className="storage-large-results">{table()}</div>}
 </section>
 {snapshot.partial&&<div className="storage-partial-note">{snapshot.skipped} 处路径、链接或系统目录未纳入统计。大小按文件逻辑大小计算。</div>}
 </>}
 {!snapshot&&!activeJob(job)&&<Empty icon={ChartNoAxesCombined} title="还没有扫描结果" description="扫描在后台进行，期间可以继续使用其他页面。"/>}
 </>}
 </div>

 {error&&<div className="storage-message error" role="alert">{error}<button aria-label="关闭错误提示" onClick={()=>setError('')}><X size={16}/></button></div>}{notice&&<div className="storage-message" role="status">{notice}<button aria-label="关闭提示" onClick={()=>setNotice('')}><X size={16}/></button></div>}
 {transfer&&['queued','running'].includes(transfer.status)&&<div className="storage-inline-task"><Copy size={17}/><span>正在复制 {transfer.filesDone} / {transfer.files} 项 · {bytesText(transfer.done)} / {bytesText(transfer.bytes)}</span><Button onClick={()=>action(()=>call('files.cancel',{id:transfer.id}))}>取消复制</Button></div>}
 {hover&&!dialog&&!menu&&<div ref={tooltipNode} className="storage-tooltip" role="tooltip" style={{left:Math.max(12,Math.min(hover.x+14,innerWidth-312)),top:Math.max(12,Math.min(hover.y+16,innerHeight-122))}}><strong>{hover.n.name}</strong><span>{bytesText(hover.n.bytes)} · {(hover.n.bytes/(data?.bytes||1)*100).toFixed(1)}%</span><small>{hover.n.path||'查看文件列表以浏览其余项目'}</small></div>}
 {menu&&<div ref={contextRef} className="storage-context" role="menu" style={{left:menu.x,top:menu.y}}><button role="menuitem" onClick={()=>copy([menu.n])}><Copy size={17}/>复制</button><button role="menuitem" disabled={!clipboard} onClick={()=>{setMenu(null);paste()}}><ClipboardPaste size={17}/>粘贴到当前文件夹</button><button role="menuitem" disabled={menu.n.directory} onClick={()=>askTrash([menu.n])}><Trash2 size={17}/>移入回收区</button><button role="menuitem" onClick={()=>{setDialog({type:'detail',item:menu.n});setMenu(null)}}>查看详情</button></div>}
 {dialog?.type==='scan'&&<StorageScanDialog deviceId={deviceId} initialPath={path} busy={busy} blocked={activeJob(job)} submitError={error} onClose={()=>setDialog(null)} onScan={scan}/>}
 {dialog?.type==='trash'&&<FormDialog title="移入 AsterHub 回收区？" onClose={()=>!busy&&setDialog(null)}><p>文件会移入原文件夹下的专属回收区，可以在本页恢复。此操作仍占用磁盘空间。</p><div className="storage-dialog-files">{dialog.items.map(n=><div key={n.id}><strong>{n.name} · {bytesText(n.bytes)}</strong><span>{n.path}</span></div>)}</div>{!status?.isLocal&&<label className="native-setting-check"><input type="checkbox" checked={unused} onChange={e=>setUnused(e.target.checked)}/>我确认这些远端文件未被业务或训练使用</label>}<div className="dialog-actions"><Button disabled={busy} onClick={()=>setDialog(null)}>取消</Button><Button disabled={busy||!status?.isLocal&&!unused} onClick={recycle}>{busy?'正在移入…':'移入回收区'}</Button></div></FormDialog>}
 {dialog?.type==='copy'&&<FormDialog title="复制到当前文件夹？" onClose={()=>!busy&&setDialog(null)}><p>{dialog.plan.names.join('、')} · {bytesText(dialog.plan.bytes)}</p><p>同名文件将保留两份，不覆盖已有文件。</p><div className="dialog-actions"><Button disabled={busy} onClick={()=>{call('files.cancel',{id:dialog.plan.id});setDialog(null)}}>取消</Button><Button tone="primary" disabled={busy} onClick={()=>action(async()=>{const result=await call('files.start',{id:dialog.plan.id,overwrite:false});setTransfer(result);setDialog(null)})}>开始复制</Button></div></FormDialog>}
 {dialog?.type==='recycle'&&<FormDialog title="AsterHub 回收区" wide onClose={()=>!busy&&setDialog(null)}><p>文件仍占用磁盘空间。恢复可取回文件，永久删除后才释放空间且无法撤销。</p><div className="storage-recycle-list">{trash.map(n=><div key={n.id}><div><strong>{n.name}</strong><span>{n.path}</span><small>{bytesText(n.bytes)}</small></div><Button disabled={busy} onClick={()=>action(async()=>{await call('storage.trash.restore',{id:n.id},60000);setTrash(await call('storage.trash.list'));setNotice('文件已恢复，重新扫描可更新结果')})}>恢复</Button><Button disabled={busy||n.state!=='moved'} onClick={()=>setDialog({type:'delete',item:n})}>永久删除</Button></div>)}{!trash.length&&<p>回收区为空</p>}</div><div className="dialog-actions"><Button onClick={()=>setDialog(null)}>完成</Button></div></FormDialog>}
 {dialog?.type==='delete'&&<FormDialog title="永久删除此文件？" onClose={()=>!busy&&setDialog({type:'recycle'})}><p>此操作无法撤销，文件将从当前设备的 AsterHub 回收区永久删除。</p><div className="storage-dialog-files"><strong>{dialog.item.name} · {bytesText(dialog.item.bytes)}</strong><span>{dialog.item.path}</span></div><div className="dialog-actions"><Button disabled={busy} onClick={()=>setDialog({type:'recycle'})}>取消</Button><Button tone="danger" busy={busy} busyLabel="正在删除…" onClick={()=>action(async()=>{await call('storage.trash.delete',{id:dialog.item.id,confirm:true},60000);setTrash(await call('storage.trash.list'));setDialog({type:'recycle'});setNotice('已永久删除 '+dialog.item.name)})}>永久删除</Button></div></FormDialog>}
 {dialog?.type==='detail'&&<FormDialog title="文件详情" onClose={()=>setDialog(null)}><div className="storage-dialog-files"><strong>{dialog.item.name}</strong><span>{dialog.item.path}</span><p>{bytesText(dialog.item.bytes)} · {kinds[dialog.item.kind]}</p><span>{new Date(dialog.item.modified).toLocaleString('zh-CN')}</span></div><div className="dialog-actions"><Button onClick={()=>{copy([dialog.item]);setDialog(null)}}>复制</Button><Button onClick={()=>setDialog(null)}>关闭</Button></div></FormDialog>}
 </section>;
}
