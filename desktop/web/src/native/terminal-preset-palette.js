import {contrast,mix} from '../appearance/resolve-appearance.js';
// Each family defines six ANSI hues and five hand-selected surfaces. UI displays samples, not names.
const families=[
 {bg:['#14161b','#20232b','#252330','#171c25','#161919'],fg:'#dce3f2',tint:'#aebee9',ansi:['#f2909f','#9ed7b5','#efd08f','#9db8f9','#d0a7f1','#8ed9e2']},
 {bg:['#101b18','#192821','#20332b','#222920','#202622'],fg:'#e2edda',tint:'#b0db95',ansi:['#f4988d','#b4db83','#f2cc78','#93bce1','#dbabd9','#8cd4bc']},
 {bg:['#201817','#2c201c','#342721','#262329','#231b20'],fg:'#f4e7da',tint:'#ffc5a1',ansi:['#ff9a8f','#c6d58e','#f7ce87','#a2bfe7','#e5a4d9','#94d6ca']},
 {bg:['#151c2c','#192339','#24314b','#21293c','#1b232c'],fg:'#e1ebff',tint:'#b4c6ff',ansi:['#f691a6','#9de5bd','#f4d185','#8aaeff','#c9afff','#83dce9']},
 {bg:['#1e1424','#271a2f','#34213d','#2a2030','#201c2a'],fg:'#f3def4',tint:'#e8aaf0',ansi:['#fb98ac','#b3de94','#f1ca95','#a0b9f8','#edafed','#94dae6']},
 {bg:['#101e25','#142a31','#1d3540','#203039','#18252e'],fg:'#dff3f3',tint:'#9ce4e1',ansi:['#f9959d','#9ce1a4','#efd28a','#94bfff','#d1b5fa','#74e5df']},
 {bg:['#211c13','#2d2718','#392d1c','#2d281e','#211e1a'],fg:'#f5ecd6',tint:'#f4ce82',ansi:['#fa9680','#bfdb9c','#f4d28a','#a3bbf5','#ddaef1','#97d5d4']},
 {bg:['#211620','#2c1d26','#39242e','#2c2229','#211b25'],fg:'#f4e1e8',tint:'#f3b6ce',ansi:['#f58fa2','#b0dfa7','#f3c990','#a6beee','#dfb4f4','#a0d8e1']},
 {bg:['#151718','#1d1f20','#282a2b','#20272a','#222420'],fg:'#e5e9ea',tint:'#b9cdcb',ansi:['#ec9a9a','#b1d29a','#e7c786','#a8c5f2','#cfacd9','#97d4d3']},
 {bg:['#f5f7fb','#edf2fa','#f0f4f2','#e7eff2','#f2eff7'],fg:'#24384f',tint:'#375a82',ansi:['#a23654','#286647','#765813','#315ca2','#783a88','#256575']},
 {bg:['#f9f3e7','#f3eadb','#f3f0e0','#f8eee9','#eee9e0'],fg:'#493728',tint:'#7c4e32',ansi:['#9d3b45','#436837','#735511','#3f5d99','#79497b','#2c655e']},
 {bg:['#f2f5ea','#eaf3ec','#e7f0e8','#f0f4f1','#eef2df'],fg:'#2c4131',tint:'#386f55',ansi:['#9a394e','#2b683e','#705a21','#395f95','#794982','#28676b']},
];
function readable(color,bg,goal){const ink=contrast('#ffffff',bg)>contrast('#000000',bg)?'#ffffff':'#000000';for(let i=0;i<=20;i++){const candidate=mix(color,ink,i/20);if(contrast(candidate,bg)>=goal)return candidate;}return ink;}
export const terminalPalettePresets=families.flatMap(({bg,fg,tint,ansi},family)=>bg.map((background,variant)=>{
 const light=family>=9,accent=ansi[(family+variant*2)%ansi.length];
 const foreground=readable(variant===0||variant===4?fg:mix(fg,tint,.25+variant*.14),background,7);
 const cursor=readable(mix(accent,tint,.33),background,4.5);
 const selectionBackground=mix(background,cursor,light?.18:.29),selectionForeground=readable(foreground,selectionBackground,4.5);
 const normal=ansi.map(color=>readable(mix(color,tint,variant*.043),background,4.5));
 const bright=normal.map(color=>readable(mix(color,light?'#000000':'#ffffff',.19),background,5.5));
 return {id:`palette-${family+1}-${variant+1}`,theme:{background,foreground,cursor,cursorAccent:background,selectionBackground,selectionForeground},colors:[light?mix(foreground,'#000000',.6):mix(background,'#000000',.65),...normal,foreground,readable(mix(background,foreground,.46),background,3),...bright,readable(mix(foreground,light?'#000000':'#ffffff',.33),background,7)]};
}));
