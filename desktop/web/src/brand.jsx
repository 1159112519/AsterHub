import React from 'react';
export const iconThemes=[["01", "01"], ["02", "02"], ["03", "03"], ["04", "04"], ["05", "05"], ["06", "06"], ["07", "07"], ["08", "08"], ["09", "09"], ["10", "10"], ["11", "11"], ["12", "12"]];
const aliases={night:'01',pearl:'02',zones:'03',ember:'04',mint:'05',glacier:'06'};
export const validIcon=id=>aliases[id]|| (iconThemes.some(([key])=>key===id)?id:'01');
export function BrandMark({iconId=globalThis.__ASTER_ICON,className='',...props}){return <img src={'/brand-icons/'+validIcon(iconId)+'/symbol.svg'} alt="" aria-hidden="true" className={'asterhub-brand-image '+className} width="32" height="32" draggable="false" onError={e=>{if(!e.currentTarget.dataset.fallback){e.currentTarget.dataset.fallback='true';e.currentTarget.src='/brand-icons/01/symbol.svg'}}} {...props}/>}
