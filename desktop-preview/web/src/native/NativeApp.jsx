import {StartupUpdatePrompt} from './StartupUpdatePrompt';
import {resolveAppearance,applyAppearance,nativeAppearance} from '../appearance/resolve-appearance';
import {NumericUpdateProvider} from '../components';
import {MemoryWorkspace} from './MemoryTools';
import {StorageTools,StorageTaskBadge} from './StorageTools';
import './preview5-layout.css';
import {orderedSamples,liveSparkWindow,liveRangeSamples} from '../time-series';
import {FilesPage} from './FilesPage';
import {TerminalPage} from './TerminalPage';
import {NetworkPage,StoragePage} from './PeripheralPages';
import {EndProcessDialog,PageBoundary} from './ProcessDialog';
import {loadColor} from '../load-color';
import {Preferences} from './Preferences';
import {ByteValue,OverviewCards,ResourceInspector,ResourceFooter,HardwareInventory,MemoryDetails} from './ResourceWidgets';
import {FrequencyValue} from './FrequencyValue';
import {ThemedSelect} from "../themed-select";
import {DeviceSidebar,DeviceDialog,ConnectionStatus,useDeviceSession} from './Devices';
import {GpuInspector} from '../gpu-inspector';
import React,{useCallback,useEffect,useLayoutEffect,useMemo,useRef,useState} from 'react';
import {Cpu,Microchip,MemoryStick,Network,HardDrive,Monitor,ChevronRight,LayoutDashboard,History,Terminal,FolderOpen,Settings2,Info,Clock,Activity,X,Search,PanelLeftClose,PanelLeftOpen} from 'lucide-react';
import {BrandMark} from '../brand';
import {Chart,NumberValue,Meter,Sparkline,SectionTitle,Button,Modal,IconButton,Empty} from '../components';
import {ProcessTable} from '../pages/Resources';
import {HistoryView} from './HistoryView';
import {MotionSurface} from '../motion-surface';
import {DisplayStyleSelect} from '../display-styles';
import {installMotionTokens,revealContent} from '../motion';
import {native} from './bridge';
import {useDismissableLayer} from '../foundation/overlay';
import {workspaceState} from '../foundation/workspace-state.js';
import {createScrollMemory} from '../foundation/scroll-memory.js';
import {resourceChartSamples} from './light-telemetry.js';
import './sidebar-collapse.css';
import {normalize,percent,bytes,gpuAverage} from './telemetry';
import {storageSummary,frequency} from './resource-summary';
const navigation=[['overview','资源概览',LayoutDashboard],['cpu','CPU',Cpu],['gpu','GPU',Microchip],['memory','内存',MemoryStick],['network','网络',Network],['storage','存储',HardDrive],['history','历史记录',History]];
const colors={cpu:'var(--blue)',gpu:'var(--chart-gpu,var(--green))',memory:'var(--purple)',network:'var(--amber)',storage:'var(--muted)'};
const saved=()=>{try{return JSON.parse(localStorage.getItem('asterhub-preview-appearance'))||{}}catch{return {}}};
function Reading({value,unit='%',decimals=1,stale=false}){return value==null?<span className="subdued">—</span>:<NumberValue value={value} decimals={decimals} suffix={unit} stale={stale}/>}
function TimeChart({samples,metric='cpu',onSelect,selected,popover,live=true,rangeMinutes=15}){
 const points=useMemo(()=>orderedSamples(live?liveRangeSamples(samples,rangeMinutes):samples),[samples,live,rangeMinutes]),timestamps=useMemo(()=>points.map(s=>s.time),[points]);
 const value=s=>metric==='gpu'?gpuAverage(s):s[metric];
 const series=useMemo(()=>[{name:metric==='cpu'?'CPU':metric==='gpu'?'GPU':'内存',color:colors[metric],values:points.map(value)}],[points,metric]);
 return <Chart series={series} timestamps={timestamps} height={240} scalable rangeMinutes={rangeMinutes} live={live} breakBefore={points.some(p=>p.gapBefore)?points.map(p=>!!p.gapBefore):null} expectedInterval={points.at(-1)?.interval??points.find(p=>p.interval)?.interval} onSelect={onSelect?(i)=>{if(Number.isFinite(value(points[i])))onSelect(points[i],i)}:undefined} selected={selected} selectionPopover={popover}/>;
}
function Hardware({sample}){return <HardwareInventory sample={sample}/>;}
function NativeGpuCard({gpu,samples,style,onClick}){const {recent,spanMs}=liveSparkWindow(samples),values=recent.map(s=>s.gpus.find(g=>g.id===gpu.id)?.util??null);return <button className={'gpu-tile '+(style==='classic'?'gpu-classic':'gpu-trend')} onClick={onClick} aria-label={'查看 GPU '+gpu.id}>
 <header><span className="gpu-title"><Microchip size={15}/>GPU {gpu.id}</span><span className="state-label">{gpu.util==null?'未提供':gpu.util>10?'运行中':'空闲'}</span></header><div className="gpu-model">{gpu.name}</div>
 <div className="gpu-reading"><span className="native-gpu-number-slot"><Reading value={gpu.util}/></span>{style==='classic'?<span className="gpu-util-label">利用率</span>:values.length>1&&<Sparkline values={values} timestamps={recent.map(s=>s.time)} spanMs={spanMs} color={colors.gpu}/>}</div>{gpu.util!=null&&<Meter value={gpu.util} color={loadColor(gpu.util)}/>}
 <div className="gpu-card-details"><div className="gpu-memory-heading"><span>显存</span><span><Reading value={gpu.used} decimals={2} unit=" GiB"/><em> / {gpu.total?.toFixed(2)??'—'} GiB</em></span></div>{gpu.total>0&&gpu.used!=null&&<Meter value={gpu.used/gpu.total*100} color={loadColor(gpu.used/gpu.total*100)}/>}
 <div className="gpu-hardware-row"><span>当前频率</span><span><FrequencyValue value={gpu.clock}/></span></div>{gpu.memoryClock>0&&<div className="gpu-hardware-row"><span>显存频率</span><span><FrequencyValue value={gpu.memoryClock}/></span></div>}<div className="gpu-hardware-row"><span>温度 / 风扇</span><span><Reading value={gpu.temp} unit="°C" decimals={0}/><em> / </em><Reading value={gpu.fan} decimals={0}/></span></div>
 <div className="gpu-hardware-row"><span>功耗 / 限制</span><span><Reading value={gpu.power} unit=" W" decimals={0}/><em> / {gpu.limit==null?'—':gpu.limit+' W'}</em></span></div></div></button>}
function SnapshotProcesses({sample}){return <ProcessTable device={{gpuCount:sample?.gpus.length||0}} processes={sample?.processes||[]} readonly/>}
export default function NativeApp(){
 const [collapsed,setCollapsed]=useState(()=>localStorage.getItem('asterhub-sidebar-collapsed')==='true'),[devicePicker,setDevicePicker]=useState(false),devicePopup=useRef(null),deviceTrigger=useRef(null);
 useDismissableLayer({open:collapsed&&devicePicker,refs:[devicePopup,deviceTrigger],onDismiss:()=>setDevicePicker(false)});
 useEffect(()=>{localStorage.setItem('asterhub-sidebar-collapsed',String(collapsed));setDevicePicker(false)},[collapsed]);
 const [info,setInfo]=useState(null),[page,setPage]=useState('overview'),[settings,setSettings]=useState(()=>({theme:globalThis.__ASTER_THEME||'graphite',displayStyle:'classic',reduced:false,iconThemeId:globalThis.__ASTER_ICON||'01',...saved()})),[dialog,setDialog]=useState(false),[settingsTab,setSettingsTab]=useState('appearance'),[desktopPrefs,setDesktopPrefs]=useState(null),[error,setError]=useState(''),[gpu,setGpu]=useState(null),[deviceDialog,setDeviceDialog]=useState(null),[inspect,setInspect]=useState(null),[terminalVisited,setTerminalVisited]=useState(false),[filesVisited,setFilesVisited]=useState(false),[storageVisited,setStorageVisited]=useState(false),[ending,setEnding]=useState(null);const main=useRef(null);const [systemDark,setSystemDark]=useState(()=>matchMedia('(prefers-color-scheme:dark)').matches);useEffect(()=>{const m=matchMedia('(prefers-color-scheme:dark)'),change=()=>setSystemDark(m.matches);m.addEventListener('change',change);return()=>m.removeEventListener('change',change)},[]);
 const session=useDeviceSession(setError),{sample,samples,lightSamples}=session,activeDevice=session.row?.device;
 const cpuSamples=useMemo(()=>resourceChartSamples(samples,lightSamples,'cpu'),[samples,lightSamples]),memorySamples=useMemo(()=>resourceChartSamples(samples,lightSamples,'memory'),[samples,lightSamples]);
 const [storageTool,setStorageTool]=useState({deviceId:null,tool:null}),scrollMemory=useRef(null);
 const handleStorageToolChange=useCallback((deviceId,tool)=>setStorageTool(previous=>previous.deviceId===deviceId&&previous.tool===tool?previous:{deviceId,tool}),[]);
 const activeStorageTool=storageTool.deviceId===session.selected?storageTool.tool:null;
 const [memoryTool,setMemoryTool]=useState({deviceId:null,tool:'monitor'});
 const handleMemoryToolChange=useCallback((deviceId,tool)=>setMemoryTool(previous=>previous.deviceId===deviceId&&previous.tool===tool?previous:{deviceId,tool}),[]);
 const activeMemoryTool=memoryTool.deviceId===session.selected?memoryTool.tool:'monitor';
 const scrollKey=page==='storage'?`scroll:storage:${activeStorageTool||'monitor'}`:page==='memory'?`scroll:memory:${activeMemoryTool}`:`scroll:${page}`;
 // Host inventories always contain the local device. The initial empty hook value
 // is not an authoritative deletion list and must not prune session memory.
 useEffect(()=>{if(session.devices.some(row=>row.device.id==='local'))workspaceState.retainDevices(session.devices.map(row=>row.device.id))},[session.devices]);
 useLayoutEffect(()=>{
  const element=main.current;if(!element)return;
  const controller=createScrollMemory(element,workspaceState.open(session.selected,scrollKey,0));scrollMemory.current=controller;
  const tween=revealContent(element);
  return()=>{controller.dispose();if(scrollMemory.current===controller)scrollMemory.current=null;tween?.kill()};
 },[page,session.selected,scrollKey]);
 useEffect(()=>{setGpu(null);setInspect(null)},[page,session.selected]);
 useEffect(()=>{installMotionTokens();let active=true;native.request('desktop.settings').then(value=>active&&setDesktopPrefs(value)).catch(e=>active&&setError(e.message));const navigate=native.subscribe(command=>{setSettingsTab(command==='about'?'about':'appearance');setDialog(true)},'navigate');native.request('app.info').then(value=>active&&setInfo(value)).catch(e=>active&&setError(e.message));return()=>{active=false;navigate()}},[]);
 useEffect(()=>{let alive=true;Promise.resolve(document.fonts?.ready).then(()=>native.request('app.uiReady')).then(()=>{if(!alive)return;requestAnimationFrame(()=>{document.getElementById('launch-shell')?.classList.add('complete');setTimeout(()=>document.getElementById('launch-shell')?.remove(),280)})}).catch(e=>setError(e.message));return()=>{alive=false}},[]);
 useEffect(()=>{const resolved=resolveAppearance(settings,systemDark,settings.iconThemeId||globalThis.__ASTER_ICON);applyAppearance(resolved);document.documentElement.dataset.motion=settings.reduced?'reduced':'full';localStorage.setItem('asterhub-preview-appearance',JSON.stringify(settings));native.request('window.appearance',nativeAppearance(resolved)).catch(e=>setError(e.message))},[settings,systemDark]);
 useEffect(()=>{if(desktopPrefs){document.documentElement.dataset.density=desktopPrefs.density}},[desktopPrefs]);
 useEffect(()=>{if(page==='terminal')setTerminalVisited(true);if(page==='files')setFilesVisited(true);if(page==='storage')setStorageVisited(true)},[page]);
 const label=navigation.find(n=>n[0]===page)?.[1]||({terminal:'终端',files:'文件'})[page],cores=sample?.cores||[],device={gpuCount:sample?.gpus.length||0};const footer=[['cpu','CPU',Cpu,sample?.cpu,'%',samples.map(s=>s.cpu)],['gpu','GPU',Microchip,gpuAverage(sample),'%',samples.map(gpuAverage)],['memory','内存',MemoryStick,sample?.memory,'%',samples.map(s=>s.memory)],['network','网络',Network,sample?.network?.DownloadBytesPerSecond,null,samples.map(s=>s.network?.DownloadBytesPerSecond)],['storage','存储',HardDrive,storageSummary(sample).percent,'%',samples.map(s=>storageSummary(s).percent)]];
 return <NumericUpdateProvider intervalMs={activeDevice?.isLocal?(sample.light?.intervalMs??desktopPrefs?.localIntervalMs??2000):(sample.interval||3)*1000}><div className={"app-shell native-workbench preview5-workbench"+(collapsed?" sidebar-collapsed":"")}><aside className="sidebar"><div className="brand"><div className="brand-mark"><BrandMark/></div><div className="sidebar-brand-name"><strong>AsterHub</strong></div><IconButton className="sidebar-collapse-toggle" icon={collapsed?PanelLeftOpen:PanelLeftClose} label={collapsed?'展开侧栏':'折叠侧栏'} aria-expanded={!collapsed} onClick={()=>setCollapsed(v=>!v)}/></div>{collapsed?<div className="collapsed-device"><button ref={deviceTrigger} aria-label={'切换设备，当前 '+(activeDevice?.name||'本机')} aria-expanded={devicePicker} title={activeDevice?.name||'本机'} onClick={()=>setDevicePicker(v=>!v)}><Monitor size={21}/><span>{activeDevice?.name||'本机'}</span><ChevronRight size={13}/></button>{devicePicker&&<div ref={devicePopup} className="sidebar-device-popover"><DeviceSidebar session={{...session,select:id=>{session.select(id);setDevicePicker(false)}}} onAdd={()=>{setDevicePicker(false);setDeviceDialog({})}} onEdit={d=>{setDevicePicker(false);setDeviceDialog(d)}}/></div>}</div>:<DeviceSidebar session={session} onAdd={()=>setDeviceDialog({})} onEdit={setDeviceDialog}/>}<div className="sidebar-section-label">资源监控</div><nav>{navigation.map(([id,name,Icon])=><button className={'nav-item '+(page===id?'active':'')} key={id} title={collapsed?name:undefined} aria-label={name} onClick={()=>setPage(id)}><Icon size={17}/><span>{name}</span></button>)}</nav><div className="sidebar-section-label">工作空间</div><nav>{[[Terminal,'终端','terminal'],[FolderOpen,'文件','files']].map(([Icon,name,id])=><button key={name} title={collapsed?name:undefined} aria-label={name} className={'nav-item '+(page===id?'active':'')} onClick={()=>setPage(id)}><Icon size={17}/><span>{name}</span></button>)}</nav><div className="sidebar-bottom"><StorageTaskBadge onOpen={id=>{session.select(id);setPage('storage')}}/><button className="nav-item" title={collapsed?"偏好设置":undefined} aria-label="偏好设置" onClick={()=>{setSettingsTab('appearance');setDialog(true)}}><Settings2 size={17}/><span>偏好设置</span></button><button title={collapsed?"关于 AsterHub":undefined} aria-label="关于 AsterHub" className="nav-item native-about-entry" onClick={()=>{setSettingsTab('about');setDialog(true)}}><Info size={17}/><span>关于 AsterHub<small>{info?.version||info?.stage||'Windows 预览版'}</small></span></button></div></aside>
 <div className="workspace"><header className="topbar"><div className="topbar-left"><Monitor size={17}/><span className="top-device">{activeDevice?.name||'设备'}</span><ChevronRight size={14}/><span className="top-page">{label}</span>{page==='storage'&&activeStorageTool&&<><ChevronRight size={14}/><span className="top-tool">{activeStorageTool==='analysis'?'磁盘空间分析':'大文件清理'}</span></>}{page==='memory'&&session.selected==='local'&&activeMemoryTool!=='monitor'&&<><ChevronRight size={14}/><span className="top-tool">{activeMemoryTool==='cleanup'?'内存清理':'自动策略'}</span></>}</div><div className="topbar-right"><span>{sample.time?new Date(sample.time).toLocaleTimeString('zh-CN',{hour12:false}):'正在采样'}</span><span className="topbar-separator"/><ConnectionStatus row={session.row}/></div></header>
 <main onScroll={()=>scrollMemory.current?.onScroll()} ref={main} className="main-content" data-reading-state={['cpu','memory'].includes(page)&&sample.light?(sample.light.stale?'stale':'live'):session.stale?'stale':session.hasSample?'live':'loading'}><PageBoundary key={page+session.selected}>{error&&<p className="native-error" role="alert">{error}</p>}{session.connectionError&&<div className="native-connection-notice" role="status"><Activity size={17}/><div><strong>{session.connectionError}</strong><span>{session.hasSample?'保留上次读数 · '+new Date(session.lastSuccess).toLocaleTimeString('zh-CN',{hour12:false})+' · 恢复连接后继续更新':'等待连接成功后显示读数'}</span></div></div>}
 <div hidden={page==='terminal'||page==='files'||page==='storage'}>{page==='history'?<HistoryView key={session.selected} deviceId={session.selected}/>:!sample?<Empty icon={Activity} title={'正在读取'+(activeDevice?.name||'设备')+'状态'} description="等待第一个真实采样，不填充演示数据。"/>:<>
 <div className="page-heading"><div><div className="eyebrow">{activeDevice?.isLocal?'本机':'SSH'} · {activeDevice?.host}</div><h1>{label}</h1><p>{page==='gpu'?'':page==='network'?'网络吞吐与活动接口':page==='storage'?'卷容量与可用空间':page==='overview'?'':page==='memory'?'':sample.hardware?.CpuModel||'硬件信息正在读取'}</p></div>{page==='gpu'&&<DisplayStyleSelect value={settings.displayStyle} onChange={displayStyle=>setSettings(s=>({...s,displayStyle}))}/>}</div>
 {page==='overview'&&<><OverviewCards key={session.selected} sample={sample} onInspect={setInspect}/><HardwareInventory sample={sample}/></>}
 {page==='gpu'&&(sample.gpus.length?<><section className="native-gpu-section"><SectionTitle>GPU</SectionTitle><div className={sample.gpus.length===1?"single-gpu-layout":""}><div className="gpu-grid" data-display-style={settings.displayStyle}>{sample.gpus.map(g=><NativeGpuCard key={session.selected+':'+g.id} gpu={g} samples={samples} style={settings.displayStyle} onClick={e=>setGpu({id:g.id,anchor:e.currentTarget})}/>)}</div></div></section><ProcessTable device={device} processes={sample.processes} onEnd={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} onTerminal={()=>setPage('terminal')}/></>:<Empty icon={Microchip} title={session.hasSample?"未检测到独立显卡":"正在获取显卡信息"} description={session.hasSample?"CPU、内存、网络与存储仍持续记录。":"连接成功后显示显卡型号与读数。"}/>)}
 {page==='cpu'&&<><section className="resource-chart"><SectionTitle>CPU 利用率</SectionTitle><TimeChart samples={cpuSamples}/></section><section className="core-section"><SectionTitle meta={(session.hasSample?cores.length:'—')+' 个逻辑处理器'}>逐核占用</SectionTitle><div className="core-grid">{cores.map(c=><div className="core-cell" key={c.Index} style={{backgroundColor:'color-mix(in srgb,var(--blue) '+Math.max(8,c.Util*.65)+'%,var(--surface))'}}><span>CPU {c.Index}</span><strong>{Number.isFinite(c.Util)?c.Util.toFixed(0)+'%':'—'}</strong></div>)}</div></section></>}
 {page==='memory'&&<MemoryWorkspace key={session.selected} deviceId={session.selected} onToolChange={handleMemoryToolChange}><div className="detail-summary"><div><span>已使用</span><strong><Reading value={sample.memUsed} unit=" GiB" stale={sample.light?.stale??sample.stale}/></strong></div><div><span>总容量</span><strong><Reading value={sample.memTotal} unit=" GiB" stale={sample.light?.stale??sample.stale}/></strong></div><div><span>使用率</span><strong><Reading value={sample.memory} stale={sample.light?.stale??sample.stale}/></strong></div></div><section className="resource-chart"><SectionTitle>内存使用</SectionTitle><TimeChart samples={memorySamples} metric="memory"/></section><MemoryDetails sample={sample}/></MemoryWorkspace>}
 {page==='network'&&<NetworkPage deviceId={session.selected} sample={sample} samples={samples}/>}
 {page==='storage'&&<StoragePage deviceId={session.selected} sample={sample} samples={samples}/>}
 </>}
 </div></PageBoundary><PageBoundary key="terminal-workspace">{storageVisited&&<StorageTools key={session.selected} deviceId={session.selected} sample={sample} samples={samples} settings={settings} active={page==='storage'} onToolChange={handleStorageToolChange}/>}{terminalVisited&&<TerminalPage deviceId={session.selected} settings={settings} active={page==='terminal'}/>}{filesVisited&&<div hidden={page!=='files'} className="retained-files-page"><FilesPage deviceId={session.selected} devices={session.devices} active={page==='files'}/></div>}</PageBoundary></main><ResourceFooter key={session.selected} items={footer} page={page} onSelect={setPage} sample={sample} samples={samples} lightSamples={lightSamples}/></div>
 {gpu!==null&&sample?.gpus.some(g=>g.id===gpu.id)&&<GpuInspector deviceId={session.selected} gpu={{...sample.gpus.find(g=>g.id===gpu.id),series:[]}} anchor={gpu.anchor} pulse={0} processes={sample.processes} liveSamples={samples} onClose={()=>setGpu(null)} onEndProcess={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} confirmationOpen={!!ending}/>}
 {inspect&&sample&&<ResourceInspector deviceId={session.selected} key={inspect.id} {...inspect} sample={sample} samples={samples} lightSamples={lightSamples} onClose={()=>setInspect(null)} onEndProcess={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} confirmationOpen={!!ending}/>}
 {ending&&<EndProcessDialog {...ending} onClose={()=>setEnding(null)}/>}
 {deviceDialog&&<DeviceDialog device={deviceDialog.id?deviceDialog:null} onClose={()=>setDeviceDialog(null)} onSaved={session.refresh}/>}
 <StartupUpdatePrompt/>
 {dialog&&<Preferences settings={settings} info={info} initialTab={settingsTab} onClose={()=>setDialog(false)} onApplied={(next,interval,desktop)=>{setSettings(next);setDesktopPrefs(desktop);setInfo(s=>({...s,interval}))}}/>}

 </div></NumericUpdateProvider>;
}
