import React from 'react';
export const iconThemes=[['night','夜色多彩'],['pearl','瓷白多彩'],['zones','色域分层'],['ember','日落信号'],['mint','薄荷电路'],['glacier','冰川极光']];
export const validIcon=id=>iconThemes.some(([key])=>key===id)?id:'night';
export function BrandMark({iconId=globalThis.__ASTER_ICON,className='',...props}){return <img src={'/brand-icons/'+validIcon(iconId)+'/symbol.svg'} alt="" aria-hidden="true" className={'asterhub-brand-image '+className} width="32" height="32" draggable="false" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='true';e.currentTarget.src='/brand-icons/night/symbol.svg'}}} {...props}/>}
