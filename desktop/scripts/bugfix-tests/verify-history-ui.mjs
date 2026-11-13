import assert from 'node:assert/strict';import fs from 'node:fs/promises';import path from 'node:path';import http from 'node:http';import {createRequire} from 'node:module';import {fileURLToPath} from 'node:url';
import {chromium} from 'file:///C:/Users/lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..'),out=path.resolve(process.env.ASTER_HISTORY_EVIDENCE||path.resolve(root,'../artifacts/history-ui'));await fs.mkdir(out,{recursive:true});const require=createRequire(path.join(root,'web/package.json'));const esbuild=require('esbuild');
await esbuild.build({stdin:{contents:`import React from 'react';import {createRoot} from 'react-dom/client';import {HistoryView} from './src/native/HistoryView.jsx';import './src/styles.css';import './src/refinements.css';import './src/native/native.css';import './src/native/readability.css';import './src/foundation/control-alignment.css';const root=createRoot(document.getElementById('root'));window.mountDevice=id=>root.render(<HistoryView deviceId={id}/>);window.hideHistory=()=>root.render(null);window.mountDevice('A');`,resolveDir:path.join(root,'web'),loader:'jsx'},bundle:true,external:['/fonts/*'],outfile:path.join(out,'ui.js')});
const mock=`window.calls=[];window.slow=false;window.detailDelay=5;const listeners=[];window.chrome={webview:{addEventListener(t,fn){listeners.push(fn)},removeEventListener(){},postMessage(m){window.calls.push(m);let result={},delay=5;if(m.method==='history.query'){const end=m.params.toUtc||Date.now(),from=m.params.fromUtc||end-m.params.minutes*60000;result={total:m.params.minutes===1440?43200:1200,points:Array.from({length:m.params.minutes===1440?2400:500},(_,i)=>({id:String(i),time:from+(end-from)*i/(m.params.minutes===1440?2399:499),cpu:20+i%50,memory:40,gpu:30+i%60,interval:2,gapBefore:i===800}))};if(window.slow&&m.params.minutes===1440)delay=900}if(m.method==='history.read'){delay=window.detailDelay;result={cpu:25,memory:30,cores:[],gpus:[{Index:'0',Name:'Fixture GPU',Util:'55%',Used:'1024 MiB',Total:'8192 MiB'}],processes:Array.from({length:9},(_,i)=>({Gpu:'0',Pid:String(42+i),Name:i===0?'C:/Windows/System32/dwm.exe':'C:/Users/fixture/Applications/long-directory-name/environments/python/fixture-worker-'+i+'.exe',User:i===2?'本机用户':'fixture',Memory:i===0?'482 MiB':i===1?'43 MiB':'512 MiB'}))}};if(m.method==='recorder.status')result={installed:false};if(m.method==='history.cancel')result=true;setTimeout(()=>listeners.forEach(fn=>fn({data:{id:m.id,ok:true,result}})),delay)}}};`;
const html=`<!doctype html><meta charset="utf-8"><link rel="stylesheet" href="/fonts/interface-fonts.css"><link rel="stylesheet" href="/ui.css"><style>body{margin:0;background:#1c211e;color:#e7eee9}.native-workbench{display:block!important;padding:30px;max-width:1240px;margin:auto}.page-heading{display:flex;justify-content:space-between}.history-toolbar{display:flex;justify-content:space-between;align-items:center;margin-bottom:16px}.history-chart{padding:20px;background:var(--surface);border:1px solid var(--line);border-radius:12px}.history-calendar{box-sizing:content-box}</style><div class="native-workbench"><div id="root"></div></div><script>${mock}</script><script src="/ui.js"></script>`;
const server=http.createServer(async(req,res)=>{if(req.url.startsWith('/fonts/')){try{return res.end(await fs.readFile(path.join(root,'web/public',req.url)))}catch{res.statusCode=404;return res.end()}}if(req.url==='/'){res.setHeader('Content-Type','text/html; charset=utf-8');return res.end(html)}try{res.setHeader('Content-Type',req.url.endsWith('.css')?'text/css':'text/javascript');res.end(await fs.readFile(path.join(out,req.url.slice(1))))}catch{res.statusCode=404;res.end()}});await new Promise(r=>server.listen(0,'127.0.0.1',r));const browser=await chromium.launch({channel:'msedge',headless:true}),page=await browser.newPage({viewport:{width:1280,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));const url=`http://127.0.0.1:${server.address().port}`;
// Sample the same popup from its first visible frame through the native reply.
async function frameAudit(scale){
 const target=await browser.newPage({viewport:{width:1280,height:1000},deviceScaleFactor:scale});
 try{
  await target.goto(url);await target.getByText('1200 个已存档采样',{exact:false}).waitFor();
  await target.evaluate(async()=>{await document.fonts.ready;window.detailDelay=350});
  const chart=target.locator('.history-trend-panel[aria-hidden="false"] .chart');
  async function capture(label,x){
   await target.evaluate(()=>{window.frameCapture=new Promise(resolve=>{const frames=[],start=performance.now();function read(now){const outer=document.querySelector('.chart-selection-popover'),surface=outer?.querySelector('.motion-surface');if(outer&&surface&&Number(getComputedStyle(surface).opacity)>.01){const r=outer.getBoundingClientRect(),scroll=outer.querySelector('.history-peek-scroll .scroll-surface-scroll'),rows=[...outer.querySelectorAll('.history-peek-processes li')],rowRects=rows.slice(0,3).map(row=>[...row.querySelectorAll('bdi,.history-peek-process-main>span,.history-peek-process-meta>span')].map(node=>{const ink=document.createRange();ink.selectNodeContents(node);const box=ink.getBoundingClientRect();return [box.x,box.y,box.width,box.height]}).flat()).flat();frames.push({t:Math.round(now-start),x:r.x,y:r.y,w:r.width,h:r.height,rowRects,rows:rows.length,scrollWidth:scroll?.clientWidth,scrollTop:scroll?.scrollTop,scrollHeight:scroll?.clientHeight,trackWidth:scroll?.offsetWidth-scroll?.clientWidth,layer:getComputedStyle(surface).willChange,mask:scroll&&getComputedStyle(scroll).maskImage})}if(now-start<900)requestAnimationFrame(read);else resolve(frames)}requestAnimationFrame(read)})});
   await chart.click({position:{x,y:95}});
   const frames=await target.evaluate(()=>window.frameCapture);
   await fs.writeFile(path.join(out,`frames-${scale}-${label}.json`),JSON.stringify(frames,null,2));
   assert(frames.length>3,`${label} ${scale} did not capture visible frames`);
   const last=frames.at(-1);assert.equal(last.rows,9);
   const first=frames[0];assert.equal(first.rows,9,`${label} ${scale} painted an incomplete popover: ${first.h} → ${last.h} CSS px`);
   assert(first.trackWidth>0&&first.rowRects.length>30,'fixture must include multiple clipped paths and a reserved scrollbar');
   for(const frame of frames){
    for(const key of ['x','y','w','h','scrollWidth','scrollTop','scrollHeight','trackWidth'])assert(Math.abs(frame[key]-first[key])*scale<.05,`${label} ${scale}: ${key} changed ${first[key]} → ${frame[key]}`);
    assert.deepEqual(frame.rowRects,first.rowRects,`${label} ${scale}: process glyph geometry shifted`);
    assert.equal(frame.layer,first.layer,`${label} ${scale}: text and scrollbar moved between composited layers while visible`);
   }
   await target.locator('.chart-selection-popover').screenshot({path:path.join(out,`snapshot-${scale}-${label}.png`)});
  }
  await chart.hover({position:{x:425,y:95}});
  await chart.locator('.chart-tooltip').waitFor();
  await capture('uncached',430);
  assert.equal(await chart.locator('.chart-tooltip').count(),0,'hover tooltip overlapped the snapshot');
  await target.keyboard.press('Escape');await target.locator('.chart-selection-popover').waitFor({state:'detached'});
  await capture('cached',430);
  const popup=target.locator('.chart-selection-popover');
  const old=await popup.boundingBox();
  await chart.click({position:{x:80,y:195}});
  const exit=await popup.boundingBox();
  assert(exit&&Math.abs(exit.x-old.x)<.05&&Math.abs(exit.y-old.y)<.05,'old snapshot moved while exiting');
  await popup.waitFor({state:'detached'});
  await popup.waitFor({state:'visible'});
  assert.equal(await popup.locator('.history-peek-processes li').count(),9);
  assert(Math.abs((await popup.boundingBox()).x-old.x)>10,'new snapshot did not move to its own point');
  for(const [x,dismiss] of [[690,()=>target.keyboard.press('Escape')],[290,()=>target.getByRole('heading',{name:'历史记录'}).click()]]){
   await chart.click({position:{x,y:195}});
   await target.getByRole('status').filter({hasText:'正在读取快照'}).waitFor();
   await dismiss();
   await target.waitForTimeout(430);
   assert.equal(await popup.count(),0,'a dismissed asynchronous snapshot reopened');
  }

  console.log(`PASS ${scale*100}%: shell and process text stable from first visible frame, cold and cached; cross-point fade and pending dismiss`);
 }finally{await target.close()}
}
async function range(name){await page.getByRole('combobox',{name:'历史时间范围',exact:true}).click();await page.getByRole('option',{name,exact:true}).click()}
try{
 for(const scale of [1,1.25])await frameAudit(scale);
 if(!process.argv.includes('--frames-only')){
 await page.goto(url);await page.getByText('1200 个已存档采样',{exact:false}).waitFor();
 await range('最近 24 小时');await page.getByText('43200 个已存档采样',{exact:false}).waitFor();assert.equal(await page.locator('.history-trend-viewport').getAttribute('data-history-minutes'),'1440');const count24=await page.evaluate(()=>window.calls.filter(c=>c.method==='history.query'&&c.params.minutes===1440).length);const before=performance.now();await page.getByRole('button',{name:'GPU',exact:true}).click();await page.waitForFunction(()=>document.querySelector('.history-trend-viewport').dataset.historyMetric==='gpu');assert(performance.now()-before<500);assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.method==='history.query'&&c.params.minutes===1440).length),count24);console.log('PASS 24h uses bounded real-summary response and metric switch does not requery');
 await page.locator('.history-trend-panel[aria-hidden="false"] .chart').click({position:{x:401,y:100}});await page.locator('.chart-selection-popover[data-motion-state="open"]').waitFor();await page.waitForTimeout(100);const crisp=await page.locator('.chart-selection-popover').evaluate(el=>{const rect=el.getBoundingClientRect(),d=devicePixelRatio,peek=el.querySelector('.history-peek');return {x:rect.left*d,y:rect.top*d,snapped:el.dataset.pixelSnapped,filter:getComputedStyle(peek).filter,font:getComputedStyle(peek).fontFamily,bodyFont:getComputedStyle(document.body).fontFamily,readingFont:getComputedStyle(peek.querySelector('.history-peek-reading strong')).fontFamily}});assert.equal(crisp.snapped,'true');assert(Math.abs(crisp.x-Math.round(crisp.x))<.02&&Math.abs(crisp.y-Math.round(crisp.y))<.02);assert.equal(crisp.filter,'none');assert.equal(crisp.readingFont,crisp.font);await page.screenshot({path:path.join(out,'snapshot-popover.png')});await page.getByRole('button',{name:'关闭时间快照',exact:true}).click();console.log('PASS history snapshot rests at physical pixels with inherited font and no foreground filter');
 await range('最近 1 小时');await range('最近 24 小时');assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.method==='history.query'&&c.params.minutes===1440).length),count24);console.log('PASS recent ranges reuse bounded fresh cache');
 await range('自定义时间');await page.getByRole('textbox',{name:'开始时间',exact:true}).fill('2026-11-03 09:00');await page.getByRole('textbox',{name:'结束时间',exact:true}).fill('2026-11-03 08:00');const customBefore=await page.evaluate(()=>window.calls.filter(c=>c.method==='history.query'&&c.params.fromUtc).length);await page.getByRole('button',{name:'应用时间',exact:true}).click();await page.getByText('结束时间必须晚于开始时间。',{exact:true}).waitFor();assert.equal(await page.evaluate(()=>window.calls.filter(c=>c.method==='history.query'&&c.params.fromUtc).length),customBefore);console.log('PASS invalid custom range cannot reach native query');
 await page.getByRole('textbox',{name:'结束时间',exact:true}).fill('2026-11-03 10:30');await page.getByRole('button',{name:'应用时间',exact:true}).click();await page.waitForFunction(()=>window.calls.some(c=>c.method==='history.query'&&c.params.toUtc-c.params.fromUtc===5400000));await page.getByRole('button',{name:'选择开始时间',exact:true}).click();await page.getByRole('dialog',{name:'开始时间选择器'}).waitFor();await page.screenshot({path:path.join(out,'calendar.png')});await page.getByRole('combobox',{name:'开始时间小时'}).click();await page.getByRole('option',{name:'07',exact:true}).click();assert((await page.getByRole('textbox',{name:'开始时间',exact:true}).inputValue()).includes('07:00'));await page.getByRole('button',{name:'完成',exact:true}).click();await page.screenshot({path:path.join(out,'custom-range.png')});console.log('PASS themed calendar and time selector update editable local-time fields');
 await page.evaluate(()=>window.mountDevice('B'));await page.getByRole('combobox',{name:'历史时间范围',exact:true}).waitFor();await page.evaluate(()=>window.mountDevice('A'));await page.getByRole('textbox',{name:'开始时间',exact:true}).waitFor();assert.equal(await page.getByRole('textbox',{name:'开始时间',exact:true}).inputValue(),'2026-11-03 07:00');await page.reload();await page.getByRole('textbox',{name:'开始时间',exact:true}).waitFor();assert.equal(await page.getByRole('textbox',{name:'开始时间',exact:true}).inputValue(),'2026-11-03 07:00');console.log('PASS device switch and renderer reload preserve custom range and draft');
 await range('最近 1 小时');await page.evaluate(()=>{window.slow=true;window.calls=[]});await range('最近 24 小时');await page.getByRole('status').filter({hasText:'正在读取存档'}).waitFor();await range('最近 15 分钟');await page.waitForFunction(()=>window.calls.some(c=>c.method==='history.cancel'));await page.waitForTimeout(1000);assert.equal(await page.locator('.history-trend-viewport').getAttribute('data-history-minutes'),'15');console.log('PASS leaving a slow native range sends identity-specific cancel and ignores late result');
 assert.deepEqual(errors,[]);await fs.writeFile(path.join(out,'result.json'),JSON.stringify({passed:7,pageErrors:errors},null,2));
 }
}finally{await browser.close();await new Promise(r=>server.close(r))}
