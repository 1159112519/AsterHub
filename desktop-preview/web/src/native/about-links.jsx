import React,{useState} from 'react';
import {openExternal} from './desktop-actions';
import {ExternalLink} from 'lucide-react';
import {native} from './bridge';
export const repositoryUrl='https://github.com/1159112519/AsterHub';
export function versionReleaseUrl(version){return version?`${repositoryUrl}/releases/tag/${encodeURIComponent('v'+version.replace(/^v/,''))}`:`${repositoryUrl}/releases`}
export function ReleaseLink({href,children,label}){
 const [error,setError]=useState('');
 return <div className="about-external-link"><a href={href} target="_blank" rel="noopener noreferrer" aria-label={label} onClick={event=>{if(native.available){event.preventDefault();setError('');openExternal(href).catch(e=>setError(e.message))}}}>{children}<ExternalLink size={13}/></a>{error&&<span role="alert">{error}</span>}</div>
}
