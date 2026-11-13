import {t,tr,useLocale,locale} from '../i18n/index.js';
import {ThemedSelect} from "../themed-select";
import React,{useEffect,useLayoutEffect,useRef,useState} from 'react';
import {Monitor,Server,Plus,Pencil,Trash2,KeyRound,FolderOpen,Check,Loader2,ChevronsUpDown,Search} from 'lucide-react';
import {Button,IconButton,Modal} from '../components';
import {native} from './bridge';
import {useDismissableLayer} from '../foundation/overlay';
import {mergeReading,emptySample} from './device-state';
import {mergeLightReading,overlayLightReading} from './light-telemetry';
const stateLabel={connecting:'正在连接',connected:'持续记录',retrying:'等待重连'};
export function useDeviceSession(onError){
 const [devices,setDevices]=useState([]),[selected,setSelected]=useState('local'),[readings,setReadings]=useState({});
 const alive=useRef(true),selection=useRef(0),known=useRef(new Set(['local'])),cache=useRef({}),deviceRows=useRef([]),active=useRef(true),selectedRef=useRef('local'),frame=useRef(0);
 const [,setAgeClock]=useState(0);
 const flush=()=>{frame.current=0;if(active.current&&alive.current){setReadings(cache.current);setDevices(deviceRows.current)}};
 const paint=()=>{if(active.current&&!frame.current)frame.current=requestAnimationFrame(flush)};
 const accept=(id,raw,merge)=>{if(!alive.current||!known.current.has(id))return;const previous=cache.current[id],next=merge(previous,raw);if(next===previous)return;cache.current={...cache.current,[id]:next};if(selectedRef.current===id)paint()};
 const receive=raw=>{if(raw)accept(raw.deviceId,raw,mergeReading)};
 const receiveLight=raw=>{if(raw?.deviceId==='local')accept('local',raw,mergeLightReading)};
 const update=list=>{if(!alive.current)return;known.current=new Set(list.items.map(x=>x.device.id));deviceRows.current=list.items;for(const id of Object.keys(cache.current))if(!known.current.has(id)){cache.current={...cache.current};delete cache.current[id]}paint()};
 const changeSelected=id=>{selectedRef.current=id;setSelected(id);paint()};
 useEffect(()=>{
  alive.current=true;
  const visibility=visible=>{const next=visible===true;if(active.current===next)return;active.current=next;if(next)flush();else if(frame.current){cancelAnimationFrame(frame.current);frame.current=0}};
  const controller=new AbortController();let visibilityChanged=false;
  const off=native.subscribe(receive),offDevices=native.subscribe(update,'devices'),offLight=native.subscribe(receiveLight,'capture-light'),offVisibility=native.subscribe(value=>{visibilityChanged=true;visibility(value)},'window-telemetry');
  native.request('window.telemetry',{}, {signal:controller.signal}).then(value=>{if(!controller.signal.aborted&&!visibilityChanged)visibility(value)}).catch(()=>{});
  const timer=setInterval(()=>{const light=cache.current.local?.light;if(active.current&&selectedRef.current==='local'&&light?.good&&(light.error||Date.now()-light.good.time>Math.max(3000,light.good.intervalMs*3)))setAgeClock(v=>v+1)},1000);
  native.request('devices.list').then(async list=>{if(!alive.current)return;update(list);changeSelected(list.selected);native.request('snapshot.light',{deviceId:'local'}).then(receiveLight).catch(()=>{});await Promise.all(list.items.map(async row=>receive(await native.request('snapshot.latest',{deviceId:row.device.id}))))}).catch(e=>alive.current&&onError(e.message));
  return()=>{alive.current=false;controller.abort();off();offDevices();offLight();offVisibility();clearInterval(timer);if(frame.current){cancelAnimationFrame(frame.current);frame.current=0}};
 },[]);
 async function select(id){const seq=++selection.current;changeSelected(id);try{await native.request('devices.select',{id});if(seq!==selection.current)return;receive(await native.request('snapshot.latest',{deviceId:id}))}catch(e){if(seq===selection.current)onError(e.message)}}
 async function refresh(){const list=await native.request('devices.list');update(list);if(!list.items.some(x=>x.device.id===selected))changeSelected(list.selected);return list}
 const shown=overlayLightReading(readings[selected]?.sample||emptySample(selected),readings[selected]?.light),selectedRow=devices.find(x=>x.device.id===selected);
 return {devices,selected,select,refresh,sample:shown,lightSamples:readings[selected]?.lightSamples||[],hasSample:!!readings[selected]?.sample,stale:!!readings[selected]?.stale,connectionError:readings[selected]?.error||'',lastSuccess:readings[selected]?.lastSuccess??null,samples:readings[selected]?.samples||[],row:selectedRow&&shown.light?{...selectedRow,light:shown.light}:selectedRow};
}
export function DeviceSwitcher({session,collapsed,onAdd,onEdit}){useLocale();
 const [open,setOpen]=useState(false),anchor=useRef(null),panel=useRef(null),search=useRef(null);
 const current=session.row?.device??session.devices.find(r=>r.device.id===session.selected)?.device;
 const Icon=current?.isLocal===false?Server:Monitor;
 const close=(restore=false)=>{setOpen(false);if(restore)anchor.current?.focus({preventScroll:true})};
 useDismissableLayer({open,refs:[anchor,panel],onDismiss:reason=>close(reason==='escape')});
 useEffect(()=>setOpen(false),[collapsed]);
 useLayoutEffect(()=>{if(!open)return;
  const position=()=>{const a=anchor.current?.getBoundingClientRect(),p=panel.current;if(!a||!p)return;
   p.style.setProperty('--device-pop-left',`${Math.max(12,Math.min(a.right+12,window.innerWidth-328))}px`);
   p.style.setProperty('--device-pop-top',`${Math.max(12,Math.min(a.top,window.innerHeight-246))}px`);
  };position();search.current?.focus({preventScroll:true});const side=anchor.current?.closest('.sidebar');side?.addEventListener('scroll',position,{passive:true});window.addEventListener('resize',position);
  return()=>{side?.removeEventListener('scroll',position);window.removeEventListener('resize',position)};
 },[open]);
 return <div className={collapsed?'collapsed-device':'device-switcher'}>
  {tr(!collapsed&&<div className="native-device-label"><span>{t("我的设备")}</span><IconButton icon={Plus} label={t("添加设备")} onClick={()=>{close();onAdd()}}/></div>)}
  <button className={collapsed?'':'device-switch-trigger'} ref={anchor} aria-label={tr('切换设备，当前 '+(current?.name||'本机'))} aria-expanded={open} aria-haspopup="dialog" title={current?.host||current?.name} onClick={()=>setOpen(v=>!v)}><Icon size={19}/><span><strong>{current?.isLocal?t('本机'):current?.name||t('本机')}</strong>{!collapsed&&current?.host&&<small>{current.host}</small>}</span><ChevronsUpDown size={14}/></button>
  {tr(open&&<div ref={panel} role="dialog" aria-label={t("切换设备")} className="sidebar-device-popover"><DeviceSidebar search={search} session={{...session,select:id=>{session.select(id);close(true)}}} onAdd={()=>{close();onAdd()}} onEdit={d=>{close();onEdit(d)}}/></div>)}
 </div>;
}
function DeviceSidebar({session,onAdd,onEdit,search}){useLocale();
 const key='asterhub-device-order-v1',drag=useRef(null),suppress=useRef(false),[order,setOrder]=useState(()=>{try{return JSON.parse(localStorage.getItem(key))||[]}catch{return []}}),[over,setOver]=useState(null),[query,setQuery]=useState('');
 const rows=[...session.devices].sort((a,b)=>{const x=order.indexOf(a.device.id),y=order.indexOf(b.device.id);return (x<0?order.length+session.devices.indexOf(a):x)-(y<0?order.length+session.devices.indexOf(b):y)});
 function reorder(source,target){if(!source||source===target)return;const ids=rows.map(r=>r.device.id),from=ids.indexOf(source),to=ids.indexOf(target);if(from<0||to<0)return;ids.splice(from,1);ids.splice(to,0,source);setOrder(ids);try{localStorage.setItem(key,JSON.stringify(ids))}catch{}}
 const term=query.trim().toLocaleLowerCase(),visible=term?rows.filter(({device:d})=>`${d.id==='local'?t('本机'):d.name} ${d.host}`.toLocaleLowerCase().includes(term)):rows;
 return <><div className="device-picker-controls"><label><Search size={15}/><input ref={search} aria-label={t("搜索设备")} placeholder={t("搜索设备")} value={query} onChange={e=>setQuery(e.target.value)}/></label><IconButton icon={Plus} label={t("添加设备")} onClick={onAdd}/></div><div className="device-list native-device-list">{tr(visible.length===0&&<p className="device-picker-empty">{t("没有匹配的设备")}</p>)}{tr(visible.map((row,index)=>{const d=row.device,Icon=d.isLocal?Monitor:Server;return <div className={'native-device-row '+(session.selected===d.id?'selected':'')+(over===d.id?' device-drag-over':'')} key={d.id} draggable={!term} onDragStart={e=>{drag.current=d.id;suppress.current=true;e.dataTransfer.setData('application/x-asterhub-device',d.id);e.dataTransfer.effectAllowed='move'}} onDragOver={e=>{if(drag.current){e.preventDefault();setOver(d.id);e.dataTransfer.dropEffect='move'}}} onDrop={e=>{if(!drag.current)return;e.preventDefault();reorder(drag.current,d.id);drag.current=null;setOver(null);setTimeout(()=>suppress.current=false,0)}} onDragEnd={()=>{drag.current=null;setOver(null);setTimeout(()=>suppress.current=false,0)}}><button className={'device-item '+(session.selected===d.id?'selected':'')} onClick={()=>{if(!suppress.current)session.select(d.id)}} onKeyDown={e=>{if(!term&&e.altKey&&['ArrowUp','ArrowDown'].includes(e.key)){e.preventDefault();const next=rows[index+(e.key==='ArrowUp'?-1:1)];if(next)reorder(d.id,next.device.id)}}} aria-pressed={session.selected===d.id} aria-description={t("拖拽调整顺序，或按 Alt 加上下方向键")} title={d.host+' · '+stateLabel[row.state]}><Icon size={18}/><span><strong>{d.id==='local'?t('本机'):d.name}</strong><small>{d.host}</small></span><i className={'status-dot '+(row.state==='retrying'?'warning':'')}/></button>{tr(!d.isLocal&&<IconButton icon={Pencil} label={tr('编辑 '+d.name)} className="native-device-edit" onClick={()=>onEdit(d)}/>)}</div>}))}</div></>}
export function ConnectionStatus({row}){useLocale();if(!row)return <span className="native-connection">{t("正在读取设备")}</span>;const local=row.device.isLocal,probe=row.probe,light=local?row.light:null;const delayed=!!light?.stale,age=light?Math.floor(light.ageMs/1000):0;const title=local?(light?(light.cpuSupported?'CPU/内存每 ':'内存每 ')+light.intervalMs/1000+' 秒轻采样；最后采样 '+new Date(light.time).toLocaleTimeString(locale(),{hour12:false})+'，'+age+' 秒前。GPU、磁盘与历史保持独立间隔。':'直接读取 Windows 资源'):probe?'SSH 测试耗时，包含握手、认证和轻量命令往返。测试于 '+new Date(probe.checkedAt).toLocaleTimeString(locale(),{hour12:false}):'尚未测试 SSH 连接耗时';return <span className={'native-connection '+(row.state==='retrying'||delayed?'warning':'')} title={tr(title)} data-light-state={light?(delayed?'stale':'fresh'):undefined}><i className="status-dot"/>{tr(stateLabel[row.state])}{tr(delayed&&<span className="native-latency">{t("读数延迟 ")}{tr(age)}{t(" 秒")}</span>)}{tr(!local&&probe?.success&&<span className="native-latency">{tr(probe.milliseconds)} ms</span>)}</span>}
export function DeviceDialog({device,onClose,onSaved}){useLocale();
 const [form,setForm]=useState(()=>({name:device?.name||'',host:device?.host||'',port:device?.port||22,user:device?.user||'',authMode:device?.authMode||'password',keyPath:device?.keyPath||'',password:''}));
 const [passwordChanged,setPasswordChanged]=useState(false),[busy,setBusy]=useState(''),[result,setResult]=useState(null),[error,setError]=useState(''),[confirm,setConfirm]=useState(false);const revision=useRef(0),mounted=useRef(true);
 useEffect(()=>{mounted.current=true;return()=>{mounted.current=false;revision.current++}},[]);
 const change=(key,value)=>{revision.current++;setResult(null);setError('');setForm(f=>({...f,[key]:value}))};
 const payload=()=>{const {password,...rest}=form;return {...rest,...(device?{id:device.id}:{}),...(passwordChanged||!device?{password}:{})}};
 async function perform(action){const seq=revision.current;setBusy(action);setError('');try{if(action==='test'){const response=await native.request('devices.test',payload(),45000);if(mounted.current&&seq===revision.current)setResult(response)}else if(action==='save'){await native.request('devices.save',payload());await onSaved();if(mounted.current)onClose()}else{await native.request('devices.remove',{id:device.id});await onSaved();if(mounted.current)onClose()}}catch(e){if(mounted.current&&seq===revision.current)setError(e.message)}finally{if(mounted.current)setBusy('')}}
 const input=(key,label,props={})=><label className="field">{tr(label)}<input value={form[key]} onChange={e=>change(key,e.target.value)} disabled={!!busy} {...props}/></label>;
 return <Modal title={tr(device?'编辑设备':'添加设备')} onClose={()=>{if(!busy)onClose()}} className="native-device-dialog"><form onSubmit={e=>{e.preventDefault();perform('save')}}><div className="dialog-content">
 {tr(input('name','设备名称',{placeholder:'例如：训练服务器',maxLength:80,required:true,autoComplete:'off'}))}
 <div className="native-connection-fields">{tr(input('host','主机地址',{placeholder:'IP 地址或主机名',required:true,autoComplete:'off'}))}{tr(input('port','端口',{type:'number',min:1,max:65535,required:true}))}</div>
 {tr(input('user','SSH 用户名',{placeholder:'留空时使用 SSH 默认用户',autoComplete:'off',maxLength:64}))}
 <label className="field">{t("认证方式")}<ThemedSelect aria-label={t("认证方式")} value={form.authMode} onChange={e=>change('authMode',e.target.value)} disabled={!!busy}>{tr([['password','密码'],['key','私钥文件'],['auto','SSH 配置 / 密钥代理'],['none','无密码认证']].map(([v,l])=><option key={v} value={v}>{tr(l)}</option>))}</ThemedSelect></label>
 {tr(form.authMode==='password'&&<label className="field">{t("密码")}<input aria-label={t("密码")} type="password" value={form.password} onChange={e=>{setPasswordChanged(true);change('password',e.target.value)}} placeholder={tr(device?.hasPassword?'已保存，留空保持不变':'可留空，按服务器实际认证配置')} autoComplete="new-password" disabled={!!busy}/><small>{tr(device?.hasPassword&&passwordChanged?'保存后将替换原密码。':'使用当前 Windows 账户加密保存在本机。')}</small></label>)}
 {tr(form.authMode==='key'&&<label className="field">{t("私钥文件")}<div className="native-key-field"><input aria-label={t("私钥文件")} value={form.keyPath} onChange={e=>change('keyPath',e.target.value)} placeholder={t("选择本机私钥")} disabled={!!busy}/><IconButton icon={FolderOpen} label={t("选择私钥文件")} disabled={!!busy} onClick={async()=>{try{const path=await native.request('devices.keyFile');if(path)change('keyPath',path)}catch(e){setError(e.message)}}}/></div></label>)}
 {tr(form.authMode==='none'&&<p className="native-field-note">{t("服务器需允许 SSH 无密码认证。")}</p>)}{tr(form.authMode==='auto'&&<p className="native-field-note">{t("使用 Windows OpenSSH 配置和可用的密钥代理。")}</p>)}
 <p className="native-field-note"><KeyRound size={14}/>{t("首次连接会在本机保存主机密钥，密钥变更时拒绝连接。")}</p>
 {tr(result&&<div className={'native-test-result '+(result.success?'success':'failure')} role="status">{tr(result.success&&<Check size={16}/>)}<div><strong>{tr(result.message)}</strong><small>{tr(result.details)}</small></div></div>)}{tr(error&&<p className="native-error" role="alert">{tr(error)}</p>)}
 {tr(confirm&&<div className="native-remove-confirm"><p>{t("移除“")}{device.id==='local'?t('本机'):device.name}{t("”？将停止采样，已有本机存档会保留。")}</p><Button tone="danger" disabled={!!busy} onClick={()=>perform('remove')}>{t("确认移除")}</Button><Button tone="quiet" onClick={()=>setConfirm(false)}>{t("取消")}</Button></div>)}
 </div><footer className="dialog-actions native-device-actions">{tr(device&&<IconButton icon={Trash2} label={t("移除设备")} onClick={()=>setConfirm(true)} disabled={!!busy}/>)}<Button className="native-test-button" onClick={()=>perform('test')} disabled={!!busy||!form.host||!form.name}>{tr(busy==='test'?<Loader2 size={15} className="native-spinner"/>:null)}{tr(busy==='test'?'正在测试…':'测试连接')}</Button><span/><Button tone="quiet" onClick={onClose} disabled={!!busy}>{t("取消")}</Button><Button tone="primary" type="submit" disabled={!!busy}>{tr(busy==='save'?'正在保存…':'保存设备')}</Button></footer></form></Modal>;
}
