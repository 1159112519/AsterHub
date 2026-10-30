import React,{useEffect,useState} from 'react';
import {RefreshCw,CheckCircle2,Download,History,MessageSquare,Github,FileText} from 'lucide-react';
import {Button} from '../components';
import {BrandMark} from '../brand';
import {ReleaseLink,versionReleaseUrl,repositoryUrl} from './about-links';
import {RecentUpdates} from './about-recent-updates';
import {native} from './bridge';
import './updater.css';

export function UpdatesPanel({currentVersion,onFeedback}){
 const [release,setRelease]=useState(null),[busy,setBusy]=useState(''),[error,setError]=useState(''),[progress,setProgress]=useState(0),[ready,setReady]=useState(false),[confirming,setConfirming]=useState(false),[automatic,setAutomatic]=useState(null),[saving,setSaving]=useState(false),[recent,setRecent]=useState(false);
 useEffect(()=>{let alive=true;native.request('updates.preferences').then(value=>alive&&setAutomatic(value.checkOnStartup)).catch(e=>alive&&setError(e.message));const unsubscribe=native.subscribe(setProgress,'update-progress');return()=>{alive=false;unsubscribe()}},[]);
 async function setStartup(enabled){setSaving(true);setError('');try{const value=await native.request('updates.preferences.save',{checkOnStartup:enabled});setAutomatic(value.checkOnStartup)}catch(e){setError(e.message)}finally{setSaving(false)}}
 async function check(){setBusy('check');setError('');setRelease(null);setReady(false);setConfirming(false);try{setRelease(await native.request('updates.check',{},35000))}catch(e){setError(e.message)}finally{setBusy('')}}
 async function update(){setConfirming(false);setError('');try{if(!ready){setBusy('download');setProgress(0);await native.request('updates.download',{},600000);setReady(true)}setBusy('install');await native.request('updates.install',{},30000)}catch(e){setError(e.message)}finally{setBusy('')}}
 const version=currentVersion||release?.current;
 const status=busy==='check'?'正在检查官方更新…':release?(release.available?'发现新版本 '+release.latest:'当前已是最新版本'):'点击右侧按钮检查更新';
 const StatusIcon=busy==='check'?RefreshCw:release&&!release.available?CheckCircle2:Download;
 return <section className="native-updater" aria-label="关于 AsterHub 与版本更新">
  <div className="about-identity"><div className="about-brand-mark"><BrandMark/></div><div className="about-identity-copy"><h3>AsterHub <span>Windows 预览版</span></h3><p>连接设备，看见运行。</p><span className="about-version">{version||'版本读取中'}</span></div><Button icon={RefreshCw} className={busy==='check'?'about-check checking':'about-check'} onClick={check} disabled={!!busy}>{busy==='check'?'正在检查…':'检查更新'}</Button></div>
  <div className={'about-update-status '+(release&&!release.available?'is-current':'')}><StatusIcon size={15} aria-hidden="true"/><span role="status">{status}</span>{release?.available&&<Button tone="primary" onClick={()=>setConfirming(true)} disabled={!!busy}>{busy==='download'?'正在下载 '+Math.round(progress)+'%':busy==='install'?'正在安装…':ready?'重试安装并重启':'下载并安装更新'}</Button>}</div>
  {busy==='download'&&<progress aria-label="更新下载进度" value={progress} max="100"/>}
  {confirming&&<div className="native-updater-confirm" role="alert"><p>现在安装 {release.latest}？下载校验完成后会自动安装并重新启动，设备配置与历史记录会保留。</p><div><Button onClick={()=>setConfirming(false)}>稍后</Button><Button tone="primary" onClick={update}>确认更新并重启</Button></div></div>}
  {error&&<p className="native-error" role="alert">{error}</p>}
  <div className="about-startup-setting"><label className="native-setting-check"><input type="checkbox" checked={automatic===true} disabled={automatic===null||saving||!!busy} onChange={e=>setStartup(e.target.checked)}/>启动时自动检查更新</label><p>{saving?'正在保存…':'启动时静默检查，发现新版本后询问。'}</p></div>
  <div className="about-links" aria-label="版本信息与帮助">
   <ReleaseLink href={versionReleaseUrl(version)} label="当前版本日志"><FileText size={17}/><span>版本日志<small>当前版本发布页</small></span></ReleaseLink>
   <button type="button" className="about-link-button" onClick={()=>setRecent(true)}><History size={17}/><span>最近更新<small>最近三个版本</small></span></button>
   {onFeedback&&<button type="button" className="about-link-button" onClick={onFeedback}><MessageSquare size={17}/><span>反馈与建议<small>提交问题或想法</small></span></button>}
   <ReleaseLink href={repositoryUrl} label="GitHub 项目"><Github size={17}/><span>GitHub<small>项目与发布记录</small></span></ReleaseLink>
  </div>
  {release?.available&&release.notes&&<details className="native-updater-notes"><summary>查看 {release.latest} 更新说明</summary><p>{release.notes}</p></details>}
  <p className="about-install-help">官方 Windows 预览通道 · 下载校验后自动安装并重启</p>
  {recent&&<RecentUpdates currentVersion={version} onClose={()=>setRecent(false)}/>}
 </section>
}

