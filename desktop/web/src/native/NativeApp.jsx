import {t,tr,useLocale,locale} from '../i18n/index.js';
import {applyNumberMotion} from '../number-motion';
import {useOpticalAlignment} from '../foundation/optical-alignment';
import {storageTaskFocus} from '../foundation/task-focus';
import {StartupUpdatePrompt} from './StartupUpdatePrompt';
import {TaskCenter} from './TaskCenter';
import {resolveAppearance,applyAppearance,nativeAppearance} from '../appearance/resolve-appearance';
import {NumericUpdateProvider} from '../components';
import {AnalysisWorkspace} from './analysis/ResourceAnalysis';
import {MemoryWorkspace} from './MemoryTools';
import {StorageTools} from './StorageTools';
import './layout.css';
import {orderedSamples,liveSparkWindow,liveRangeSamples,timelineSampleProps} from '../time-series';
import {FilesPage} from './FilesPage';
import {TerminalPage} from './TerminalPage';
import {StoragePage} from './PeripheralPages';
import {NetworkWorkspace} from './NetworkSpeed';
import {EndProcessDialog,PageBoundary} from './ProcessDialog';
import {loadColor} from '../load-color';
import {Preferences} from './Preferences';
import {ByteValue,OverviewCards,ResourceInspector,ResourceFooter,HardwareInventory,MemoryDetails} from './ResourceWidgets';
import {FrequencyValue} from './FrequencyValue';
import {ThemedSelect} from "../themed-select";
import {DeviceSwitcher,DeviceDialog,ConnectionStatus,useDeviceSession} from './Devices';
import {GpuInspector} from '../gpu-inspector';
import React,{useCallback,useEffect,useLayoutEffect,useMemo,useRef,useState} from 'react';
import {Cpu,Microchip,MemoryStick,Network,HardDrive,Monitor,ChevronRight,LayoutDashboard,History,Terminal,FolderOpen,Settings2,Info,Clock,Activity,X,Search,PanelLeftClose,PanelLeftOpen} from 'lucide-react';
import {BrandMark} from '../brand';
import {Chart,NumberValue,Meter,Sparkline,SectionTitle,Button,Modal,IconButton,Empty} from '../components';
import {ProcessTable} from '../pages/Resources';
import {HistoryView} from './HistoryView';
import {MotionSurface} from '../motion-surface';
import {installMotionTokens,revealContent} from '../motion';
import {native} from './bridge';
import {workspaceState} from '../foundation/workspace-state.js';
import {useSidebarDrawer} from '../foundation/use-sidebar-drawer';
import {useWorkspaceState} from '../foundation/use-workspace-state.js';
import {createScrollMemory} from '../foundation/scroll-memory.js';
import {resourceChartSamples} from './light-telemetry.js';
import './sidebar-collapse.css';
import {normalize,percent,bytes} from './telemetry';
import {storageSummary,frequency} from './resource-summary';
const navigation=[['overview','资源概览',LayoutDashboard],['cpu','CPU',Cpu],['gpu','GPU',Microchip],['memory','内存',MemoryStick],['network','网络',Network],['storage','存储',HardDrive],['history','历史记录',History]];
const colors={cpu:'var(--blue)',gpu:'var(--chart-gpu,var(--green))',memory:'var(--icon-pink)',network:'var(--amber)',storage:'var(--muted)'};
const saved=()=>{try{return JSON.parse(localStorage.getItem('asterhub-preview-appearance'))||{}}catch{return {}}};
function Reading({value,unit='%',decimals=1,stale=false}){useLocale();return value==null?<span className="subdued">—</span>:<NumberValue value={value} decimals={decimals} suffix={unit} stale={stale}/>}
function TimeChart({samples,metric='cpu',gpuId,onSelect,selected,popover,live=true,rangeMinutes=15}){useLocale();
 const points=useMemo(()=>orderedSamples(live?liveRangeSamples(samples,rangeMinutes):samples),[samples,live,rangeMinutes]),timestamps=useMemo(()=>points.map(s=>s.time),[points]);
 const value=s=>metric==='gpu'?s.gpus?.find(g=>g.id===gpuId)?.util??null:s[metric];
 const series=useMemo(()=>[{name:metric==='cpu'?'CPU':metric==='gpu'?'GPU':'内存',color:colors[metric],values:points.map(value)}],[points,metric,gpuId]);
 return <Chart series={series} timestamps={timestamps} height={240} scalable rangeMinutes={rangeMinutes} live={live} {...timelineSampleProps(points)} expectedInterval={points.at(-1)?.interval??points.find(p=>p.interval)?.interval} onSelect={onSelect?(i)=>{if(Number.isFinite(value(points[i])))onSelect(points[i],i)}:undefined} selected={selected} selectionPopover={popover}/>;
}
function Hardware({sample}){useLocale();return <HardwareInventory sample={sample}/>;}
function NativeGpuCard({gpu,samples,style,onClick}){useLocale();const {recent,spanMs}=liveSparkWindow(samples),values=recent.map(s=>s.gpus.find(g=>g.id===gpu.id)?.util??null);const Tile=onClick?'button':'article';return <Tile className={'gpu-tile '+(onClick?'':'native-static-gpu ')+(style==='classic'?'gpu-classic':'gpu-trend')} onClick={onClick} aria-label={tr((onClick?'查看 GPU ':'GPU ')+gpu.id)}>
 <header><span className="gpu-title"><Microchip size={15}/>GPU {tr(gpu.id)}</span><span className="state-label">{tr(gpu.util==null?'未提供':gpu.util>10?'运行中':'空闲')}</span></header><div className="gpu-model">{gpu.name}</div>
 <div className="gpu-reading"><span className="native-gpu-number-slot"><Reading value={gpu.util}/></span>{tr(style==='classic'?<span className="gpu-util-label">{t("利用率")}</span>:values.length>1&&<Sparkline values={values} timestamps={recent.map(s=>s.time)} spanMs={spanMs} {...timelineSampleProps(recent)} color={colors.gpu}/>)}</div>{tr(gpu.util!=null&&<Meter value={gpu.util} color={loadColor(gpu.util)}/>)}
 <div className="gpu-card-details"><div className="gpu-memory-heading"><span>{t("显存")}</span><span><Reading value={gpu.used} decimals={2} unit=" GiB"/><em> / {tr(gpu.total?.toFixed(2)??'—')} GiB</em></span></div>{tr(gpu.total>0&&gpu.used!=null&&<Meter value={gpu.used/gpu.total*100} color={loadColor(gpu.used/gpu.total*100)}/>)}
 <div className="gpu-hardware-row"><span>{t("当前频率")}</span><span><FrequencyValue value={gpu.clock}/></span></div>{tr(gpu.memoryClock>0&&<div className="gpu-hardware-row"><span>{t("显存频率")}</span><span><FrequencyValue value={gpu.memoryClock}/></span></div>)}<div className="gpu-hardware-row"><span>{t("温度 / 风扇")}</span><span><Reading value={gpu.temp} unit="°C" decimals={0}/><em> / </em><Reading value={gpu.fan} decimals={0}/></span></div>
 <div className="gpu-hardware-row"><span>{t("功耗 / 限制")}</span><span><Reading value={gpu.power} unit=" W" decimals={0}/><em> / {tr(gpu.limit==null?'—':gpu.limit+' W')}</em></span></div></div></Tile>}
function ComputeTrend({metric,samples,value,gpuId,children}){useLocale();
 const title=metric==='cpu'?'CPU':'GPU';
 return <section className="resource-chart native-compute-trend"><SectionTitle action={<div className="native-compute-controls">{tr(children)}<div className="native-compute-current"><span>{t("当前")}</span><strong><Reading value={value}/></strong></div></div>}>{tr(title)}{t(" 利用率")}</SectionTitle><TimeChart samples={samples} metric={metric} gpuId={gpuId}/></section>;
}
function GpuWorkspace({sample,samples,device,style,onInspect,onEnd,onTerminal}){useLocale();
 const single=sample.gpus.length===1,gpu=sample.gpus[0];
 return <>
  {tr(single&&<ComputeTrend metric="gpu" samples={samples} value={gpu.util} gpuId={gpu.id}/>)}
  <section className="native-gpu-section"><SectionTitle>{tr(single?'显卡状态':'GPU 详情')}</SectionTitle><div className={single?'single-gpu-layout':''}><div className="gpu-grid" data-display-style={style}>{tr(sample.gpus.map(g=><NativeGpuCard key={g.id} gpu={g} samples={samples} style={style} onClick={single?undefined:e=>onInspect(g.id,e.currentTarget)}/>))}</div></div></section>
  <ProcessTable device={device} processes={sample.processes} onEnd={onEnd} onTerminal={onTerminal}/>
 </>;
}
function SnapshotProcesses({sample}){useLocale();return <ProcessTable device={{gpuCount:sample?.gpus.length||0}} processes={sample?.processes||[]} readonly/>}
export default function NativeApp(){useLocale();
 useOpticalAlignment();
 const [collapsed,setCollapsed]=useState(()=>localStorage.getItem('asterhub-sidebar-collapsed')==='true');
 const toggleSidebar=useSidebarDrawer(collapsed,setCollapsed);
 useEffect(()=>{localStorage.setItem('asterhub-sidebar-collapsed',String(collapsed))},[collapsed]);
 const [info,setInfo]=useState(null),[settings,setSettings]=useState(()=>({theme:globalThis.__ASTER_THEME||'graphite',displayStyle:'classic',reduced:false,numberMotion:'current',iconThemeId:globalThis.__ASTER_ICON||'01',...saved()})),[dialog,setDialog]=useState(false),[settingsTab,setSettingsTab]=useState('appearance'),[desktopPrefs,setDesktopPrefs]=useState(null),[error,setError]=useState(''),[gpu,setGpu]=useState(null),[deviceDialog,setDeviceDialog]=useState(null),[inspect,setInspect]=useState(null),[terminalVisited,setTerminalVisited]=useState(false),[filesVisited,setFilesVisited]=useState(false),[storageVisited,setStorageVisited]=useState(false),[ending,setEnding]=useState(null);const main=useRef(null);const [systemDark,setSystemDark]=useState(()=>matchMedia('(prefers-color-scheme:dark)').matches);useEffect(()=>{const m=matchMedia('(prefers-color-scheme:dark)'),change=()=>setSystemDark(m.matches);m.addEventListener('change',change);return()=>m.removeEventListener('change',change)},[]);
 const session=useDeviceSession(setError),{sample,samples,lightSamples}=session,activeDevice=session.row?.device;
 const [navigationView,setNavigationView]=useWorkspaceState(session.selected,'navigation',()=>({page:'overview'}));
 const page=navigationView.page,setPage=next=>setNavigationView(view=>({...view,page:next}));
 const [workspaceTools]=useWorkspaceState(session.selected,'workspace-tools',()=>({storageTool:null,memoryTool:'monitor'}));
 function openDevicePage(id,next){workspaceState.open(id,'navigation',()=>({page:'overview'})).setState({page:next});session.select(id)}
 const showResourceSeries=page!=='overview';
 const cpuSamples=useMemo(()=>showResourceSeries?resourceChartSamples(samples,lightSamples,'cpu'):samples,[samples,lightSamples,showResourceSeries]),memorySamples=useMemo(()=>showResourceSeries?resourceChartSamples(samples,lightSamples,'memory'):samples,[samples,lightSamples,showResourceSeries]);
 const scrollMemory=useRef(null);
 const handleStorageToolChange=useCallback((deviceId,tool)=>workspaceState.open(deviceId,'workspace-tools',()=>({storageTool:null,memoryTool:'monitor'})).setState(previous=>previous.storageTool===tool?previous:{...previous,storageTool:tool}),[]);
 const activeStorageTool=workspaceTools.storageTool;
 const handleMemoryToolChange=useCallback((deviceId,tool)=>workspaceState.open(deviceId,'workspace-tools',()=>({storageTool:null,memoryTool:'monitor'})).setState(previous=>previous.memoryTool===tool?previous:{...previous,memoryTool:tool}),[]);
 const activeMemoryTool=workspaceTools.memoryTool;
 const scrollKey=`scroll:${page}`;
 // Host inventories always contain the local device. The initial empty hook value
 // is not an authoritative deletion list and must not prune session memory.
 useEffect(()=>{if(session.devices.some(row=>row.device.id==='local'))workspaceState.retainDevices(session.devices.map(row=>row.device.id))},[session.devices]);
 useLayoutEffect(()=>{
  const element=main.current;if(!element)return;
  const controller=createScrollMemory(element,workspaceState.open(session.selected,scrollKey,0));scrollMemory.current=controller;
  const tween=revealContent(element);
  return()=>{controller.dispose();if(scrollMemory.current===controller)scrollMemory.current=null;tween?.kill()};
 },[page,session.selected,scrollKey]);
 useEffect(()=>{setGpu(null);setInspect(null);setEnding(null)},[page,session.selected]);
 useEffect(()=>{installMotionTokens();let active=true;native.request('desktop.settings').then(value=>active&&setDesktopPrefs(value)).catch(e=>active&&setError(e.message));const navigate=native.subscribe(command=>{if(command?.target==='taskbar-resource'&&navigation.some(([id])=>id===command.page)&&typeof command.deviceId==='string'){setDialog(false);openDevicePage(command.deviceId,command.page);return;}setSettingsTab(command==='about'?'about':command==='taskbar-settings'?'taskbar':'appearance');setDialog(true)},'navigate');native.request('app.info').then(value=>active&&setInfo(value)).catch(e=>active&&setError(e.message));return()=>{active=false;navigate()}},[]);
 useEffect(()=>{let alive=true;Promise.resolve(document.fonts?.ready).then(()=>native.request('app.uiReady')).then(()=>{if(!alive)return;requestAnimationFrame(()=>{document.getElementById('launch-shell')?.classList.add('complete');setTimeout(()=>document.getElementById('launch-shell')?.remove(),280)})}).catch(e=>setError(e.message));return()=>{alive=false}},[]);
 useEffect(()=>{const resolved=resolveAppearance(settings,systemDark,settings.iconThemeId||globalThis.__ASTER_ICON);applyAppearance(resolved);document.documentElement.dataset.motion=settings.reduced?'reduced':'full';applyNumberMotion(settings.numberMotion);localStorage.setItem('asterhub-preview-appearance',JSON.stringify(settings));let current=true;native.request('window.appearance',nativeAppearance(resolved,settings.windowGlass)).then(result=>{if(current)document.documentElement.dataset.windowGlass=result?.glass?'on':'off'}).catch(e=>{if(current)setError(e.message)});return()=>{current=false}},[settings,systemDark]);
 useEffect(()=>{if(desktopPrefs){document.documentElement.dataset.density=desktopPrefs.density}},[desktopPrefs]);
 useEffect(()=>{if(page==='terminal')setTerminalVisited(true);if(page==='files')setFilesVisited(true);if(page==='storage')setStorageVisited(true)},[page]);
 const label=navigation.find(n=>n[0]===page)?.[1]||({terminal:'终端',files:'文件'})[page],cores=sample?.cores||[],device={gpuCount:sample?.gpus.length||0};
 return <NumericUpdateProvider intervalMs={activeDevice?.isLocal?(sample.light?.intervalMs??desktopPrefs?.localIntervalMs??2000):(sample.interval||3)*1000}><div className={"app-shell native-workbench preview5-workbench"+(collapsed?" sidebar-collapsed":"")}><aside className="sidebar"><div className="brand"><div className="brand-mark"><BrandMark/></div><div className="sidebar-brand-name"><strong>AsterHub</strong></div><IconButton className="sidebar-collapse-toggle" icon={collapsed?PanelLeftOpen:PanelLeftClose} label={tr(collapsed?'展开侧栏':'折叠侧栏')} aria-expanded={!collapsed} onClick={toggleSidebar}/></div><DeviceSwitcher session={session} collapsed={collapsed} onAdd={()=>setDeviceDialog({})} onEdit={setDeviceDialog}/><div className="sidebar-section-label">{t("资源监控")}</div><nav>{tr(navigation.map(([id,name,Icon])=><button className={'nav-item '+(page===id?'active':'')} key={id} title={tr(collapsed?name:undefined)} aria-label={tr(name)} onClick={()=>setPage(id)}><Icon size={17} data-functional-icon={id}/><span>{tr(name)}</span></button>))}</nav><div className="sidebar-section-label">{t("工作空间")}</div><nav>{tr([[Terminal,'终端','terminal'],[FolderOpen,'文件','files']].map(([Icon,name,id])=><button key={name} title={tr(collapsed?name:undefined)} aria-label={tr(name)} className={'nav-item '+(page===id?'active':'')} onClick={()=>setPage(id)}><Icon size={17} data-functional-icon={id}/><span>{tr(name)}</span></button>))}</nav><div className="sidebar-bottom"><TaskCenter devices={session.devices} onOpenStorage={id=>{storageTaskFocus.request(id);openDevicePage(id,'storage')}} onOpenUpdates={()=>{setSettingsTab('about');setDialog(true)}}/><button className="nav-item" title={tr(collapsed?"偏好设置":undefined)} aria-label={t("偏好设置")} onClick={()=>{setSettingsTab('appearance');setDialog(true)}}><Settings2 size={17} data-functional-icon="appearance"/><span>{t("偏好设置")}</span></button><button title={tr(collapsed?"关于 AsterHub":undefined)} aria-label={t("关于 AsterHub")} className="nav-item native-about-entry" onClick={()=>{setSettingsTab('about');setDialog(true)}}><Info size={17} data-functional-icon="about"/><span>{t("关于 AsterHub")}<small>{tr(info?.version||info?.stage||'Windows 正式版')}</small></span></button></div></aside>
 <div className="workspace"><header className="topbar"><div className="topbar-left"><Monitor size={17}/><span className="top-device">{activeDevice?.isLocal?t('本机'):activeDevice?.name||t('设备')}</span><ChevronRight size={14}/><span className="top-page">{tr(label)}</span>{tr(page==='storage'&&activeStorageTool&&<><ChevronRight size={14}/><span className="top-tool">{tr(activeStorageTool==='analysis'?'磁盘空间分析':'大文件清理')}</span></>)}{tr(page==='memory'&&session.selected==='local'&&activeMemoryTool!=='monitor'&&<><ChevronRight size={14}/><span className="top-tool">{tr(activeMemoryTool==='analysis'?'内存分析':activeMemoryTool==='cleanup'?'内存清理':'自动清理')}</span></>)}</div><div className="topbar-right"><span>{tr(sample.time?new Date(sample.time).toLocaleTimeString(locale(),{hour12:false}):'正在采样')}</span><span className="topbar-separator"/><ConnectionStatus row={session.row}/></div></header>
 <main onScroll={()=>scrollMemory.current?.onScroll()} ref={main} className="main-content" data-page={page} data-reading-state={['cpu','memory'].includes(page)&&sample.light?(sample.light.stale?'stale':'live'):session.stale?'stale':session.hasSample?'live':'loading'}><PageBoundary key={page+session.selected}>{tr(error&&<p className="native-error" role="alert">{tr(error)}</p>)}{tr(session.connectionError&&<div className="native-connection-notice" role="status"><Activity size={17}/><div><strong>{tr(session.connectionError)}</strong><span>{tr(session.hasSample?'保留上次读数 · '+new Date(session.lastSuccess).toLocaleTimeString(locale(),{hour12:false})+' · 恢复连接后继续更新':'等待连接成功后显示读数')}</span></div></div>)}
 <div hidden={page==='terminal'||page==='files'||page==='storage'}>{tr(page==='history'?<HistoryView key={session.selected} deviceId={session.selected}/>:!sample?<Empty icon={Activity} title={tr('正在读取'+(activeDevice?.name||'设备')+'状态')} description={t("等待第一个真实采样，不填充演示数据。")}/>:<>
 <div className="page-heading"><div><div className="eyebrow">{tr(activeDevice?.isLocal?'本机':'SSH')} · {activeDevice?.host}</div><h1>{tr(label)}</h1>{tr(page==='cpu'&&sample.hardware?.CpuModel&&<p>{tr(sample.hardware.CpuModel)}</p>)}{page==='gpu'&&sample.gpus.length===1&&sample.gpus[0].name&&<p title={sample.gpus[0].name}>{sample.gpus[0].name}</p>}</div></div>
 {tr(page==='overview'&&<><OverviewCards key={session.selected} sample={sample} onInspect={setInspect}/><HardwareInventory sample={sample}/></>)}
 {tr(page==='gpu'&&<AnalysisWorkspace key={session.selected} metric="gpu" deviceId={session.selected}>{(tr(sample.gpus.length?<GpuWorkspace key={session.selected} sample={sample} samples={samples} device={device} style={settings.displayStyle} onInspect={(id,anchor)=>setGpu({id,anchor})} onEnd={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} onTerminal={()=>setPage('terminal')}/>:<Empty icon={Microchip} title={tr(session.hasSample?"未检测到独立显卡":"正在获取显卡信息")} description={tr(session.hasSample?"CPU、内存、网络与存储仍持续记录。":"连接成功后显示显卡型号与读数。")}/>))}</AnalysisWorkspace>)}
 {tr(page==='cpu'&&<AnalysisWorkspace key={session.selected} metric="cpu" deviceId={session.selected}><ComputeTrend metric="cpu" samples={cpuSamples} value={sample.cpu}/><section className="core-section"><SectionTitle meta={(session.hasSample?cores.length:'—')+' 个逻辑处理器'}>{t("逐核占用")}</SectionTitle><div className="core-grid">{tr(cores.map(c=><div className="core-cell" key={c.Index} style={{backgroundColor:'color-mix(in srgb,var(--blue) '+Math.max(8,c.Util*.65)+'%,var(--surface))'}}><span>CPU {tr(c.Index)}</span><strong>{tr(Number.isFinite(c.Util)?c.Util.toFixed(0)+'%':'—')}</strong></div>))}</div></section></AnalysisWorkspace>)}
 {tr(page==='memory'&&<MemoryWorkspace key={session.selected} deviceId={session.selected} onToolChange={handleMemoryToolChange}><div className="detail-summary"><div><span>{t("已使用")}</span><strong><Reading value={sample.memUsed} unit=" GiB" stale={sample.light?.stale??sample.stale}/></strong></div><div><span>{t("总容量")}</span><strong><Reading value={sample.memTotal} unit=" GiB" stale={sample.light?.stale??sample.stale}/></strong></div><div><span>{t("使用率")}</span><strong><Reading value={sample.memory} stale={sample.light?.stale??sample.stale}/></strong></div></div><section className="resource-chart"><SectionTitle>{t("内存使用")}</SectionTitle><TimeChart samples={memorySamples} metric="memory"/></section><MemoryDetails sample={sample}/></MemoryWorkspace>)}
 {tr(page==='network'&&<NetworkWorkspace deviceId={session.selected} sample={sample} samples={samples}/>)}
 {tr(page==='storage'&&<StoragePage deviceId={session.selected} sample={sample} samples={samples}/>)}
 </>)}
 </div></PageBoundary><PageBoundary key="terminal-workspace">{tr(storageVisited&&<StorageTools key={session.selected} deviceId={session.selected} device={activeDevice} sample={sample} samples={samples} settings={settings} active={page==='storage'} onToolChange={handleStorageToolChange}/>)}{tr(terminalVisited&&<TerminalPage deviceId={session.selected} settings={settings} active={page==='terminal'}/>)}{tr(filesVisited&&<div hidden={page!=='files'} className="retained-files-page"><FilesPage deviceId={session.selected} devices={session.devices} active={page==='files'}/></div>)}</PageBoundary></main><ResourceFooter key={session.selected} page={page} onSelect={setPage} sample={sample} samples={samples} cpuSamples={cpuSamples} memorySamples={memorySamples}/></div>

 {tr(gpu!==null&&sample?.gpus.length>1&&sample.gpus.some(g=>g.id===gpu.id)&&<GpuInspector deviceId={session.selected} gpu={{...sample.gpus.find(g=>g.id===gpu.id),series:[]}} anchor={gpu.anchor} pulse={0} processes={sample.processes} liveSamples={samples} onClose={()=>setGpu(null)} onEndProcess={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} confirmationOpen={!!ending}/>)}
 {tr(inspect&&sample&&<ResourceInspector deviceId={session.selected} key={inspect.id} {...inspect} sample={sample} samples={samples} lightSamples={lightSamples} onClose={()=>setInspect(null)} onEndProcess={session.stale?null:p=>setEnding({process:p,deviceId:session.selected})} confirmationOpen={!!ending} onOpenPage={id=>{setInspect(null);setPage(id)}}/>)}
 {tr(ending&&<EndProcessDialog {...ending} onClose={()=>setEnding(null)}/>)}
 {tr(deviceDialog&&<DeviceDialog device={deviceDialog.id?deviceDialog:null} onClose={()=>setDeviceDialog(null)} onSaved={session.refresh}/>)}
 <StartupUpdatePrompt/>
 {tr(dialog&&<Preferences settings={settings} info={info} initialTab={settingsTab} onClose={()=>setDialog(false)} onApplied={(next,interval,desktop)=>{setSettings(next);setDesktopPrefs(desktop);setInfo(s=>({...s,interval}))}}/>)}

 </div></NumericUpdateProvider>;
}
