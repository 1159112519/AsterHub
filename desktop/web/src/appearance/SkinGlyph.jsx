import React from 'react';
import {mix,resolveAppearance,skinPacks} from './resolve-appearance.js';

// Use the earlier color-block silhouette, with the current theme's color roles.
const legacy={
 graphite:['#17181a','#1c1d20','#28292d','#343539','#c2c3c7'],
 slate:['#1b1e26','#161920','#2b3040','#3a4354','#a7bfe8'],
 forest:['#19241f','#15201b','#293a31','#3d5446','#a7d8b6'],
 ocean:['#1a2330','#151d29','#28384b','#3c526b','#91ccdf'],
 light:['#e0ebfc','#cddff8','#d7e6fc','#aec7e8','#48689e']
};
export const SkinGlyph=React.memo(function SkinGlyph({id,mode}){
 const {tokens,mode:variant}=resolveAppearance({theme:id,appearanceMode:mode});
 const pack=skinPacks.find(p=>p.id===id);
 const [canvas,side,surface,border,accent]=tokens?[tokens.bg,tokens.sidebar,tokens.surface2,tokens.line,pack.palette.accent]:legacy[id]??legacy.graphite;
 const soft=variant==='light'?.54:(pack ? .95 : .73);
 return <svg className="skin-miniature" viewBox="0 0 88 60" aria-hidden="true" focusable="false">
  <rect x=".5" y=".5" width="87" height="59" rx="7" fill={canvas} stroke={mix(canvas,border,.55)}/>
  <rect x="8" y="8" width="13" height="44" rx="3.5" fill={side}/>
  <rect x="27" y="8" width="53" height="20" rx="3.5" fill={mix(canvas,accent,soft)}/>
  <rect x="27" y="33" width="24" height="19" rx="3.5" fill={surface}/>
  <rect x="56" y="33" width="24" height="19" rx="3.5" fill={surface}/>
 </svg>;
});
