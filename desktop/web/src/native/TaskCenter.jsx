import {t,tr,useLocale,locale} from '../i18n/index.js';
import React,{useId,useEffect,useLayoutEffect,useMemo,useRef,useState,useSyncExternalStore} from 'react';
import {createPortal} from 'react-dom';
import {ArrowUpRight,Check,Download,ListChecks,ScanLine,ArrowRightLeft,X,Trash2} from 'lucide-react';
import {Button,IconButton} from '../components';
import {TaskNavigationItem} from '../foundation/TaskNavigationItem';
import {useDismissableLayer} from '../foundation/overlay';
import {native} from './bridge';
import {useFileTransfers,refreshFileTransfers} from './file-transfers';
import {storageJobs,cleanupJobs} from './storage-jobs';
import {readTaskInbox,writeTaskInbox,recordTaskInbox,dismissTask,clearFinishedTasks,canClearFinishedTask,taskInboxGroups,isTaskRunning,taskIdentity} from './task-inbox';
import {updateStore,useUpdateTask} from './update-task';
import {buildTaskGroups,countPendingTasks,formatTransferSize,transferLabels,isActiveScan} from './task-center-model';
import './task-center.css';

const sections={update:['应用更新',Download],transfer:['文件传输',ArrowRightLeft],scan:['扫描与索引',ScanLine],cleanup:['存储清理',Trash2]};
const updateLabels={downloading:'下载中',cancelling:'正在取消',ready:'可安装',installing:'正在安装',cancelled:'已取消',error:'下载中断'};
const scanLabels={queued:'等待扫描',running:'正在扫描',enumerating:'正在扫描',aggregating:'整理结果',paused:'已暂停',ready:'已完成',cancelled:'已取消',failed:'扫描未完成'};
const cleanupAction={move:'移入回收区',delete:'永久删除',restore:'恢复项目'};
const cleanupStatus={queued:'等待处理',running:'处理中',completed:'已完成',partial:'部分完成',failed:'未完成'};
const integer=new Intl.NumberFormat();

export function TaskCenter({devices,onOpenStorage,onOpenUpdates}){useLocale();
 const timeFormat=useMemo(()=>new Intl.DateTimeFormat(locale(),{month:'numeric',day:'numeric',hour:'2-digit',minute:'2-digit',hour12:false}),[locale()]);
 const triggerId='tasks-'+useId(),anchor=useRef(null),panel=useRef(null),closeButton=useRef(null),inFlight=useRef(new Set());
 const [open,setOpen]=useState(false),[pending,setPending]=useState({}),[errors,setErrors]=useState({});
 const update=useUpdateTask(),{jobs:transfers,error:transferError}=useFileTransfers();
 const scans=useSyncExternalStore(storageJobs.subscribe,storageJobs.getSnapshot);
 const cleanup=useSyncExternalStore(cleanupJobs.subscribe,cleanupJobs.getSnapshot);
 const [inbox,setInbox]=useState(()=>readTaskInbox(globalThis.localStorage));
 const live=useMemo(()=>buildTaskGroups(update,transfers,scans.data||[],cleanup.data||[]),[update,transfers,scans.data,cleanup.data]);
 useEffect(()=>setInbox(old=>recordTaskInbox(old,live)),[live]);
 useEffect(()=>writeTaskInbox(globalThis.localStorage,inbox),[inbox]);
 const groups=useMemo(()=>taskInboxGroups(live,inbox),[live,inbox]);
 const canClear=groups.some(({kind,jobs})=>jobs.some(job=>canClearFinishedTask(kind,job)));
 const count=countPendingTasks(groups),labels=new Map(devices.map(({device})=>[device.id,device.name]));
 const deviceName=id=>labels.get(id)||(id==='local'?'本机':id?'原设备':'');
 const close=(restore=false)=>{setOpen(false);if(restore)document.getElementById(triggerId)?.focus({preventScroll:true})};
 useDismissableLayer({open,refs:[anchor,panel],onDismiss:reason=>close(reason==='escape')});
 useLayoutEffect(()=>{
  if(!open)return;
  const position=()=>{
   const source=anchor.current?.getBoundingClientRect(),layer=panel.current;
   if(!source||!layer)return;
   const width=Math.min(390,window.innerWidth-24);
   layer.style.left=Math.max(12,Math.min(source.right+14,window.innerWidth-width-12))+'px';
   layer.style.bottom=Math.max(12,window.innerHeight-source.bottom)+'px';
  };
  position();closeButton.current?.querySelector('button')?.focus({preventScroll:true});
  const sidebar=anchor.current?.closest('.sidebar'),observer=new ResizeObserver(position);
  if(sidebar){observer.observe(sidebar);sidebar.addEventListener('scroll',position,{passive:true})}
  window.addEventListener('resize',position);
  return()=>{observer.disconnect();sidebar?.removeEventListener('scroll',position);window.removeEventListener('resize',position)};
 },[open]);
 const toggle=()=>{if(open){close(true);return;}setOpen(true);refreshFileTransfers();storageJobs.refresh();cleanupJobs.refresh()};
 async function action(key,work){
  if(inFlight.current.has(key))return;
  inFlight.current.add(key);setPending(p=>({...p,[key]:true}));setErrors(e=>({...e,[key]:''}));
  try{await work()}catch(error){setErrors(e=>({...e,[key]:error.message}))}
  finally{inFlight.current.delete(key);setPending(p=>({...p,[key]:false}))}
 }
 function row(kind,job){
  const key=taskIdentity(kind,job),blocked=Boolean(pending[key]),{status}=job;
  const running=isTaskRunning(kind,job),stamp=job.time||job.created;
  let name,meta,progress,indeterminate=false,state='normal',statusText,actions=[];
  if(kind==='update'){
   name='AsterHub '+job.latest;statusText=updateLabels[status];
   if(status==='downloading'||status==='cancelling'){progress=Math.round(job.progress||0);meta=progress+'%'}
   if(status==='ready')state='success';if(status==='error')state='error';
   if(status==='downloading'&&job.canCancel!==false)actions.push(['取消',()=>action(key,()=>updateStore.request('cancel'))]);
   if(status==='ready')actions.push(['查看更新',()=>{close();onOpenUpdates()},ArrowUpRight]);
   if(!job.archived&&(status==='cancelled'||status==='error'))actions.push(['继续下载',()=>action(key,()=>updateStore.request('download'))]);
  }else if(kind==='transfer'){
   const names=job.names||[];name=names.slice(0,2).join('、')||'文件复制';if(names.length>2)name+=` 等 ${names.length} 项`;
   statusText=transferLabels[status]||'传输已结束';
   const route=[deviceName(job.sourceDeviceId),deviceName(job.targetDeviceId)].filter(Boolean).join(' → ');
   const sizes=Number.isFinite(job.bytes)?`${formatTransferSize(job.done)} / ${formatTransferSize(job.bytes)}`:'';
   meta=[route,sizes].filter(Boolean).join(' · ');
   if(status==='queued'||status==='running'){
    if(job.bytes>0)progress=100*job.done/job.bytes;else if(job.files>0)progress=100*job.filesDone/job.files;else indeterminate=true;
    actions.push(['取消',()=>action(key,async()=>{await native.request('files.cancel',{id:job.id});await refreshFileTransfers()})]);
   }
   if(status==='undoing')indeterminate=true;
   if(status==='completed')state='success';if(status==='failed'||status==='undo-failed')state='error';
  }else if(kind==='cleanup'){
   name=cleanupAction[job.action]||'存储清理';statusText=cleanupStatus[status]||'已结束';
   meta=[deviceName(job.deviceId),job.names?.join('、'),running?`${job.done} / ${job.total} 项`:`成功 ${job.succeeded} / ${job.total} 项`].filter(Boolean).join(' · ');
   if(running){if(job.done>0)progress=100*job.done/job.total;else indeterminate=true;}
   if(status==='completed')state='success';if(status==='partial')state='warning';if(status==='failed')state='error';
  }else{
   name=job.kind==='index'?'存储查询索引':job.root||'磁盘扫描';statusText=job.kind==='index'?(job.current||'正在构建索引'):(scanLabels[status]||'扫描已结束');
   meta=[deviceName(job.deviceId),job.kind==='index'?job.root:null,Number.isFinite(job.enumerated)?`${integer.format(job.enumerated)} 项`:''].filter(Boolean).join(' · ');
   indeterminate=isActiveScan(status)&&status!=='paused';
   if(status==='ready'){state=job.partial?'warning':'success';if(job.partial)statusText='部分完成'}
   if(status==='failed')state='error';
   if(labels.has(job.deviceId))actions.push([status==='ready'?'查看结果':'查看',()=>{close();onOpenStorage(job.deviceId)},ArrowUpRight]);
  }
  const error=errors[key]||job.error;
  const failures=kind==='cleanup'?job.failures||[]:[];
  const dismiss=()=>setInbox(old=>dismissTask(recordTaskInbox(old,[{kind,jobs:[job]}]),kind,job));
  return <article key={key} className="task-center-row" data-state={state}>
   <div className="task-center-task-title"><strong title={tr(name)}>{tr(name)}</strong><div className="task-center-status-actions"><span className="task-center-state">{tr(state==='success'&&<Check size={13} aria-hidden="true"/>)}{tr(statusText)}</span>{tr(!running&&<IconButton icon={X} label={tr('关闭任务通知：'+name)} onClick={dismiss}/>)}</div></div>
   {tr((meta||actions.length>0)&&<div className="task-center-task-foot"><span title={tr(meta)}>{tr(meta)}</span><div>{tr(actions.map(([label,run,icon])=><Button key={label} disabled={blocked} onClick={run} icon={icon}>{tr(label)}</Button>))}</div></div>)}
   {tr((Number.isFinite(progress)||indeterminate)&&<div className="task-center-progress" role="progressbar" aria-label={tr(name+'进度')} aria-valuemin={0} aria-valuemax={100} aria-valuenow={indeterminate?undefined:Math.round(Math.max(0,Math.min(100,progress)))} data-indeterminate={indeterminate}><i style={indeterminate?undefined:{width:Math.max(0,Math.min(100,progress))+'%'}}/></div>)}
   {tr(stamp&&Number.isFinite(Date.parse(stamp))&&<time className="task-center-time" dateTime={stamp} title={tr(new Date(stamp).toLocaleString(locale()))}>{tr(timeFormat.format(new Date(stamp)))}</time>)}
   {tr(error&&<p className="task-center-error" role="status">{tr(error)}</p>)}
   {tr(!!failures.length&&<details className="task-center-failures"><summary>{tr(failures.length)}{t(" 项未处理")}</summary><ul>{failures.map((item,i)=><li key={item.id||i}>{item.name}：{tr(item.error)}</li>)}</ul></details>)}
  </article>;
 }
 return <div ref={anchor} className="task-center-anchor">
  <TaskNavigationItem id={triggerId} className={'task-center-trigger'+(count?' has-tasks':'')} icon={ListChecks} label={t("任务")} detail={count||undefined} title={tr(count?`任务，${count} 项待处理`:'任务')} expanded={open} onClick={toggle}/>
  {tr(open&&createPortal(<section className="task-center-panel" ref={panel} role="dialog" aria-modal="false" aria-labelledby={triggerId+'-title'} id={triggerId+'-panel'}>
   <header><h2 id={triggerId+'-title'}>{t("任务")}</h2><div className="task-center-header-actions"><Button disabled={!canClear} onClick={()=>{closeButton.current?.querySelector('button')?.focus({preventScroll:true});setInbox(old=>clearFinishedTasks(old,live))}}>{t("清除已结束")}</Button><div ref={closeButton}><IconButton icon={X} label={t("收起任务")} onClick={()=>close(true)}/></div></div></header>
   <div className="task-center-content">
    {tr(groups.map(({kind,jobs})=>{const [title,Icon]=sections[kind];return <section key={kind} aria-label={tr(title)} className="task-center-group" data-kind={kind}><h3><Icon size={16} aria-hidden="true"/>{tr(title)}</h3>{tr(jobs.map(job=>row(kind,job)))}</section>}))}
    {tr((transferError||scans.error||cleanup.error)?<p className="task-center-error" role="status">{t("部分任务状态暂时无法读取")}</p>:!groups.length&&<p className="task-center-empty">{tr(!scans.data&&scans.loading?'正在读取任务…':'暂无任务')}</p>)}
   </div>
  </section>,document.body))}
 </div>;
}
