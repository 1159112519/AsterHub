import React from 'react';
import {Clock,X,ChevronRight,Layers,UserRound} from 'lucide-react';
import {ScrollSurface} from './scroll-surface';
export function HistoryPeek({date,time,metric,value,rows,gpuCount,onClose,onViewProcesses}){
 const total=rows.reduce((sum,p)=>sum+p.memory,0);
 return <section className="history-peek" aria-label="所选时间快照">
  <header><Clock size={14}/><time>{date} {time}</time><button className="history-peek-close" type="button" aria-label="关闭时间快照" onClick={onClose}><X size={15}/></button></header>
  <div className="history-peek-reading"><span>{metric==='内存'?'内存使用':metric+' 利用率'}</span><strong>{value.toFixed(1)}<small>%</small></strong></div>
  {gpuCount>0?<>
   <div className="history-peek-list-heading"><h3>当时的进程 <span>{rows.length}</span></h3><span>显存</span></div>
   <ScrollSurface className="history-peek-scroll" resetKey={date+time}>
    {rows.length?<ul className="history-peek-processes" aria-label="该时间点的 GPU 进程">{rows.map(p=><li key={p.gpu+':'+p.pid}>
     <div className="history-peek-process-main"><strong title={p.path||p.name}>{p.name}</strong><span>{p.memory==null?'—':p.memory.toFixed(2)} <small>GiB</small></span></div>
     <div className="history-peek-process-meta"><span>GPU {p.gpu}</span><span className="history-peek-user" title={p.userFull||p.user}><UserRound size={11}/><span>{p.user}</span></span><span>PID {p.pid}</span></div>
    </li>)}</ul>:<p className="history-peek-empty">此时没有已识别的 GPU 进程</p>}
   </ScrollSurface>
  </>:<p className="history-peek-empty">此设备没有独立显卡</p>}
  <footer><span><Layers size={12}/>{gpuCount?'GPU 进程 '+rows.length:'CPU 与内存'}{rows.length>0&&rows.every(p=>p.memory!=null)&&<small>· {total.toFixed(2)} GiB</small>}</span>{gpuCount>0&&<button type="button" onClick={onViewProcesses}>完整列表<ChevronRight size={13}/></button>}</footer>
 </section>;
}
