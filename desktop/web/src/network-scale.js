import {useRef} from 'react';
import {networkScaleState} from './network-scale-state';
// Scale selection is sample-driven. TimelineCurve interpolates SVG transforms;
// never render the entire footer once per animation frame.
export function useNetworkScale(samples){
 const state=useRef(null);
 const next=networkScaleState(samples,state.current);state.current=next;
 return next.max;
}
