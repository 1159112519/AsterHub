import React,{useEffect,useRef,useState} from 'react';
import {AlertTriangle} from 'lucide-react';
import {Modal,Button} from '../components';
import {native} from './bridge';
export function EndProcessDialog({process,deviceId,onClose}){
 const [target,setTarget]=useState(null),[error,setError]=useState(''),[busy,setBusy]=useState(false),[done,setDone]=useState(false);const token=useRef(null),working=useRef(false);
 useEffect(()=>{let active=true;native.request('process.inspect',{deviceId,pid:process.pid,gpu:String(process.gpu)},20000).then(value=>{if(!active){native.request('process.cancel',{token:value.token}).catch(()=>{});return}token.current=value.token;setTarget(value)}).catch(e=>active&&setError(e.message));return()=>{active=false;if(token.current)native.request('process.cancel',{token:token.current}).catch(()=>{})}},[deviceId,process.pid,process.gpu]);
 const close=()=>{if(!working.current)onClose()};
 async function confirm(){if(working.current||!target?.canTerminate)return;working.current=true;setBusy(true);setError('');try{const value=await native.request('process.terminate',{token:token.current},20000);token.current=null;setDone(value||'结束请求已发送')}catch(e){setError(e.message);setTarget(null)}finally{working.current=false;setBusy(false)}}
 return <Modal title={done?'操作完成':'是否结束进程？'} onClose={close} className="process-confirmation"><div className="dialog-content"><div className="process-confirm-lead"><AlertTriangle size={22}/><p>{done||'结束后，尚未保存的计算结果可能丢失。请确认这是你要停止的任务。'}</p></div><dl className="process-identity"><div><dt>进程</dt><dd>{target?.name||process.path||process.name}</dd></div><div><dt>PID / GPU</dt><dd>{process.pid} / GPU {process.gpu}</dd></div><div><dt>用户</dt><dd>{target?.user||process.userFull||process.user}</dd></div>{(target?.commandLine||process.commandLine)&&<div><dt>完整命令</dt><dd className="process-command">{target?.commandLine||process.commandLine}</dd></div>}</dl>{!target&&!error&&!done&&<p role="status">正在核验进程身份…</p>}{target&&!target.canTerminate&&<p role="alert" className="native-error">{target.reason||'当前账户无权结束该进程'}</p>}{error&&<p role="alert" className="native-error">{error}。请关闭后重新选择进程。</p>}</div><footer className="dialog-actions"><Button tone="quiet" onClick={close} disabled={busy}>{done?'完成':'取消'}</Button>{!done&&<Button tone="danger" onClick={confirm} disabled={busy||!target?.canTerminate}>{busy?'正在结束…':'结束进程'}</Button>}</footer></Modal>
}
export class PageBoundary extends React.Component{
 state={failed:false};static getDerivedStateFromError(){return {failed:true}}
 render(){return this.state.failed?<section className="native-page-recovery" role="alert"><AlertTriangle size={30}/><h1>此页面暂时无法显示</h1><p>设备采样仍在继续，可以重新加载此页面或从左侧切换。</p><Button onClick={()=>this.setState({failed:false})}>重新加载页面</Button></section>:this.props.children}
}
