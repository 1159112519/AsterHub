import React from 'react';
import {ExternalLink} from 'lucide-react';
import {native} from './bridge';
export const repositoryUrl='https://github.com/1159112519/AsterHub';
export function versionReleaseUrl(version){return version?`${repositoryUrl}/releases/tag/${encodeURIComponent('v'+version.replace(/^v/,''))}`:`${repositoryUrl}/releases`}
export function ReleaseLink({href,children,label}){
 return <div className="about-external-link" data-unavailable={native.available||undefined} title={native.available?"当前版本暂不能打开外部浏览器":undefined}><a href={href} target="_blank" rel="noopener noreferrer" aria-disabled={native.available||undefined} onClick={event=>{if(native.available){event.preventDefault()}}}>{children}<ExternalLink size={13}/></a></div>
}
