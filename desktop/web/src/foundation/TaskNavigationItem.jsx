import {t,tr,useLocale} from '../i18n/index.js';
import React from 'react';
import './task-navigation.css';
// All background tasks share one unambiguous sidebar row; secondary data is isolated.
export function TaskNavigationItem({icon:Icon,label,detail,title,onClick,className='',id,expanded}){useLocale();return <button type="button" id={id} aria-expanded={expanded} aria-haspopup={expanded!=null?"dialog":undefined} aria-controls={expanded?id+"-panel":undefined} className={'nav-item foundation-task-navigation '+className} aria-label={tr(title||label)} title={tr(title||label)} onClick={onClick}><Icon size={19} aria-hidden="true"/><span className="task-navigation-label">{tr(label)}</span>{tr(detail!=null&&<small className="task-navigation-detail">{tr(detail)}</small>)}</button>}
