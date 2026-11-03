import React,{useEffect,useState} from 'react';
import {Download,RefreshCw} from 'lucide-react';
import {Modal,Button} from '../components';
import {BrandMark} from '../brand';
import {native} from './bridge';
import './startup-update-prompt.css';
export function StartupUpdatePrompt(){
 const [offer,setOffer]=useState(null),[busy,setBusy]=useState(false);
 useEffect(()=>{let alive=true;const receive=value=>{if(alive){setOffer(value);setBusy(value?.state==='installing')}};const off=native.subscribe(receive,'startup-update');native.request('updates.startup.state').then(value=>{if(value)receive(value)}).catch(()=>{});return()=>{alive=false;off()}},[]);
 if(!offer)return null;
 async function answer(accepted){if(busy)return;if(!accepted){setOffer(null);if(offer.token)native.request('updates.startup.answer',{token:offer.token,accepted:false}).catch(()=>{});return}setBusy(true);try{await native.request('updates.startup.answer',{token:offer.token,accepted:true})}catch(e){setBusy(false);setOffer({...offer,state:'error',message:e.message})}}
 return <Modal title={offer.state==='error'?'更新未完成':busy?'正在准备更新':'发现新版本'} onClose={()=>!busy&&answer(false)} className="startup-update-prompt"><div className="startup-update-body"><BrandMark/><div>{offer.state==='error'?<p role="alert">{offer.message}</p>:<><h3>AsterHub {offer.version}</h3><p>{busy?'正在下载并校验安装包，完成后将自动安装并重新启动。':'安装后 AsterHub 将重新启动，设备配置与历史记录会保留。'}</p></>}</div></div><footer className="dialog-actions">{offer.state==='error'?<Button onClick={()=>setOffer(null)}>关闭</Button>:<><Button tone="quiet" disabled={busy} onClick={()=>answer(false)}>稍后</Button><Button icon={busy?RefreshCw:Download} busy={busy} busyLabel="正在更新…" onClick={()=>answer(true)}>下载并安装</Button></>}</footer></Modal>
}
