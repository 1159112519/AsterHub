import React,{useEffect,useState} from 'react';
import {Button} from '../components';
import {native} from './bridge';
import './updater.css';

export function UpdatesPanel({currentVersion}){
 const [release,setRelease]=useState(null),[busy,setBusy]=useState(''),[error,setError]=useState(''),[progress,setProgress]=useState(0),[ready,setReady]=useState(false),[confirming,setConfirming]=useState(false),[automatic,setAutomatic]=useState(null),[saving,setSaving]=useState(false);
 useEffect(()=>{let alive=true;native.request('updates.preferences').then(value=>alive&&setAutomatic(value.checkOnStartup)).catch(e=>alive&&setError(e.message));const unsubscribe=native.subscribe(setProgress,'update-progress');return()=>{alive=false;unsubscribe()}},[]);
 async function setStartup(enabled){setSaving(true);setError('');try{const value=await native.request('updates.preferences.save',{checkOnStartup:enabled});setAutomatic(value.checkOnStartup)}catch(e){setError(e.message)}finally{setSaving(false)}}
 async function check(){setBusy('check');setError('');setRelease(null);setReady(false);setConfirming(false);try{setRelease(await native.request('updates.check',{},35000))}catch(e){setError(e.message)}finally{setBusy('')}}
 async function update(){setConfirming(false);setError('');try{if(!ready){setBusy('download');setProgress(0);await native.request('updates.download',{},600000);setReady(true)}setBusy('install');await native.request('updates.install',{},30000)}catch(e){setError(e.message)}finally{setBusy('')}}
 const status=busy==='check'?'正在检查官方更新…':release?(release.available?'发现新版本 '+release.latest:'当前已是最新版本'):'尚未检查更新';
 return <section className="native-updater" aria-label="版本更新">
  <div className="native-updater-top">
   <div className="native-updater-version"><div><h3>AsterHub</h3><p>当前版本 {currentVersion||release?.current||'—'} · Windows</p><strong role="status">{status}</strong></div><div className="native-updater-actions"><Button onClick={check} disabled={!!busy}>{busy==='check'?'正在检查…':'检查更新'}</Button>{release?.available&&<Button tone="primary" onClick={()=>setConfirming(true)} disabled={!!busy}>{busy==='download'?'正在下载 '+Math.round(progress)+'%':busy==='install'?'正在安装…':ready?'重试安装并重启':'下载并安装更新'}</Button>}</div></div>
   {busy==='download'&&<progress aria-label="更新下载进度" value={progress} max="100"/>}
   {confirming&&<div className="native-updater-confirm" role="alert"><p>现在安装 {release.latest}？下载校验完成后会自动安装并重新启动，设备配置与历史记录会保留。</p><div><Button onClick={()=>setConfirming(false)}>稍后</Button><Button tone="primary" onClick={update}>确认更新并重启</Button></div></div>}
   {error&&<p className="native-error" role="alert">{error}</p>}
  </div>
  <label className="native-setting-check"><input type="checkbox" checked={automatic===true} disabled={automatic===null||saving||!!busy} onChange={e=>setStartup(e.target.checked)}/>启动时自动检查更新</label>
  <p className="native-setting-help">{saving?'正在保存…':'设置即时保存。启动时静默检查一次，仅发现经过验证的新版本时询问。'}</p>
  {release&&<div className="native-updater-notes"><h4>{release.available?'新版本说明':'版本说明'}</h4><p>{release.notes||'暂无版本说明。'}</p></div>}
  <p className="native-setting-help">使用官方 Windows 预览版更新通道。下载校验完成后自动安装与重启。</p>
 </section>
}
