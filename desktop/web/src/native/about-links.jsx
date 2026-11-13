import {t,tr,useLocale} from '../i18n/index.js';
import React,{useState} from 'react';
import {openExternal} from './desktop-actions';
import {ExternalLink} from 'lucide-react';
import {native} from './bridge';
export const repositoryUrl='https://github.com/1159112519/AsterHub';
export function versionReleaseUrl(version){return version?`${repositoryUrl}/releases/tag/${encodeURIComponent('v'+version.replace(/^v/,''))}`:`${repositoryUrl}/releases`}
export function ReleaseLink({href,children,label}){useLocale();
 const [error,setError]=useState('');
 return <div className="about-external-link"><a href={href} target="_blank" rel="noopener noreferrer" aria-label={tr(label)} onClick={event=>{if(native.available){event.preventDefault();setError('');openExternal(href).catch(e=>setError(e.message))}}}>{tr(children)}<ExternalLink size={13}/></a>{tr(error&&<span role="alert">{tr(error)}</span>)}</div>
}
