import {t,tr,useLocale} from '../i18n/index.js';
import React from 'react';
import {legacyIds,skinPacks,resolveAppearance} from '../appearance/resolve-appearance';
import {SkinGlyph} from '../appearance/SkinGlyph';
import {Check,ChevronDown,RotateCw} from 'lucide-react';
import {BrandMark,iconThemes,validIcon} from '../brand';
function chooseWithArrow(e){if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown','Home','End'].includes(e.key))return;e.preventDefault();const buttons=[...e.currentTarget.querySelectorAll('[role=radio]')],i=buttons.indexOf(document.activeElement);const next=e.key==='Home'?0:e.key==='End'?buttons.length-1:(Math.max(0,i)+(['ArrowLeft','ArrowUp'].includes(e.key)?-1:1)+buttons.length)%buttons.length;buttons[next]?.click();buttons[next]?.focus()}
function AppearanceDisclosure({title,className='',children}){useLocale();
 return <details className={'appearance-picker appearance-disclosure '+className}><summary><span>{tr(title)}</span><ChevronDown aria-hidden="true" size={16}/></summary><div className="appearance-disclosure-content">{tr(children)}</div></details>;
}
// Every swatch is a complete independent selection; mode is an internal saved field.
export const skins=[...legacyIds.map(id=>({id,mode:id==='light'?'light':'dark'})),...skinPacks.flatMap(p=>['dark','light'].map(mode=>({id:p.id,mode})))];
export function SkinChoices({value,mode='dark',onChange}){useLocale();
 const resolved=resolveAppearance({theme:value,appearanceMode:mode},typeof matchMedia==='function'?matchMedia('(prefers-color-scheme:dark)').matches:true);
 return <AppearanceDisclosure title={t("全局皮肤")}><div className="skin-choices" role="radiogroup" onKeyDown={chooseWithArrow} aria-label={t("全局皮肤")}>{tr(skins.map(({id,mode},index)=>{
  const selected=resolved.id===id&&resolved.mode===mode;
  return <button key={id+'-'+mode} data-skin={id} data-mode={mode} type="button" role="radio" aria-label={tr('皮肤 '+String(index+1).padStart(2,'0'))} aria-checked={selected} tabIndex={selected?0:-1} className={'skin-choice'+(selected?' selected':'')} onClick={()=>onChange(id,mode)}><SkinGlyph id={id} mode={mode}/>{tr(selected&&<Check size={16} className="skin-selected-check" aria-hidden="true"/>)}</button>
 }))}</div></AppearanceDisclosure>;
}
export function IconChoices({value,onChange}){useLocale();return <AppearanceDisclosure className="icon-picker" title={t("应用图标")}><div className="brand-icon-choices" role="radiogroup" onKeyDown={chooseWithArrow} aria-label={t("应用图标")}>{tr(iconThemes.map(([id,label])=><button type="button" role="radio" aria-label={tr(label)} aria-checked={validIcon(value)===id} tabIndex={validIcon(value)===id?0:-1} className={'brand-icon-choice'+(validIcon(value)===id?' selected':'')} onClick={()=>onChange(id)} key={id}><BrandMark iconId={id}/>{tr(validIcon(value)===id&&<Check size={15} className="choice-check"/>)}</button>))}</div><p className="native-setting-help">{t("图标与皮肤独立设置，应用后同步更换窗口、托盘与本程序快捷方式。")}</p></AppearanceDisclosure>}
export function RestartNotice({desktop,onRestart,busy}){useLocale();return desktop?.restartRequired&&<div className="icon-restart-notice" role="status"><span>{t("图标已保存，下次启动时生效。")}</span><button className="btn" disabled={busy} onClick={onRestart}><RotateCw size={15}/><span className="control-label">{t("立即重启")}</span></button></div>}
