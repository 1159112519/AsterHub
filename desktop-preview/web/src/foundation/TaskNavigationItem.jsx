import React from 'react';
import './task-navigation.css';
// All background tasks share one unambiguous sidebar row; secondary data is isolated.
export function TaskNavigationItem({icon:Icon,label,detail,title,onClick,className=''}){return <button type="button" className={'nav-item foundation-task-navigation '+className} aria-label={title||label} title={title||label} onClick={onClick}><Icon size={19} aria-hidden="true"/><span className="task-navigation-label">{label}</span>{detail!=null&&<small className="task-navigation-detail">{detail}</small>}</button>}
