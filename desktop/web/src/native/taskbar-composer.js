import {taskbarDefaults} from './taskbar-layout.js';
export const partLabels={number:'数字',line:'折线',bars:'图形'};
export const groups=[['CPU',['cpu','cpuTemperature','cpuFrequency']],['GPU',['gpu','gpuFrequency','gpuMemory','gpuFan','gpuPower','gpuTemperature']],['内存',['memory','memoryUsed']],['网络',['down','up','ping','loss']],['存储',['storage','storageRead','storageWrite','io']]];
const id=prefix=>`${prefix}-${globalThis.crypto?.randomUUID?.()??Date.now().toString(36)+Math.random().toString(36).slice(2)}`;
const blank=(page,row,column)=>({id:`${page}-r${row}c${column}`,row,column,module:null});
const newPage=(page,columns)=>({id:page,grid:{rows:2,columns},slots:Array.from({length:2*columns},(_,i)=>blank(page,Math.floor(i/columns),i%columns))});
export const nameVariants=metric=>metric==='memory'?['chinese','english','ram','icon']:metric==='storage'?['chinese','english','ssd','icon']:['down','up'].includes(metric)?['arrow','chinese']:['storageRead','storageWrite'].includes(metric)?['english','short','chinese','icon']:/^cpu|^gpu(?:$|Frequency|Temperature)/.test(metric)?['english','icon']:['chinese','english','icon'];
export const variantLabel={chinese:'中文',english:'英文',arrow:'箭头',icon:'图标',short:'单字',ram:'RAM',ssd:'SSD'};
const defaultVariant=metric=>nameVariants(metric)[0];
export function moduleLabel(module,fallback='未知'){
 const metric=module.metricId,variant=module.name?.variant;
 if(metric==='memory'&&variant==='ram')return 'RAM';if(metric==='storage'&&variant==='ssd')return 'SSD';
 if(metric==='down'||metric==='up')return variant==='chinese'?(metric==='down'?'下载':'上传'):(metric==='down'?'↓':'↑');
 if(variant==='icon')return '◇';if(metric.startsWith('cpu'))return 'CPU';if(['gpu','gpuFrequency','gpuTemperature'].includes(metric))return 'GPU';
 if(variant==='english')return ({memory:'MEM',storage:'Disk',gpuMemory:'VRAM',memoryUsed:'Used',gpuFan:'Fan',gpuPower:'Power',storageRead:'R',storageWrite:'W',ping:'Ping',loss:'Loss',io:'I/O'})[metric]||fallback;
 if(variant==='short')return ({storageRead:'读',storageWrite:'写'})[metric]||fallback;
 if(variant==='chinese')return ({storageRead:'读取',storageWrite:'写入'})[metric]||fallback;return fallback;
}
export function migrateLayout(value){
 if(value?.layout?.pages?.length)return value.layout;
 const source={...taskbarDefaults(),...value},columns=[2,4,6].includes(source.slots)?source.slots/2:2;
 const pages=(source.pages?.length?source.pages:taskbarDefaults().pages).slice(0,3).map((row,p)=>{
  const page=newPage(`page-${p+1}`,columns);page.slots.forEach((slot,i)=>{if(row?.[i])slot.module={id:`module-${slot.id}`,metricId:row[i],name:{variant:defaultVariant(row[i])},parts:['number']}});return page;
 });return {schemaVersion:2,revision:0,activePageId:pages[Math.max(0,Math.min(pages.length-1,source.page||0))].id,pages,shelf:{},typography:{}};
}
// Three columns show one value component per slot. Keep the saved composition
// until the user explicitly edits that component, so a wider layout can restore it.
export function displayModule(module,columns){
 if(!module||columns!==3||module.parts.length<=1)return module;
 return {...module,parts:[module.parts.includes('number')?'number':module.parts[0]]};
}
export function editLayout(layout,pageId,action){
 const current=layout.pages.find(p=>p.id===pageId)??layout.pages[0];
 let focus=action.to??action.from??null,nextPage=current.id;
 const fail=(message='',target=focus)=>({layout,message,focus:target,pageId:current.id});
 const single=current.grid.columns===3;
 const slots=current.slots.map(s=>({...s}));const a=slots.find(s=>s.id===action.from),b=slots.find(s=>s.id===action.to);
 const shelf={...layout.shelf};let pages=layout.pages.slice(),activePageId=layout.activePageId;
 let page={...current,slots};const setParts=(module,parts)=>({...module,parts});
 switch(action.type){
  case 'swap':if(!a?.module||!b||a===b)return fail();[a.module,b.module]=[b.module,a.module];break;
  case 'order':if(single||!a?.module||a.module.parts.length!==2)return fail();a.module=setParts(a.module,[...a.module.parts].reverse());break;
  case 'name':if(!a?.module||!nameVariants(a.module.metricId).includes(action.variant))return fail();a.module={...a.module,name:{...a.module.name,variant:action.variant}};break;
  case 'remove':if(!a?.module)return fail();shelf[a.module.metricId]=a.module;
   if(!single&&Number.isInteger(action.index)&&a.module.parts.length===2)a.module=setParts(a.module,a.module.parts.filter((_,i)=>i!==action.index));else a.module=null;break;
  case 'library':{
   if(!b)return fail('先选择一个位置。');
   const duplicate=slots.find(s=>s!==b&&s.module?.metricId===action.metric);if(duplicate)return fail('本页已有这个指标。',duplicate.id);
   if(b.module&&action.part&&b.module.metricId!==action.metric)return fail('只能替换同一指标的内容。');
   if(!b.module||b.module.metricId!==action.metric){if(b.module)shelf[b.module.metricId]=b.module;
    const saved=shelf[action.metric];const inUse=saved&&pages.some(p=>p.slots.some(s=>s.module?.id===saved.id));
    b.module=saved?{...saved,id:inUse?id('module'):saved.id}:{id:id('module'),metricId:action.metric,name:{variant:defaultVariant(action.metric)},parts:[action.part||'number']};
    if(action.part&&saved&&(single||!saved.parts.includes(action.part)))b.module=setParts(b.module,[action.part]);
    if(action.variant)b.module={...b.module,name:{...b.module.name,variant:action.variant}};break;
   }
   if(action.variant){if(b.module.name.variant===action.variant)return fail();b.module={...b.module,name:{...b.module.name,variant:action.variant}};break;}
   if(!action.part)return fail();
   if(single){if(displayModule(b.module,3).parts[0]===action.part)return fail();b.module=setParts(b.module,[action.part]);break;}
   const parts=[...b.module.parts],at=action.index??(parts.length===1?1:0);
   if(at<0||at>1||parts[at]===action.part)return fail();if(parts.includes(action.part))return fail('该内容已在此位置。');
   parts[at]=action.part;b.module=setParts(b.module,parts);break;
  }
  case 'resetTypographyX':{
   const {metric}=action,typography={...layout.typography};
   const source=metric?typography.metrics?.[metric]:typography.global;
   if(!source||!['labelX','numberX','unitX','graphX'].some(part=>Number(source[part])))return fail();
   const {labelX,numberX,unitX,graphX,...rest}=source;
   if(metric)typography.metrics={...typography.metrics,[metric]:rest};
   else typography.global=rest;
   return {layout:{...layout,typography},message:'',focus:action.from??null,pageId:current.id};
  }
  case 'typography':{
   const {metric,part}=action;if(!/^(label|number|unit|graph)X?$/.test(part))return fail();
   const limit=part.endsWith('X')?6:2,raw=Number(action.value);if(!Number.isFinite(raw))return fail();
   const value=Math.max(-limit,Math.min(limit,Math.round(raw*8)/8)),typography={...layout.typography};
   if(metric)typography.metrics={...typography.metrics,[metric]:{...typography.metrics?.[metric],[part]:value}};
   else typography.global={...typography.global,[part]:value};
   return {layout:{...layout,typography},message:'',focus:action.from??null,pageId:current.id};
  }
  case 'grid':{
   const columns=action.columns;if(![1,2,3].includes(columns)||columns===current.grid.columns)return fail();
   if(slots.some(s=>s.column>=columns&&s.module))return fail('先移走右侧列的模块，再缩减格数。');
   const archived=[...(current.retiredSlots??[])],activeMetrics=new Set(slots.map(s=>s.module?.metricId).filter(Boolean));
   page={...current,grid:{...current.grid,columns},slots:newPage(current.id,columns).slots.map(s=>{
    const old=slots.find(old=>old.row===s.row&&old.column===s.column);if(old)return old;
    const at=archived.findIndex(old=>old.row===s.row&&old.column===s.column&&!activeMetrics.has(old.module?.metricId));
    if(at<0)return s;const [restored]=archived.splice(at,1);if(restored.module)activeMetrics.add(restored.module.metricId);return restored;
   }),retiredSlots:archived};break;
  }
  case 'add':if(pages.length===3)return fail();page=newPage(id('page'),current.grid.columns);pages.push(page);nextPage=page.id;break;
  case 'delete':if(pages.length===1)return fail();
   if(slots.some(s=>s.module)&&!action.confirm)return fail('此页还有模块。再次点击减号，移除该页并把组合放回组件库。');
   for(const slot of slots)if(slot.module)shelf[slot.module.metricId]=slot.module;
   pages=pages.filter(p=>p!==current);nextPage=pages[Math.max(0,layout.pages.indexOf(current)-1)].id;
   if(activePageId===current.id)activePageId=nextPage;
   return {layout:{...layout,pages,shelf,activePageId},message:'',focus:null,pageId:nextPage};
  case 'movePage':{const at=pages.indexOf(current),to=at+action.delta;if(to<0||to>=pages.length)return fail();[pages[at],pages[to]]=[pages[to],pages[at]];break;}
  default:return fail();
 }
 const i=pages.findIndex(p=>p.id===page.id);pages[i]=page;
 return {layout:{...layout,pages,shelf,activePageId},message:'',focus,pageId:nextPage};
}
