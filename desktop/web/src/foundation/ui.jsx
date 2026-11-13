import {t,tr,useLocale} from '../i18n/index.js';
import React from 'react';
import {LoaderCircle} from 'lucide-react';
export function Button({children, icon: Icon, tone = '', className = '', busy = false, busyLabel, disabled, ...props}) {useLocale();
  const Glyph = busy ? LoaderCircle : Icon;
  return <button type="button" {...props} disabled={disabled || busy} aria-busy={busy || undefined} className={`button ${tone} ${className}`}>
    {tr(Glyph && <Glyph size={16} className={busy ? 'foundation-spinner' : undefined} aria-hidden="true"/>)}
    <span className="button-content">{tr(busy && busyLabel ? busyLabel : children)}</span>
  </button>;
}
export function IconButton({icon: Icon, label, onClick, className = '', ...props}) {useLocale();
  return <button type="button" aria-label={tr(label)} title={tr(label)} className={`icon-button ${className}`} onClick={onClick} {...props}><Icon size={18} aria-hidden="true"/></button>;
}
export function Toolbar({children, label, align = 'start', className = '', ...props}) {useLocale();
  return <div {...props} role="group" aria-label={tr(label)} data-align={align} className={`foundation-toolbar ${className}`}>{tr(children)}</div>;
}

// Monitoring always precedes optional tools; task status never displaces it.
export function MonitoringLayout({heading, monitoring, tools, task, monitorLabel="资源监控", toolsLabel="工具"}) {useLocale();
  return <>{tr(heading)}<section className="foundation-monitoring" aria-label={tr(monitorLabel)}>{tr(monitoring)}</section>
    {tr(task)}<section className="foundation-tools" aria-label={tr(toolsLabel)}><h2>{tr(toolsLabel)}</h2>{tr(tools)}</section></>;
}
export function ToolLauncher({icon: Icon, title, description, onClick}) {useLocale();
  return <button type="button" className="foundation-tool-launcher" onClick={onClick}><Icon size={27} aria-hidden="true"/>
    <span><strong>{tr(title)}</strong><small>{tr(description)}</small></span><span className="foundation-tool-arrow" aria-hidden="true">›</span></button>;
}
export function DialogContent({children}) {useLocale();
  const items=React.Children.toArray(children),isFooter=child=>React.isValidElement(child)&&child.props.className?.split(' ').includes('dialog-actions');
  return <><div className="foundation-dialog-body">{tr(items.filter(child=>!isFooter(child)))}</div>{tr(items.filter(isFooter))}</>;
}
