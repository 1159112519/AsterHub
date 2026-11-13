import {t,tr,useLocale} from '../i18n/index.js';
import {ProcessPath} from '../ProcessPath';
import {ResizableTable} from '../resizable-table';
import React,{useState} from 'react';
import {visibleProcesses} from '../processes';
export function GpuProcessSummary({processes,onEnd}){useLocale();
 const [expanded,setExpanded]=useState({}),rows=visibleProcesses(processes);
 return <section className="aggregate-gpu-processes" aria-label={t("全部 GPU 进程")}><h3>{t("GPU 进程 ")}<span>{tr(rows.length)}{t(" 个")}</span></h3>{tr(rows.length?<div className="aggregate-process-scroll"><ResizableTable storageKey="GpuProcessSummary-2" initialWidths={[270,62,104,112]} columnMinima={[148,48,90,100]}><thead><tr><th>{t("进程 / PID")}</th><th>GPU</th><th>{t("显存")}</th><th>{t("操作")}</th></tr></thead><tbody>{tr(rows.map(p=>{const key=p.gpu+':'+p.pid,command=p.commandLine||p.path;return <React.Fragment key={key}><tr><td className="aggregate-process-identity"><ProcessPath process={p}/><small>PID {tr(p.pid)} · {p.user}</small>{tr(command&&<button type="button" className="process-command-toggle" aria-expanded={!!expanded[key]} onClick={()=>setExpanded(v=>({...v,[key]:!v[key]}))}><span className="control-label">{tr(p.commandLine?'完整命令':'可执行文件')}</span></button>)}</td><td><span>{tr(p.gpu)}</span></td><td><span>{tr(p.memory==null?'—':p.memory.toFixed(2)+' GiB')}</span></td><td><button type="button" className="process-end-button" disabled={!onEnd} onClick={()=>onEnd?.(p)}><span className="control-label">{t("结束进程")}</span></button></td></tr>{tr(expanded[key]&&command&&<tr><td colSpan={4}><pre className="aggregate-process-command">{command}</pre></td></tr>)}</React.Fragment>}))}</tbody></ResizableTable></div>:<p className="native-setting-help">{t("当前没有可识别的 GPU 进程")}</p>)}</section>
}
