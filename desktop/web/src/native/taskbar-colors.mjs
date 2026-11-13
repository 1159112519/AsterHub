export const tabs=[['components','组件'],['colors','颜色'],['tuning','位置']];
export function moveTab(id,key){const i=tabs.findIndex(([x])=>x===id);return tabs[key==='Home'?0:key==='End'?2:(i+(key==='ArrowLeft'?2:1))%3][0]}
export const parts=[['label','名称'],['reading','读数'],['graph','图形']];
export const hex=x=>/^[#]?[a-f\d]{6}$/i.test(x?.trim()??'')?'#'+x.trim().replace('#','').toUpperCase():null;
const clamp=x=>Math.min(1,Math.max(0,x));
export function fromHsv(h,s,v){const f=n=>{const k=(n+h/60)%6;return Math.round(255*clamp(v-v*s*Math.max(0,Math.min(k,4-k,1)))).toString(16).padStart(2,'0')};return ('#'+f(5)+f(3)+f(1)).toUpperCase()}
export function toHsv(color){const [r,g,b]=hex(color).slice(1).match(/../g).map(x=>parseInt(x,16)/255);const max=Math.max(r,g,b),d=max-Math.min(r,g,b);const h=!d?0:60*(max===r?(g-b)/d:max===g?2+(b-r)/d:4+(r-g)/d);return [(h+360)%360,max?d/max:0,max]}
export function withColor(map,metric,part,value){if(value!==null&&!hex(value)||!parts.some(([p])=>p===part))return map;const out={...map}, row={...out[metric]};if(value===null)delete row[part];else row[part]=hex(value);if(Object.keys(row).length)out[metric]=row;else delete out[metric];return out}
export const metricCss=(map,metric)=>({'--tb-custom-name':map?.[metric]?.label,'--tb-custom-reading':map?.[metric]?.reading,'--tb-custom-graph':map?.[metric]?.graph});
