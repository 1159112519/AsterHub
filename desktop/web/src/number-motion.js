import {useSyncExternalStore} from 'react';
import {createPreferenceStore} from './foundation/preference-store.js';
export const numberMotionOptions=[['direct','直接更新'],['current','逐位滚动'],['fade','柔和淡入'],['slide','轻盈滑动']];
export const validNumberMotion=value=>numberMotionOptions.some(([key])=>key===value)?value:'current';
export function readNumberMotion(){return validNumberMotion(typeof document==='undefined'?undefined:document.documentElement.dataset.numberMotion)}
export function applyNumberMotion(value){document.documentElement.dataset.numberMotion=validNumberMotion(value)}
const store=createPreferenceStore(readNumberMotion,notify=>{const observer=new MutationObserver(notify);observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-number-motion']});return()=>observer.disconnect()});
export function useNumberMotion(){return useSyncExternalStore(store.subscribe,readNumberMotion)}
// Incoming and outgoing digits always end at a clean, untransformed resting state.
export function numberFrames(style,incoming=true,distance=14){
 const shift=style==='fade'?0:style==='slide'?distance*.55:distance,blur=style==='current'?1.6:0;
 const rest={transform:'translateY(0px)',opacity:1,filter:'blur(0px)'};
 const away={transform:`translateY(${incoming?shift:-shift}px)`,opacity:0,filter:`blur(${blur}px)`};
 return incoming?[away,rest]:[rest,away];
}
