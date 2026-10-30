import React,{useEffect} from 'react';
import {createPortal} from 'react-dom';
import {Modal} from '../components';
import {ReleaseLink,versionReleaseUrl,repositoryUrl} from './about-links';
import releases from './about-releases.json';

const groups=[['added','新增'],['improved','改进'],['fixed','修复']];
export function RecentUpdates({currentVersion,onClose}){
 // The parent preferences dialog also listens on document. Capture Escape here
 // so dismissing this portal cannot close the preferences underneath it.
 useEffect(()=>{function escape(event){if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();onClose()}}document.addEventListener('keydown',escape,true);return()=>document.removeEventListener('keydown',escape,true)},[onClose]);
 return createPortal(<div className="about-recent-layer"><Modal title="最近更新" className="about-recent-modal" onClose={onClose}><div className="about-recent-intro"><p>最近三个版本的更新记录</p><ReleaseLink href={repositoryUrl+'/releases'} label="全部发布记录">全部发布记录</ReleaseLink></div><div className="about-recent-body">{releases.map(release=><article className="about-release" key={release.version}><header><div><h3>{release.version}</h3><time dateTime={release.date}>{release.date}</time></div>{currentVersion?.replace(/^v/,'')===release.version&&<span className="about-current-badge">当前版本</span>}</header>{groups.map(([key,title])=>release[key].length>0&&<section className={'about-release-group '+key} key={key}><h4>{title}</h4><ul>{release[key].map((item,index)=><li key={index}>{item}</li>)}</ul></section>)}<ReleaseLink href={versionReleaseUrl(release.version)} label={release.version+'发布页'}>在 GitHub 查看此版本</ReleaseLink></article>)}</div><p className="about-recent-source">内容来自随应用提供的更新日志；外部发布页以 GitHub 实际记录为准。</p></Modal></div>,document.body)
}

