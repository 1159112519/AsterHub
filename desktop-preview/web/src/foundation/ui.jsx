import React from 'react';
import {LoaderCircle} from 'lucide-react';
export function Button({children, icon: Icon, tone = '', className = '', busy = false, busyLabel, disabled, ...props}) {
  const Glyph = busy ? LoaderCircle : Icon;
  return <button type="button" {...props} disabled={disabled || busy} aria-busy={busy || undefined} className={`button ${tone} ${className}`}>
    {Glyph && <Glyph size={16} className={busy ? 'foundation-spinner' : undefined} aria-hidden="true"/>}
    <span className="button-content">{busy && busyLabel ? busyLabel : children}</span>
  </button>;
}
export function IconButton({icon: Icon, label, onClick, className = '', ...props}) {
  return <button type="button" aria-label={label} title={label} className={`icon-button ${className}`} onClick={onClick} {...props}><Icon size={18} aria-hidden="true"/></button>;
}
export function Toolbar({children, label, align = 'start', className = '', ...props}) {
  return <div {...props} role="group" aria-label={label} data-align={align} className={`foundation-toolbar ${className}`}>{children}</div>;
}

// Monitoring always precedes optional tools; task status never displaces it.
export function MonitoringLayout({heading, monitoring, tools, task, monitorLabel="资源监控", toolsLabel="工具"}) {
  return <>{heading}<section className="foundation-monitoring" aria-label={monitorLabel}>{monitoring}</section>
    {task}<section className="foundation-tools" aria-label={toolsLabel}><h2>{toolsLabel}</h2>{tools}</section></>;
}
export function ToolLauncher({icon: Icon, title, description, onClick}) {
  return <button type="button" className="foundation-tool-launcher" onClick={onClick}><Icon size={27} aria-hidden="true"/>
    <span><strong>{title}</strong><small>{description}</small></span><span className="foundation-tool-arrow" aria-hidden="true">›</span></button>;
}
export function DialogContent({children}) {
  const items=React.Children.toArray(children),isFooter=child=>React.isValidElement(child)&&child.props.className?.split(' ').includes('dialog-actions');
  return <><div className="foundation-dialog-body">{items.filter(child=>!isFooter(child))}</div>{items.filter(isFooter)}</>;
}
