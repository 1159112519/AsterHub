import {t,tr,useLocale} from './i18n/index.js';
import {ProcessPath} from './ProcessPath';
import React from 'react';
import {Clock,X,ChevronRight,Layers,UserRound} from 'lucide-react';
import {ScrollSurface} from './scroll-surface';
export function HistoryPeek({date,time,metric,value,rows,gpuCount,error='',onClose,onViewProcesses}){useLocale();
 const total=rows.reduce((sum,p)=>sum+p.memory,0);
 return <section className="history-peek" aria-label={t("所选时间快照")}>
  <header><Clock size={14}/><time>{tr(date)} {tr(time)}</time><button className="history-peek-close" type="button" aria-label={t("关闭时间快照")} onClick={onClose}><X size={15}/></button></header>
  <div className="history-peek-reading"><span>{tr(metric==='内存'?'内存使用':metric+' 利用率')}</span><strong>{tr(value.toFixed(1))}<small>%</small></strong></div>
  {tr(error?<p role="alert" className="history-peek-empty">{t("这个时间点暂时无法读取，请重新选择。")}</p>:gpuCount>0?<>
   <div className="history-peek-list-heading"><h3>{t("当时的进程 ")}<span>{tr(rows.length)}</span></h3><span>{t("显存")}</span></div>
   <ScrollSurface className="history-peek-scroll" resetKey={date+time}>
    {tr(rows.length?<ul className="history-peek-processes" aria-label={t("该时间点的 GPU 进程")}>{tr(rows.map(p=><li key={p.gpu+':'+p.pid}>
     <div className="history-peek-process-main"><ProcessPath process={p}/><span>{tr(p.memory==null?'—':p.memory.toFixed(2))} <small>GiB</small></span></div>
     <div className="history-peek-process-meta"><span>GPU {tr(p.gpu)}</span><span className="history-peek-user" title={tr(p.userFull||p.user)}><UserRound size={11}/><span>{p.user}</span></span><span>PID {tr(p.pid)}</span></div>
    </li>))}</ul>:<p className="history-peek-empty">{t("此时没有已识别的 GPU 进程")}</p>)}
   </ScrollSurface>
  </>:<p className="history-peek-empty">{t("此设备没有独立显卡")}</p>)}
  <footer><span><Layers size={12}/>{tr(error?'读取失败':gpuCount?'GPU 进程 '+rows.length:'CPU 与内存')}{tr(rows.length>0&&rows.every(p=>p.memory!=null)&&<small>· {tr(total.toFixed(2))} GiB</small>)}</span>{tr(!error&&gpuCount>0&&<button type="button" onClick={onViewProcesses}><span className="control-label">{t("完整列表")}</span><ChevronRight size={13}/></button>)}</footer>
 </section>;
}
