import {useLayoutEffect,useRef,useState,useSyncExternalStore} from 'react';
import {gsap} from 'gsap';
import {reducedMotion,subscribeReducedMotion} from './motion';
import {networkScaleState} from './network-scale-state';
// Both directions share one animated scale; a device change remounts the footer.
export function useNetworkScale(samples){
 const state=useRef(null),value=useRef(null),[scale,setScale]=useState(()=>networkScaleState(samples,null).max),reduced=useSyncExternalStore(subscribeReducedMotion,reducedMotion);
 const next=networkScaleState(samples,state.current);state.current=next;
 if(!value.current)value.current={max:scale};
 useLayoutEffect(()=>{const target=next.max;if(reduced){value.current.max=target;setScale(target);return}const tween=gsap.to(value.current,{max:target,duration:.85,ease:'power2.inOut',onUpdate:()=>setScale(value.current.max)});return()=>tween.kill()},[next.max,reduced]);
 return scale;
}
