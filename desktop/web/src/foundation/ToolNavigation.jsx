import {tr,useLocale} from '../i18n/index.js';
import React from 'react';
import './tool-navigation.css';
// Tools retain their established capsule. Optional actions share its row and wrap as a group.
export function ToolNavigation({label,items,value,onChange,actions}){
 useLocale();
 const navigation=<nav className="foundation-tool-navigation" aria-label={tr(label)}>{items.map(([id,name])=><button type="button" key={id} aria-current={value===id?'page':undefined} onClick={()=>onChange(id)}><span className="tool-label">{tr(name)}</span></button>)}</nav>;
 return actions?<div className="foundation-tool-row">{navigation}<div className="foundation-tool-actions">{actions}</div></div>:navigation;
}
