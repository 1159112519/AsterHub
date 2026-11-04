import {TaskNavigationItem} from '../foundation/TaskNavigationItem';
import React,{useEffect,useState} from 'react';
import {RefreshCw,CheckCircle2,Download,History,Github,FileText} from 'lucide-react';
import {Button} from '../components';
import {BrandMark} from '../brand';
import {ReleaseLink,versionReleaseUrl,repositoryUrl} from './about-links';
import {RecentUpdates} from './about-recent-updates';
import {native} from './bridge';
import {updateStore,useUpdateTask} from './update-task';
import './updater.css';

export function UpdatesPanel({currentVersion}){
 const task=useUpdateTask(),release=task.latest?task:null,ready=task.ready;
 const busy={checking:'check',downloading:'download',cancelling:'cancel',installing:'install'}[task.state]||'';
 const progress=task.progress||0;
 const [error,setError]=useState(''),[confirming,setConfirming]=useState(false),[automatic,setAutomatic]=useState(null),[saving,setSaving]=useState(false),[recent,setRecent]=useState(false);
 useEffect(()=>{let alive=true;native.request('updates.preferences').then(value=>alive&&setAutomatic(value.checkOnStartup)).catch(e=>alive&&setError(e.message));return()=>{alive=false}},[]);
 async function setStartup(enabled){setSaving(true);setError('');try{const value=await native.request('updates.preferences.save',{checkOnStartup:enabled});setAutomatic(value.checkOnStartup)}catch(e){setError(e.message)}finally{setSaving(false)}}
 async function run(action){setError('');setConfirming(false);try{await updateStore.request(action)}catch(e){setError(e.message)}}
 const check=()=>run('check');
 const version=currentVersion||release?.current;
 const status=busy==='check'?'正在检查官方更新…':task.state==='cancelled'?'下载已取消，可继续下载':ready?'更新已下载并校验，等待安装':release?(release.available?'发现新版本 '+release.latest:'当前已是最新版本'):'';
 const StatusIcon=busy==='check'?RefreshCw:release&&!release.available?CheckCircle2:Download;
 return <section className="native-updater" aria-label="关于 AsterHub 与版本更新">
  <div className="about-identity"><div className="about-brand-mark"><BrandMark/></div><div className="about-identity-copy"><h3>AsterHub <span>Windows 预览版</span></h3><p>连接设备，看见运行。</p><span className="about-version">{version||'版本读取中'}</span></div><Button icon={RefreshCw} className={busy==='check'?'about-check checking':'about-check'} onClick={check} disabled={!!busy}>{busy==='check'?'正在检查…':'检查更新'}</Button></div>
  {status&&<div className={'about-update-status '+(release&&!release.available?'is-current':'')}><StatusIcon size={15} aria-hidden="true"/><span role="status">{status}</span>{release?.available&&<Button tone="primary" onClick={()=>ready?setConfirming(true):run('download')} disabled={!!busy}>{busy==='download'?'正在下载 '+Math.round(progress)+'%':busy==='cancel'?'正在取消…':busy==='install'?'正在安装…':ready?'安装并重启':task.state==='cancelled'||task.state==='error'?'继续下载':'下载更新'}</Button>}{busy==='download'&&<Button onClick={()=>run('cancel')}>取消下载</Button>}</div>}
  {(busy==='download'||busy==='cancel')&&<><progress aria-label="更新下载进度" value={progress} max="100"/><p className="native-setting-help">可关闭此面板继续使用。关闭主窗口到托盘后仍会继续下载，完成后由你确认安装。</p></>}
  {task.state==='cancelled'&&<p className="native-setting-help">已完成的下载部分会保留，继续下载时会重新校验。</p>}
  {confirming&&ready&&<div className="native-updater-confirm" role="alert"><p>现在安装 {release.latest} 并重新启动？设备配置与历史记录会保留。</p><div><Button onClick={()=>setConfirming(false)}>稍后</Button><Button tone="primary" onClick={()=>run('install')}>确认安装并重启</Button></div></div>}
  {(error||task.error)&&<p className="native-error" role="alert">{error||task.error}</p>}
  <div className="about-startup-setting"><label className="native-setting-check"><input type="checkbox" checked={automatic===true} disabled={automatic===null||saving||!!busy} onChange={e=>setStartup(e.target.checked)}/>启动时自动检查更新</label><p>{saving?'正在保存…':'启动时静默检查，发现新版本后询问。'}</p></div>
  <div className="about-links" aria-label="版本信息与帮助">
   <ReleaseLink href={versionReleaseUrl(version)} label="当前版本日志"><FileText size={17}/><span>版本日志<small>当前版本发布页</small></span></ReleaseLink>
   <button type="button" className="about-link-button" onClick={()=>setRecent(true)}><History size={17}/><span>最近更新<small>最近三个版本</small></span></button>
   <ReleaseLink href={repositoryUrl} label="GitHub 项目"><Github size={17}/><span>GitHub<small>项目与发布记录</small></span></ReleaseLink>
  </div>
  {release?.available&&release.notes&&<details className="native-updater-notes"><summary>查看 {release.latest} 更新说明</summary><p>{release.notes}</p></details>}
  <p className="about-install-help">官方 Windows 预览通道 · 后台下载，校验后由你确认安装</p>
  {recent&&<RecentUpdates currentVersion={version} onClose={()=>setRecent(false)}/>}
 </section>
}


// Optional application-shell entry: opens the existing About tab without owning the task.
export function UpdateTaskBadge({onOpen}){
 const task=useUpdateTask();
 const title=task.state==='downloading'?'更新下载中 '+Math.round(task.progress||0)+'%':task.state==='cancelling'?'正在取消更新下载':task.ready?'更新已下载，等待安装':task.state==='error'&&task.latest?'更新下载中断':null;
 if(!title)return null;
 return <TaskNavigationItem className="native-update-task-badge" icon={Download} label={task.state==='downloading'?'更新下载':task.ready?'更新就绪':'更新任务'} detail={task.state==='downloading'?Math.round(task.progress||0)+'%':undefined} title={title+'，查看更新'} onClick={onOpen}/>;
}
