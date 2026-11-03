import React from 'react';
import './tool-navigation.css';
// One resource, one row of tools. Device/page state stays with the calling workspace.
export function ToolNavigation({label,items,value,onChange}){return <nav className="foundation-tool-navigation" aria-label={label}>{items.map(([id,name])=><button type="button" key={id} aria-current={value===id?'page':undefined} onClick={()=>onChange(id)}>{name}</button>)}</nav>}
