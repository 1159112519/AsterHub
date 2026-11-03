import React,{useEffect,useState} from 'react';
import {FolderOpen,ArrowUp,ChevronLeft,ChevronRight} from 'lucide-react';
import {Button} from '../components';
import {native} from './bridge';
export function StoragePathPicker({deviceId,initialPath,onChoose}) {
 const [path,setPath]=useState(initialPath||''),[offset,setOffset]=useState(0),[data,setData]=useState(null),[error,setError]=useState(''),[busy,setBusy]=useState(false);
 useEffect(()=>{const controller=new AbortController();setBusy(true);setError('');native.request('storage.directories',{deviceId,path,offset},{signal:controller.signal,timeout:30000}).then(setData).catch(e=>{if(e.name!=='AbortError')setError(e.message)}).finally(()=>{if(!controller.signal.aborted)setBusy(false)});return()=>controller.abort()},[deviceId,path,offset]);
 function go(next){setOffset(0);setPath(next)}
 return <section className="storage-path-picker" aria-label="选择扫描文件夹"><header><Button aria-label="上一级文件夹" disabled={busy||!data||data.parent===data.path} onClick={()=>go(data.parent)} icon={ArrowUp}/><span title={data?.path}>{data?.path||'正在读取文件夹…'}</span><Button disabled={busy||!data} onClick={()=>onChoose(data.path)}>选择此文件夹</Button></header>{error&&<p role="alert">{error}</p>}<div className="storage-path-folders" aria-busy={busy}>{busy?<p>正在读取文件夹…</p>:data?.items.map(n=><button key={n.path} onClick={()=>go(n.path)}><FolderOpen size={18}/><span>{n.name}</span><ChevronRight size={16}/></button>)}{!busy&&data&&!data.items.length&&<p>此处没有子文件夹，可以选择当前路径。</p>}</div><footer><Button disabled={busy||offset===0} onClick={()=>setOffset(v=>Math.max(0,v-200))} icon={ChevronLeft}>上一页</Button><Button disabled={busy||!data?.more} onClick={()=>setOffset(v=>v+200)} icon={ChevronRight}>下一页</Button></footer></section>
}
