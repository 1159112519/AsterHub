import React,{useState} from 'react';
import {ExternalLink} from 'lucide-react';
import {native} from './bridge';
export const repositoryUrl='https://github.com/1159112519/AsterHub';
export function versionReleaseUrl(version){return version?`${repositoryUrl}/releases/tag/${encodeURIComponent('v'+version.replace(/^v/,''))}`:`${repositoryUrl}/releases`}
export function ReleaseLink({href,children,label}){
 const [unavailable,setUnavailable]=useState(false);
 return <div className="about-external-link"><a href={href} target="_blank" rel="noopener noreferrer" aria-disabled={native.available||undefined} onClick={event=>{if(native.available){event.preventDefault();setUnavailable(true)}}}>{children}<ExternalLink size={13}/></a>{unavailable&&<p className="about-copy-fallback" role="status">此链接暂时无法打开。</p>}</div>
}
