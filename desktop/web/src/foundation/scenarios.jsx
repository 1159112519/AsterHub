// Internal development entry, intentionally excluded from the production bundle.
import React,{useState,useEffect} from 'react';import {createRoot} from 'react-dom/client';
import {Copy,FolderOpen,Plus} from 'lucide-react';
import {Button,Toolbar} from './ui';import {Modal,Dropdown,NumberValue,Chart} from '../components';
import {ThemedSelect} from '../themed-select';import {MotionSurface} from '../motion-surface';
import {installMotionTokens,motionDiagnostics} from '../motion';import {overlayDiagnostics} from './overlay';
import {tileTypography} from '../native/StorageTools';
import '../styles.css';import '../refinements.css';import '../native/native.css';import '../motion-layout.css';import '../native/layout.css';import '../native/readability.css';import './scenarios.css';
installMotionTokens();
function Lab(){
 const [skin,setSkin]=useState('graphite'),[compact,setCompact]=useState(false),[reduced,setReduced]=useState(false),[busy,setBusy]=useState(false),[modal,setModal]=useState(false),[surface,setSurface]=useState(false),[value,setValue]=useState(9),[mode,setMode]=useState('auto'),[sample,setSample]=useState(0),[diagnostics,setDiagnostics]=useState({});
 useEffect(()=>{document.documentElement.dataset.theme=skin;document.documentElement.dataset.motion=reduced?'reduced':'full'},[skin,reduced]);
 useEffect(()=>{const id=setInterval(()=>setSample(n=>n+1),1000);return()=>clearInterval(id)},[]);
 const timestamps=Array.from({length:40},(_,i)=>1700000000000+(sample+i)*1000),values=timestamps.map(t=>35+Math.sin(t/3000)*12);
 return <main className="foundation-lab" data-size={compact?'compact':'full'}>
 <header><h1>AsterHub 基础组件验收</h1><p>内部场景页：使用实际公共组件，不进入安装包导航。</p></header>
 <section><h2>皮肤与状态</h2><div className="lab-fields"><ThemedSelect aria-label="验收皮肤" value={skin} onChange={e=>setSkin(e.target.value)}>{['graphite','slate','forest','ocean','light'].map(s=><option key={s} value={s}>{s}</option>)}</ThemedSelect><label><input type="checkbox" checked={compact} onChange={e=>setCompact(e.target.checked)}/>窄布局</label><label><input type="checkbox" checked={reduced} onChange={e=>setReduced(e.target.checked)}/>减少动态效果</label><label><input type="checkbox" checked={busy} onChange={e=>setBusy(e.target.checked)}/>忙碌状态</label></div></section>
 <section><h2>按钮与工具栏</h2><Toolbar label="场景操作"><Button icon={Copy}>复制</Button><Button tone="primary" icon={Plus} busy={busy} busyLabel="正在处理">新建任务</Button><Button tone="danger" disabled>移至回收站</Button><Dropdown label="更多操作"><button type="button">菜单项目</button></Dropdown><Button onClick={()=>setModal(true)}>打开对话框</Button></Toolbar></section>
 <section className="lab-floating"><h2>浮层与连续开关</h2><Button onClick={()=>setSurface(v=>!v)}>切换浮层</Button><MotionSurface open={surface} position={{left:240,top:50,originX:0,originY:20}} className="lab-popover"><strong>资源详情</strong><p>减少动态效果与关闭中重新打开共用状态。</p><Button onClick={()=>setSurface(false)}>关闭浮层</Button></MotionSurface></section>
 <section><h2>稳定数字与实时曲线</h2><Toolbar label="数字场景"><Button onClick={()=>setValue(v=>v===9?1024:9)}>改变数字位数</Button><div className="lab-measure"><NumberValue value={value}/></div><NumberValue value={null}/><NumberValue value={0}/></Toolbar><Chart series={[{name:'CPU',color:'var(--blue)',values}]} timestamps={timestamps} live expectedInterval={1} unit="%" height={180}/></section>
 <section><h2>面积自适应文字</h2><div className="lab-tiles">{[320,180,90].map((w,i)=>{const t=tileTypography(w,150,'models');return <div key={w} className="lab-tile" style={{'--lab-color':['#a9c4e5','#c4b5df','#a9cdbd'][i]}}>{t.showName&&<span style={{fontSize:t.label,fontWeight:500}}>models</span>}{t.showValue&&<strong style={{fontSize:t.value,fontWeight:600}}>12.4 GiB</strong>}</div>})}</div></section>
 <section><h2>资源生命周期</h2><Button onClick={()=>setDiagnostics({motion:motionDiagnostics(),overlays:overlayDiagnostics()})}>读取诊断</Button><pre>{JSON.stringify(diagnostics,null,2)}</pre></section>
 {modal&&<Modal title="控件组合验收" onClose={()=>setModal(false)}><p>菜单和对话框叠加时，Esc 只关闭最上层。</p><ThemedSelect aria-label="对话框范围" value={mode} onChange={e=>setMode(e.target.value)}><option value="auto">自动范围</option><option value="fixed">固定范围</option></ThemedSelect><div className="dialog-actions"><Button onClick={()=>setModal(false)}>完成</Button></div></Modal>}
 </main>;
}
createRoot(document.getElementById('root')).render(<React.StrictMode><Lab/></React.StrictMode>);
