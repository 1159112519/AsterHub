import React,{useRef,useState,useLayoutEffect} from 'react';
import './scroll-surface.css';

// The frame clips scrollbars to the rounded surface; edge shades show remaining content.
export function ScrollSurface({children,className='',contentClassName='',resetKey}){
 const scroll=useRef(null),body=useRef(null),[edges,setEdges]=useState({up:false,down:false});
 function measure(){const el=scroll.current;if(!el)return;const next={up:el.scrollTop>2,down:el.scrollTop+el.clientHeight<el.scrollHeight-2};setEdges(old=>old.up===next.up&&old.down===next.down?old:next)}
 useLayoutEffect(()=>{if(scroll.current)scroll.current.scrollTop=0;measure()},[resetKey]);
 useLayoutEffect(()=>{const observer=new ResizeObserver(measure);if(scroll.current)observer.observe(scroll.current);if(body.current)observer.observe(body.current);measure();return()=>observer.disconnect()},[]);
 return <div className={`scroll-surface ${className}`} data-scroll-up={edges.up} data-scroll-down={edges.down}><div ref={scroll} className={`scroll-surface-scroll ${contentClassName}`} onScroll={measure}><div className="scroll-surface-body" ref={body}>{children}</div></div><span className="scroll-edge top" aria-hidden="true"/><span className="scroll-edge bottom" aria-hidden="true"/></div>
}
