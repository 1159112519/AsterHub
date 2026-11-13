import assert from 'node:assert/strict';
import {createServer} from 'vite';
import {mkdtemp,mkdir} from 'node:fs/promises';
import {pathToFileURL,fileURLToPath} from 'node:url';
import {join,dirname} from 'node:path';
import {homedir,tmpdir} from 'node:os';
const modulePath=process.env.ASTERHUB_PLAYWRIGHT_MODULE||join(homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const {chromium}=await import(pathToFileURL(modulePath).href);
// Isolated bridge fixture: no real devices, files or application updates.
const fixture=String.raw`window.calls=[];const listeners=[];const gpus=[{Index:0,Name:'Fixture RTX',Util:36,Used:8050,Total:16000}],processes=Array.from({length:12},(_,i)=>({Gpu:0,Pid:300004+i,Name:'/home/demo/bin/python',Memory:2058+i,User:'fixture',CommandLine:'/home/demo/bin/python --train'}));const devices=[{device:{id:'local',name:'本机',host:'localhost',isLocal:true},state:'connected'},{device:{id:'B',name:'设备B',host:'192.0.2.2',isLocal:false},state:'connected'}];window.chrome={webview:{addEventListener(type,fn){listeners.push(fn)},removeEventListener(type,fn){const i=listeners.indexOf(fn);if(i>=0)listeners.splice(i,1)},postMessage(m){window.calls.push(m);const id=m.params?.deviceId||'local';const data={'devices.list':{items:devices,selected:'local'},'snapshot.latest':{deviceId:id,time:Date.now(),cpu:12,memory:30,cores:[],gpus,processes,hardware:{},volumes:[],network:{Interfaces:[],DownloadBytesPerSecond:0,UploadBytesPerSecond:0}},'app.info':{version:'0.4.3.4'},'desktop.settings':{},'history.query':{total:15,points:Array.from({length:15},(_,i)=>({id:'p'+i,time:Date.now()-(14-i)*60000,cpu:12+i,gpu:30+i,memory:30}))},'history.read':{gpus,processes,cpu:25,memory:30},'files.transfers':[{id:'done',status:'completed',names:['Completed fixture']},{id:'running',status:'running',names:['Active fixture']}],'updates.status':{revision:1,state:'ready',latest:'0.4.3.5',available:true},'storage.jobs':[],'storage.cleanup.jobs':[],'updates.preferences':{},'window.telemetry':true};setTimeout(()=>listeners.forEach(fn=>fn({data:{id:m.id,ok:true,result:data[m.method]??null}})),m.method==='history.read'?110:3)}}};`;
const webRoot=dirname(fileURLToPath(import.meta.url));
// Restoring one former defect must make the corresponding regression fail.
// Mutations only affect the dev server transform; source files stay intact.
const evidence=process.env.ASTERHUB_SURFACE_EVIDENCE;
if(evidence)await mkdir(evidence,{recursive:true});
const mutation=process.env.ASTERHUB_SURFACE_MUTATION;
const mutationPlugin={name:'surface-regression-mutation',enforce:'pre',transform(code,id){
 if(mutation==='device'&&id.endsWith('/sidebar-collapse.css'))return code.replace('scrollbar-gutter:stable both-edges;max-height:min(342px','scrollbar-gutter:stable;max-height:min(342px');
 if(mutation==='marker'&&id.endsWith('/taskbar-settings.css'))return code.replace('transition:transform var(--motion-layout) var(--ease),width var(--motion-layout) var(--ease)','transition:none');
 if(mutation==='history'&&id.endsWith('/motion-surface.jsx'))return code.replace("else if(was!=='open'||previous?.key!==changeKey){","else if(false){");
}};
const cacheDir=await mkdtemp(join(tmpdir(),'asterhub-surfaces-'));
const vite=await createServer({root:webRoot,plugins:[mutationPlugin],cacheDir,optimizeDeps:{esbuildOptions:{logLevel:'silent'}},resolve:{preserveSymlinks:true},server:{host:'127.0.0.1',port:0,hmr:false},logLevel:'error'});await vite.listen();
let browser;
try{
 browser=await chromium.launch({channel:'msedge',headless:true});
 for(const dpr of [1,1.25]){
  const page=await browser.newPage({viewport:{width:1100,height:800},deviceScaleFactor:dpr});page.setDefaultTimeout(7000);
  const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.addInitScript({content:fixture});
  await page.goto('http://127.0.0.1:'+vite.httpServer.address().port);const button=n=>page.getByRole('button',{name:n,exact:true});
  const picker=page.locator('.sidebar-device-popover');await page.locator('.device-switch-trigger').click();await picker.waitFor();
  const inset=await picker.evaluate(e=>{const p=e.getBoundingClientRect(),row=e.querySelector('.native-device-row').getBoundingClientRect(),s=e.querySelector('.device-picker-controls label').getBoundingClientRect(),add=e.querySelector('.device-picker-controls button').getBoundingClientRect();return [row.left-p.left,p.right-row.right,row.left-s.left,row.right-add.right]});assert.ok(Math.abs(inset[0]-inset[1])<1&&Math.abs(inset[2])<1&&Math.abs(inset[3])<1,JSON.stringify(inset));await page.keyboard.press('Escape');
  await page.locator('.task-center-trigger').click();const tasks=page.locator('.task-center-panel'),clear=tasks.getByRole('button',{name:'清除已结束'});
  await tasks.getByText('Completed fixture',{exact:true}).waitFor();await clear.click();await tasks.getByText('Completed fixture',{exact:true}).waitFor({state:'hidden'});
  await tasks.getByText('Active fixture',{exact:true}).waitFor();await tasks.getByRole('button',{name:'查看更新'}).waitFor();assert.ok(await clear.isDisabled());
  await page.keyboard.press('Escape');await page.locator('.task-center-trigger').click();await tasks.waitFor();assert.equal(await tasks.getByText('Completed fixture',{exact:true}).count(),0);await page.keyboard.press('Escape');
  const resource=page.locator('.native-resource-inspector');
  for(const id of ['GPU','CPU','内存','网络','存储']){
   await button('资源概览').click();await button('查看'+id+'概览详情').click();await resource.waitFor();
   const css=await resource.evaluate(e=>[getComputedStyle(e).backgroundImage,getComputedStyle(e.querySelector('header')).borderBottomWidth,getComputedStyle(e.querySelector('footer')).borderTopWidth]);assert.match(css[0],/linear-gradient/);assert.deepEqual(css.slice(1),['0px','0px']);
   await resource.getByRole('button',{name:'打开'+(/^[A-Z]+$/.test(id)?' '+id+' ':id)+'页面',exact:true}).click();await page.locator('.top-page').getByText(id,{exact:true}).waitFor();
  }
  await button('偏好设置').click();await page.getByRole('navigation',{name:'设置分类'}).getByRole('button',{name:'任务栏监测',exact:true}).click();
  const mode=page.locator('.tb43-modes');await mode.getByRole('tab',{name:'组件'}).waitFor();
  const marker=await mode.evaluate(e=>new Promise(resolve=>{const m=e.querySelector('.tb43-mode-indicator'),f=()=>m.getBoundingClientRect().x,a=f();e.querySelectorAll('[role=tab]')[1].click();requestAnimationFrame(()=>requestAnimationFrame(()=>{const mid=f();setTimeout(()=>resolve([a,mid,f(),e.querySelector('[aria-selected=true]').getBoundingClientRect().left+4]),330)}))}));assert.ok(marker[0]<=marker[1]&&marker[1]<marker[2]&&Math.abs(marker[2]-marker[3])<1,JSON.stringify(marker));await page.keyboard.press('Escape');
  await button('历史记录').click();await page.getByRole('group',{name:'历史资源指标'}).getByRole('button',{name:'GPU'}).click();
  const chart=page.locator('.history-trend-panel:not([aria-hidden=true]) [role=slider]');await chart.waitFor();
  await page.evaluate(()=>{window.popupFrames=[];let start=performance.now();function tick(){const node=document.querySelector('.history-trend-panel:not([aria-hidden=true]) .chart-selection-popover'),p=node?.querySelector('.history-peek header time');if(p){const s=getComputedStyle(node.firstElementChild),r=p.getBoundingClientRect();window.popupFrames.push({x:r.x,y:r.y,opacity:Number(s.opacity),transform:s.transform,phase:node.dataset.motionState})}if(performance.now()-start<1500)requestAnimationFrame(tick)}requestAnimationFrame(tick)});
  await chart.press('End');await page.locator('.history-peek').waitFor();await page.waitForTimeout(330);const frames=await page.evaluate(()=>window.popupFrames);assert.ok(frames.some(f=>f.opacity>0&&f.opacity<1),JSON.stringify(frames));assert.ok(frames.every(f=>f.x===frames[0].x&&f.y===frames[0].y&&f.transform==='none'));assert.equal(frames.at(-1).opacity,1);if(evidence)await page.screenshot({path:join(evidence,`history-${dpr}.png`)});
  const reads=()=>page.evaluate(()=>window.calls.filter(m=>m.method==='history.read').length);
  const peek=page.locator('.history-peek'),before=await reads();await page.keyboard.press('Escape');await peek.waitFor({state:'hidden'});
  await chart.press('End');await peek.waitFor();assert.equal(await reads(),before);
  await page.keyboard.press('Escape');await peek.waitFor({state:'hidden'});await chart.press('Home');await page.keyboard.press('Escape');
  await page.waitForTimeout(175);assert.equal(await peek.count(),0,'A cancelled cold request must not reopen the popup');
  await page.emulateMedia({reducedMotion:'reduce'});await chart.press('End');await peek.waitFor();assert.equal(await peek.evaluate(e=>getComputedStyle(e.parentElement.parentElement).opacity),'1');
  await page.emulateMedia({reducedMotion:'no-preference'});
  await page.locator('.sidebar nav .nav-item').first().click();await page.locator('.overview-resource-grid').waitFor();
  for(const theme of ['graphite','03']){
   await page.evaluate(async theme=>{const a=await import('/src/appearance/resolve-appearance.js');a.applyAppearance(a.resolveAppearance({theme,appearanceMode:theme==='03'?'light':'dark'}))},theme);
   await page.locator('.native-overview-card.resource-gpu').click();await resource.waitFor();await page.waitForFunction(()=>getComputedStyle(document.querySelector('.native-resource-inspector')).opacity==='1');
   for(const language of ['zh-CN','en']){
    await page.evaluate(async lang=>(await import('/src/i18n/index.js')).setLanguage(lang),language);
    const bounds=await resource.evaluate(e=>{const b=e.getBoundingClientRect(),f=e.querySelector('footer button').getBoundingClientRect(),h=e.querySelector('header h2').getBoundingClientRect();return {left:b.left,right:b.right,width:innerWidth,button:f.right,header:h.left,top:b.top,bottom:b.bottom,height:innerHeight}});
    if(evidence)await page.screenshot({path:join(evidence,`inspector-${theme}-${language}-${dpr}.png`)});
    assert.ok(bounds.left>=0&&bounds.right<=bounds.width&&bounds.button<=bounds.right&&bounds.header>=bounds.left&&bounds.top>=0&&bounds.bottom<=bounds.height,JSON.stringify({theme,language,bounds}));
   }
   await page.keyboard.press('Escape');await resource.waitFor({state:'hidden'});await page.evaluate(async()=>(await import('/src/i18n/index.js')).setLanguage('zh-CN'));
  }
  assert.deepEqual(errors,[]);console.log(`PASS ${dpr*100}%: symmetric device picker; five resource routes; sliding taskbar marker; ${frames.length} stable GPU snapshot frames`);await page.close();
 }
}finally{if(browser)await browser.close();await vite.close();}
