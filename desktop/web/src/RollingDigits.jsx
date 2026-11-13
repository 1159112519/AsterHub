import {t,tr,useLocale} from './i18n/index.js';
import React,{memo,useLayoutEffect,useRef,useSyncExternalStore} from 'react';
import {numberFrames,useNumberMotion} from './number-motion';
import {reducedMotion,subscribeReducedMotion} from './motion';
import './rolling-digits.css';

const Digit=memo(function Digit({glyph,direction,mode,duration}){useLocale();
 const last=useRef(glyph),front=useRef(null),back=useRef(null);
 useLayoutEffect(()=>{
  const before=last.current;last.current=glyph;
  if(before===glyph||mode!=='current'||!front.current?.animate)return;
  back.current.textContent=before;
  const timing={duration,easing:'cubic-bezier(.22,.74,.28,1)'};
  const incoming=front.current.animate([{transform:`translateY(${52*direction}%)`,opacity:.12,filter:'blur(1.35px)'},{transform:'none',opacity:1,filter:'none'}],timing);
  const outgoing=back.current.animate([{transform:'none',opacity:.85,filter:'none'},{transform:`translateY(${-54*direction}%)`,opacity:0,filter:'blur(1.35px)'}],timing);
  return()=>{incoming.cancel();outgoing.cancel()};
 },[glyph,mode,duration]); // Direction describes this glyph's change; unchanged digits keep their existing state.
 return <span className="roll-digit"><span className="roll-front" ref={front}>{tr(glyph)}</span><span className="roll-back" ref={back}/></span>;
});

export function useResolvedNumberMotion(){const preference=useNumberMotion(),reduced=useSyncExternalStore(subscribeReducedMotion,reducedMotion);return reduced?'direct':preference;}
export const RollingDigits=memo(function RollingDigits({text,mode,duration=240}){useLocale();
 const prior=useRef(text),wholeNode=useRef(null),direction=Number(text)<Number(prior.current)?-1:1;
 useLayoutEffect(()=>{
  const before=prior.current;prior.current=text;
  if(before===text||!['fade','slide'].includes(mode)||!wholeNode.current?.animate)return;
  const animation=wholeNode.current.animate(numberFrames(mode,true,6*direction),{duration,easing:'cubic-bezier(.22,.74,.28,1)'});
  return()=>animation.cancel();
 },[text,mode,duration]);
 const [whole,fraction]=text.split('.');
 const cells=(value,prefix,right)=>[...value].map((glyph,i)=>/\d/.test(glyph)?<Digit key={prefix+(right?value.length-i:i)} glyph={glyph} direction={direction} mode={mode} duration={duration}/>:<span key={prefix+'symbol'+i}>{tr(glyph)}</span>);
 return <span className="rolling-number" aria-label={tr(text)} ref={wholeNode}>{tr(mode==='current'?<span className="rolling-glyphs" aria-hidden="true">{tr(cells(whole,'w',true))}{tr(fraction!=null&&<><span>.</span>{tr(cells(fraction,'f',false))}</>)}</span>:text)}</span>;
});
