import {t,tr,useLocale} from '../i18n/index.js';
import React,{useState} from 'react';
import {Check,Monitor} from 'lucide-react';
import {terminalAnsiKeys,terminalSchemes,selectTerminalScheme,terminalSchemeTheme,terminalTheme,terminalIsLight} from './terminal-themes';
import './terminal-schemes.css';

export function TerminalSchemePreview({theme,fontFamily,compact=false}){useLocale();
 return <span className="terminal-scheme-sample" data-compact={compact} aria-hidden="true" style={{background:theme.background,color:theme.foreground,fontFamily:fontFamily?`${fontFamily}, Consolas, monospace`:undefined}}>
  <span><b style={{color:theme.green}}>~</b><b style={{color:theme.cyan}}> ❯</b>{tr(' ls')}<i className="terminal-scheme-cursor" style={{background:theme.cursor,color:theme.cursorAccent}}/></span>
  <span>{tr(compact?'rw  ':'drwx  ')}<b style={{color:theme.blue}}>src/</b>{tr(compact?' ':'  ')}<b style={{color:theme.yellow}}>log</b>{tr(!compact&&<>{tr('  ')}<b style={{color:theme.magenta}}>notes</b>{tr('  ')}<b style={{color:theme.red}}>stderr</b></>)}</span>
  {tr(!compact&&<span style={{color:theme.selectionForeground,background:theme.selectionBackground}}>selected</span>)}
 </span>;
}
const views=[['all','全部'],['dark','深色'],['light','浅色']];
export function TerminalSchemePicker({settings={},onChange}){useLocale();
 const [view,setView]=useState('all');
 const requested=settings.terminalTheme||'follow',current=terminalTheme(settings);
 const activeId=requested;
 const items=terminalSchemes.map(({id},index)=>({id,index,theme:terminalSchemeTheme(id,settings)}));
 const schemes=items.filter(({id,theme})=>id!=='follow'&&(view==='all'||(view==='light')===terminalIsLight(theme)));
 return <section className="terminal-scheme-picker" aria-label={t("终端配色方案")}>
  <div className="terminal-scheme-preview"><TerminalSchemePreview theme={current} fontFamily={settings.terminalFont}/></div>
  <div className="terminal-scheme-tools"><div className="terminal-scheme-filters" role="group" aria-label={t("配色明暗")}>{tr(views.map(([id,label])=><button type="button" key={id} aria-pressed={id===view} onClick={()=>setView(id)}>{tr(label)}</button>))}</div>
   <button type="button" className="terminal-scheme-follow" aria-pressed={activeId==='follow'} onClick={()=>onChange(selectTerminalScheme(settings,'follow'))}><Monitor size={14}/>{t("跟随界面")}</button>
  </div>
  <div className="terminal-scheme-list" role="region" aria-label={t("可选配色方案")} tabIndex={0}>
   {tr(schemes.map(({id,theme,index})=>{const selected=activeId===id;return <button key={id} type="button" className="terminal-scheme-card" data-preset={id} aria-pressed={selected} aria-label={tr(`配色 ${index}`)} onClick={()=>onChange(selectTerminalScheme(settings,id))}>
    <TerminalSchemePreview theme={theme} fontFamily={settings.terminalFont} compact/><span className="terminal-scheme-swatches" aria-hidden="true">{tr(terminalAnsiKeys.slice(1,7).map(key=><i key={key} style={{background:theme[key]}}/>))}</span>{tr(selected&&<Check className="terminal-scheme-check" size={13} aria-hidden="true"/>)}
   </button>}))}
  </div>
 </section>;
}
