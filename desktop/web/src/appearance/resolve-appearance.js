import packs from './skin-packs.json' with {type:'json'};
export const legacyIds=['graphite','slate','forest','ocean','light'];
export const skinPacks=packs;
export const validTheme=id=>legacyIds.includes(id)||packs.some(p=>p.id===id)?id:'graphite';
const rgb=h=>h.slice(1).match(/../g).map(v=>parseInt(v,16));
const hex=v=>'#'+v.map(x=>Math.max(0,Math.min(255,Math.round(x))).toString(16).padStart(2,'0')).join('');
export const mix=(a,b,t)=>hex(rgb(a).map((v,i)=>v*(1-t)+rgb(b)[i]*t));
const luminance=h=>rgb(h).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((n,v,i)=>n+v*[.2126,.7152,.0722][i],0);
export const contrast=(a,b)=>(Math.max(luminance(a),luminance(b))+.05)/(Math.min(luminance(a),luminance(b))+.05);
function readable(a,b){const dest=luminance(b)>.4?'#172137':'#ffffff';for(let i=0;i<=40;i++){const v=mix(a,dest,i/40);if(contrast(v,b)>=4.5)return v}return dest}
export function resolveAppearance(settings={},systemDark=true,iconId){
 let id=validTheme(settings.theme),mode=['dark','light','system'].includes(settings.appearanceMode)?settings.appearanceMode:'dark';
 const pack=packs.find(p=>p.id===id);if(!pack)return {id,mode:id==='light'?'light':'dark',tokens:null};
 mode=mode==='system'?(systemDark?'dark':'light'):mode;const base=pack.appearance[mode],light=mode==='light',solid=pack.renderStyle==='solid';
 const surface=solid?base.surface:mix(base.surface,pack.palette.tint,light?.035:.16),bg=solid?base.canvas:mix(base.canvas,pack.palette.tint,light?.055:.18),sidebar=solid?base.sidebar:mix(base.sidebar,pack.palette.tint,light?.06:.12),accent=readable(pack.palette.accent,surface),selected=mix(sidebar,accent,light?.10:.15);
 const tokens={'chart-gpu':readable(pack.palette.signal[0],surface),bg,sidebar,surface,surface2:solid?base.elevated:mix(base.elevated,pack.palette.tint,light?.05:.13),text:base.text,muted:readable(base.muted,surface),subtle:readable(base.muted,surface),line:base.border,'line-soft':mix(base.border,bg,.45),green:readable(accent,selected),'green-soft':selected,hover:mix(surface,accent,.10),terminal:solid?base.terminal:mix(base.terminal,pack.palette.tint,light?.045:.10),input:mix(surface,bg,.35),blue:readable(pack.palette.signal[1],surface),purple:readable(pack.palette.signal[2],surface),amber:readable(pack.palette.signal[3],surface),danger:light?'#b43e53':'#ed969e',red:light?'#b43e53':'#ed969e',success:light?'#28754e':'#79cda6'};
 return {id,mode,tokens};
}
const keys=['chart-gpu','bg','sidebar','surface','surface2','text','muted','subtle','line','line-soft','green','green-soft','hover','terminal','input','blue','purple','amber','danger','red','success'];
export function applyAppearance(resolved,root=document.documentElement){root.dataset.theme=resolved.id;root.dataset.appearanceMode=resolved.mode;root.style.colorScheme=resolved.mode;for(const key of keys){if(resolved.tokens)root.style.setProperty('--'+key,resolved.tokens[key]);else root.style.removeProperty('--'+key)}}
export function nativeAppearance(resolved,glass=false){return {schemaVersion:1,theme:resolved.id,mode:resolved.mode,glass:!!glass,...(resolved.tokens?{colors:{background:resolved.tokens.bg,foreground:resolved.tokens.text,surface:resolved.tokens.surface,muted:resolved.tokens.muted,border:resolved.tokens.line}}:{})}}
