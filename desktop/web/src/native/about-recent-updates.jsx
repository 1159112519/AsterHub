import {t,tr,useLocale} from '../i18n/index.js';
import React,{useEffect} from 'react';
import {createPortal} from 'react-dom';
import {Modal} from '../components';
import {ReleaseLink,versionReleaseUrl,repositoryUrl} from './about-links';
import releases from './about-releases.json';

const groups=[['added','新增'],['improved','改进'],['fixed','修复']];
export function RecentUpdates({currentVersion,onClose}){useLocale();
 // The parent preferences dialog also listens on document. Capture Escape here
 // so dismissing this portal cannot close the preferences underneath it.
 useEffect(()=>{function escape(event){if(event.key==='Escape'){event.preventDefault();event.stopImmediatePropagation();onClose()}}document.addEventListener('keydown',escape,true);return()=>document.removeEventListener('keydown',escape,true)},[onClose]);
 return createPortal(<div className="about-recent-layer"><Modal title={t("最近更新")} className="about-recent-modal" onClose={onClose}><div className="about-recent-intro"><p>{t("最近三个版本的更新记录")}</p><ReleaseLink href={repositoryUrl+'/releases'} label={t("全部发布记录")}>{t("全部发布记录")}</ReleaseLink></div><div className="about-recent-body">{tr(releases.slice(0,3).map(release=><article className="about-release" key={release.version}><header><div><h3>{tr(release.version)}</h3><time dateTime={release.date}>{tr(release.date)}</time></div>{tr(currentVersion?.replace(/^v/,'')===release.version&&<span className="about-current-badge">{t("当前版本")}</span>)}</header>{tr(groups.map(([key,title])=>release[key].length>0&&<section className={'about-release-group '+key} key={key}><h4>{tr(title)}</h4><ul>{tr(release[key].map((item,index)=><li key={index}>{tr(item)}</li>))}</ul></section>))}<ReleaseLink href={versionReleaseUrl(release.version)} label={tr(release.version+'发布页')}>{t("在 GitHub 查看此版本")}</ReleaseLink></article>))}</div><p className="about-recent-source">{t("内容来自随应用提供的更新日志；外部发布页以 GitHub 实际记录为准。")}</p></Modal></div>,document.body)
}
