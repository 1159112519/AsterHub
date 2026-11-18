import React,{useLayoutEffect,useRef} from 'react';
import {t,useLocale} from '../i18n/index.js';
export function SettingsNavigation({value,children}){
 useLocale();const ref=useRef(null);
 useLayoutEffect(()=>{const nav=ref.current;const place=()=>{const item=nav.querySelector('button.active');if(item){nav.style.setProperty('--settings-marker-y',item.offsetTop+'px');nav.style.setProperty('--settings-marker-h',item.offsetHeight+'px');nav.style.setProperty('--settings-marker-w',item.offsetWidth+'px');}};place();const observer=new ResizeObserver(place);observer.observe(nav);for(const item of nav.querySelectorAll('button'))observer.observe(item);return()=>observer.disconnect();},[value]);
 return <nav ref={ref} className="settings-navigation" aria-label={t("设置分类")}><span className="settings-navigation-marker" aria-hidden="true"/>{children}</nav>;
}
