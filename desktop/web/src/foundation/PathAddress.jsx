import React,{useLayoutEffect,useRef,useState} from 'react';
import {ChevronRight,Pencil,ArrowRight} from 'lucide-react';
import {IconButton} from '../components';
import {t,useLocale} from '../i18n/index.js';
import {pathCrumbs} from './path-address.mjs';
import './path-address.css';
export function PathAddress({path='',crumbs,onNavigate,label,disabled=false,before,after,className='',navClassName=''}){useLocale();
 const [editing,setEditing]=useState(false),[draft,setDraft]=useState(path),input=useRef(null),nav=useRef(null);
 const entries=crumbs||pathCrumbs(path);
 useLayoutEffect(()=>{if(editing){input.current?.focus({preventScroll:true});input.current?.select();}else if(nav.current)nav.current.scrollLeft=nav.current.scrollWidth;},[editing,path]);
 function edit(){setDraft(path);setEditing(true);}
 async function go(event){event.preventDefault();if(!draft.trim()||disabled)return;const result=await onNavigate(draft.trim());if(result!==false)setEditing(false);}
 return <div className={'path-address '+className}>
  {before}
  {editing?<form onSubmit={go} className="path-address-form"><input ref={input} aria-label={label} value={draft} onChange={e=>setDraft(e.target.value)} onKeyDown={e=>{if(e.key==='Escape'){e.preventDefault();setEditing(false);}}} spellCheck="false"/><IconButton icon={ArrowRight} type="submit" label={t('打开此路径')} disabled={disabled||!draft.trim()}/></form>:<>
   <nav ref={nav} className={'path-address-crumbs '+navClassName} aria-label={label} onDoubleClick={edit}>{entries.map((entry,index)=><React.Fragment key={entry.id??entry.path??index}>{index>0&&<ChevronRight size={13} aria-hidden="true"/>}<button type="button" disabled={disabled} title={entry.path||path} aria-current={index===entries.length-1?'location':undefined} onClick={()=>onNavigate(crumbs?entry:entry.path)}><span className="control-label path-address-label">{entry.name}</span></button></React.Fragment>)}<span className="path-address-space" onClick={edit}/></nav>
   <IconButton icon={Pencil} label={t('编辑路径')} onClick={edit} disabled={disabled}/>
  </>}
  {after}
 </div>;
}
