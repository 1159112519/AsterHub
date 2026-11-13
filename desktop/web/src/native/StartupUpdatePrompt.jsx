import {t,tr,useLocale} from '../i18n/index.js';
import React,{useEffect,useState} from 'react';
import {Download,RefreshCw} from 'lucide-react';
import {Modal,Button} from '../components';
import {BrandMark} from '../brand';
import {native} from './bridge';
import './startup-update-prompt.css';
export function StartupUpdatePrompt(){useLocale();
 const [offer,setOffer]=useState(null),[busy,setBusy]=useState(false);
 useEffect(()=>{let alive=true;const receive=value=>{if(alive){setOffer(value);setBusy(value?.state==='installing')}};const off=native.subscribe(receive,'startup-update');native.request('updates.startup.state').then(value=>{if(value)receive(value)}).catch(()=>{});return()=>{alive=false;off()}},[]);
 if(!offer)return null;
 async function answer(accepted){if(busy)return;if(!accepted){setOffer(null);if(offer.token)native.request('updates.startup.answer',{token:offer.token,accepted:false}).catch(()=>{});return}setBusy(true);try{await native.request('updates.startup.answer',{token:offer.token,accepted:true});setOffer(null);setBusy(false)}catch(e){setBusy(false);setOffer({...offer,state:'error',message:e.message})}}
 return <Modal title={tr(offer.state==='error'?'更新未完成':busy?'正在准备更新':'发现新版本')} onClose={()=>!busy&&answer(false)} className="startup-update-prompt"><div className="startup-update-body"><BrandMark/><div>{tr(offer.state==='error'?<p role="alert">{tr(offer.message)}</p>:<><h3>AsterHub {tr(offer.version)}</h3><p>{tr(busy?'正在启动后台下载…':'安装包将在后台下载。你可以继续使用，下载校验完成后再确认安装并重启。')}</p></>)}</div></div><footer className="dialog-actions">{tr(offer.state==='error'?<Button onClick={()=>setOffer(null)}>{t("关闭")}</Button>:<><Button tone="quiet" disabled={busy} onClick={()=>answer(false)}>{t("稍后")}</Button><Button icon={busy?RefreshCw:Download} busy={busy} busyLabel="正在开始…" onClick={()=>answer(true)}>{t("后台下载")}</Button></>)}</footer></Modal>
}
