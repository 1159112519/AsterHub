import {t,tr,useLocale} from '../i18n/index.js';
import React,{useState,useId} from 'react';
import {Button,FormDialog} from '../components';
import {ThemedSelect} from '../themed-select';
import {parseStorageSize,storageSizePresets,storageSizeUnits} from './storage-size-filter';

export function StorageSizeFilter({minimum,customSize,onChange}){useLocale();
 const [draft,setDraft]=useState(null),labelId=useId();
 const known=storageSizePresets.some(p=>p.bytes===minimum);
 const selectedCustom=!known&&customSize?.bytes===minimum;
 const parsed=draft&&parseStorageSize(draft.amount,draft.unit);
 return <>
  <ThemedSelect aria-label={t("文件大小")} value={selectedCustom?'saved':minimum} onChange={e=>{
   const v=e.target.value;
   if(v==='custom')setDraft({amount:customSize?.amount??'',unit:customSize?.unit??'GiB'});
   else if(v!=='saved')onChange(Number(v));
  }}>
   {tr(storageSizePresets.map(({bytes,label})=><option key={bytes} value={bytes}>{tr(label)}</option>))}
   {tr(selectedCustom&&<option value="saved">{t("大于 ")}{tr(customSize.amount)} {tr(customSize.unit)}</option>)}
   <option value="custom">{t("自定义…")}</option>
  </ThemedSelect>
  {tr(draft&&<FormDialog title={t("自定义文件大小")} className="storage-size-dialog" onClose={()=>setDraft(null)}>
   <form onSubmit={e=>{e.preventDefault();if(parsed){onChange(parsed.bytes,parsed);setDraft(null)}}}>
    <div className="storage-size-dialog-row">
     <label className="field" htmlFor={labelId}>{t("大于")}<input id={labelId} type="text" inputMode="decimal" maxLength={22} autoComplete="off" value={draft.amount} placeholder={t("例如 2.5")} onChange={e=>setDraft(d=>({...d,amount:e.target.value}))}/></label>
     <label className="field">{t("单位")}<ThemedSelect aria-label={t("大小单位")} compact value={draft.unit} onChange={e=>setDraft(d=>({...d,unit:e.target.value}))}>{tr(Object.keys(storageSizeUnits).map(u=><option key={u} value={u}>{tr(u)}</option>))}</ThemedSelect></label>
    </div>
    {tr(draft.amount.trim()&&!parsed&&<p className="native-error" role="status">{t("请输入有效的正数。")}</p>)}
    <div className="dialog-actions"><Button onClick={()=>setDraft(null)}>{t("取消")}</Button><Button type="submit" tone="primary" disabled={!parsed}>{t("应用筛选")}</Button></div>
   </form>
  </FormDialog>)}
 </>;
}
