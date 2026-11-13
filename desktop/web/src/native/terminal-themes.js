import {terminalPalettePresets} from './terminal-preset-palette.js';
import {resolveAppearance} from '../appearance/resolve-appearance.js';
// The xterm contract used by Tabby: foreground/background, cursor, selection and 16 ANSI entries.
export const terminalAnsiKeys=['black','red','green','yellow','blue','magenta','cyan','white','brightBlack','brightRed','brightGreen','brightYellow','brightBlue','brightMagenta','brightCyan','brightWhite'];
const palettes={
 graphite:['#202529','#da808c','#a7c3a1','#d4b481','#99b4da','#c7a4d3','#85bfc3','#dce0df','#788482','#f5a2ae','#c2deb2','#edd09a','#b8cdf0','#e0c0eb','#a4dfe0','#ffffff'],
 slate:['#24283b','#f7768e','#9ece6a','#e0af68','#7aa2f7','#bb9af7','#7dcfff','#c0caf5','#69779f','#ff9eaf','#b8e589','#f1cc8b','#a4bfff','#d5bbff','#a5e6ff','#e9edf5'],
 forest:['#203228','#d58e94','#9dcb9b','#d5c38b','#91afcd','#bca3cc','#8bc6b8','#dce8dc','#728b77','#efacb2','#c0e2b2','#f1dda4','#b4cee5','#d6bee3','#b0e1d3','#f5fff5'],
 ocean:['#1d2e3d','#e18a9e','#9ccbb9','#d9c18c','#8eb6e7','#b9a7de','#75c6d7','#d3e7f0','#728d9f','#f9afbd','#bce6d6','#efdaa7','#b6d2f8','#d6c4f4','#a3e3ed','#f2fbff'],
 light:['#26352d','#a6384c','#326b40','#826019','#365e98','#7d488a','#1e6974','#53645a','#65766b','#b84b5f','#407c4b','#92701d','#486fb0','#90579f','#257c89','#4c6154']
};
const surfaces={graphite:['#181d1a','#e6ece7','#a1ceaa','#3c5545'],slate:['#171b23','#e9edf5','#a8bbdc','#344157'],forest:['#111f17','#e7efe8','#a6d6ad','#344c3c'],ocean:['#181f28','#e2e9ef','#98c5c8','#3a4654'],light:['#eaf0e8','#26352d','#35724c','#cbd9cd']};
export const terminalThemes=Object.fromEntries(Object.entries(surfaces).map(([id,[background,foreground,cursor,selectionBackground]])=>[id,{background,foreground,cursor,cursorAccent:background,selectionBackground,selectionForeground:foreground,...Object.fromEntries(terminalAnsiKeys.map((key,i)=>[key,palettes[id][i]]))}]));
function baseTheme(settings={}){
 const follow=settings.terminalTheme==='follow'||!settings.terminalTheme;
 if(!follow)return {...(terminalThemes[settings.terminalTheme]||terminalThemes.graphite)};
 const r=resolveAppearance(settings,typeof matchMedia==='function'?matchMedia('(prefers-color-scheme:dark)').matches:true,settings.iconThemeId||globalThis.__ASTER_ICON);
 const base=terminalThemes[r.mode==='light'?'light':settings.theme]||terminalThemes.graphite;
 if(!r.tokens)return {...base};const t=r.tokens,light=r.mode==='light';
 return {...base,background:t.terminal,foreground:t.text,cursor:t.green,cursorAccent:t.terminal,selectionBackground:t['green-soft'],selectionForeground:t.text,green:light?'#2c724a':'#a7d7ad',blue:light?'#42639b':'#9cbdea',cyan:light?'#246f77':'#8ecfd3',yellow:light?'#856127':'#ddc18f'};
}


export function terminalIsLight(theme){
 const rgb=String(theme.background||'').match(/^#([0-9a-f]{6})$/i);if(!rgb)return false;const n=parseInt(rgb[1],16);return ((n>>16)*.2126+((n>>8)&255)*.7152+(n&255)*.0722)>155;
}
// Kept for older importers; schemes are now always independent of the interface mode.
export function terminalLightAllowed(){return true;}
export function terminalChrome(theme){
 const light=terminalIsLight(theme);return {'--terminal-bg':theme.background,'--terminal-fg':theme.foreground,'--terminal-muted':light?'#596465':'#a6b1ae','--terminal-surface':light?'#e3e8e6':'#202724','--terminal-hover':light?'#d0d9d5':'#313c35','--terminal-line':light?'#b5c2ba':'#46534b'};
}
export function terminalTheme(settings={},preview=false){
 const theme=baseTheme(settings),overrides=settings.terminalColorOverrides;
 if(overrides&&typeof overrides==='object')for(const key of [...terminalAnsiKeys,'foreground','background','cursor','cursorAccent','selectionBackground','selectionForeground'])if(/^#[0-9a-f]{6}$/i.test(overrides[key]||''))theme[key]=overrides[key];
 if(overrides?.foreground&&!overrides.selectionForeground)theme.selectionForeground=theme.foreground;
 return theme;
}

/* Tabby defaults from tabby-terminal/src/colorSchemes.ts (MIT).
 * Copyright (c) 2017 Tabby Developers
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * ANSI, text, background and cursor are unchanged from the source. Tabby does
 * not specify selection colors here; xterm gets an inverse text selection.
 */
const tabbyDefaults=[
 ['tabby-default','#cacaca','#171717','#bbbbbb',['#000000','#ff615a','#b1e969','#ebd99c','#5da9f6','#e86aff','#82fff7','#dedacf','#313131','#f58c80','#ddf88f','#eee5b2','#a5c7ff','#ddaaff','#b7fff9','#ffffff']],
 ['tabby-default-light','#4d4d4c','#ffffff','#4d4d4c',['#000000','#c82829','#718c00','#eab700','#4271ae','#8959a8','#3e999f','#ffffff','#000000','#c82829','#718c00','#eab700','#4271ae','#8959a8','#3e999f','#ffffff']]
];
for(const [id,foreground,background,cursor,colors] of tabbyDefaults)terminalThemes[id]={foreground,background,cursor,cursorAccent:background,selectionBackground:foreground,selectionForeground:background,...Object.fromEntries(terminalAnsiKeys.map((key,index)=>[key,colors[index]]))};

for(const {id,theme,colors} of terminalPalettePresets)terminalThemes[id]={...theme,...Object.fromEntries(terminalAnsiKeys.map((key,i)=>[key,colors[i]]))};
export const terminalSchemes=[
 {id:'follow',name:'跟随界面',description:'使用当前外观的终端颜色',keywords:'自动 follow auto'},
 {id:'graphite',name:'经典黑',description:'AsterHub · Graphite',keywords:'黑色 石墨 dark'},
 {id:'slate',name:'暮蓝',description:'AsterHub · Slate',keywords:'蓝色 dark'},
 {id:'forest',name:'松林',description:'AsterHub · Forest',keywords:'绿色 green dark'},
 {id:'ocean',name:'深海',description:'AsterHub · Ocean',keywords:'蓝色 blue dark'},
 {id:'light',name:'瓷白',description:'AsterHub · Light',keywords:'白色 浅色'},
 {id:'tabby-default',name:'Tabby Default',description:'Tabby · 内置深色',keywords:'黑色 深色 dark'},
 {id:'tabby-default-light',name:'Tabby Default Light',description:'Tabby · 内置浅色',keywords:'白色 浅色'},
 ...terminalPalettePresets.map(({id,theme})=>({id,name:id,description:'',keywords:terminalIsLight(theme)?'浅色 light':'深色 dark'}))
];
export function filterTerminalSchemes(query=''){
 const words=String(query).trim().toLocaleLowerCase().split(/\s+/).filter(Boolean);
 return terminalSchemes.filter(scheme=>words.every(word=>`${scheme.name} ${scheme.description} ${scheme.keywords} ${scheme.id}`.toLocaleLowerCase().includes(word)));
}
const overrideKeys=[...terminalAnsiKeys,'foreground','background','cursor','cursorAccent','selectionBackground','selectionForeground'];
function cleanOverrides(value){
 if(!value||typeof value!=='object'||Array.isArray(value))return {};
 return Object.fromEntries(overrideKeys.filter(key=>/^#[0-9a-f]{6}$/i.test(value[key]||'')).map(key=>[key,value[key]]));
}
function savedSchemeOverrides(settings){
 const saved=settings.terminalSchemeOverrides;
 return saved&&typeof saved==='object'&&!Array.isArray(saved)?saved:{};
}
export function selectTerminalScheme(settings={},id){
 const current=settings.terminalTheme||'follow';
 if(id===current||!terminalSchemes.some(scheme=>scheme.id===id))return {...settings};
 const saved=savedSchemeOverrides(settings),target=cleanOverrides(saved[id]);
 return {...settings,terminalTheme:id,terminalColorOverrides:target,terminalSchemeOverrides:{...saved,[current]:cleanOverrides(settings.terminalColorOverrides),[id]:{...target}}};
}
// A card shows the same colors that clicking it will apply. Active edits take
// precedence over its saved entry until switching stores them in the dictionary.
export function terminalSchemeTheme(id,settings={}){
 const overrides=id===(settings.terminalTheme||'follow')?settings.terminalColorOverrides:savedSchemeOverrides(settings)[id];
 return terminalTheme({...settings,terminalTheme:id,terminalColorOverrides:cleanOverrides(overrides)},true);
}
