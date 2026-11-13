import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import http from 'node:http';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {chromium} from 'file:///C:/Users/lenovo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../..');
const out=path.resolve(process.env.ASTER_ANALYSIS_EVIDENCE||path.join(root,'../artifacts/analysis-followup-evidence'));
await fs.mkdir(out,{recursive:true});
const {build}=createRequire(path.join(root,'web/package.json'))('esbuild');
await build({stdin:{contents:`import React from 'react';import {createRoot} from 'react-dom/client';import {ResourceAnalysis} from './src/native/analysis/ResourceAnalysis.jsx';import {installMotionTokens} from './src/motion.js';import {setLanguage} from './src/i18n/index.js';import './src/foundation/tokens.css';import './src/styles.css';import './src/foundation/ui.css';import './src/native/native.css';import './src/foundation/control-alignment.css';installMotionTokens();const root=createRoot(document.getElementById('root'));window.select=(metric,device='local')=>root.render(metric?<ResourceAnalysis key={metric+device} metric={metric} deviceId={device}/>:null);window.language=setLanguage;`,resolveDir:path.join(root,'web'),loader:'jsx'},bundle:true,external:['/fonts/*'],outfile:path.join(out,'analysis-ui.js')});
const mock=`const listeners=[];window.analysisReads=[];window.chrome={webview:{addEventListener(_t,f){listeners.push(f)},removeEventListener(){},postMessage(m){if(m.method!=='analysis.query')return;analysisReads.push(m.params);const from=1700000000000,to=from+400000;const points=Array.from({length:101},(_,i)=>({time:from+i*4000,interval:4,cpu:i%30,memory:i%30,mem:(3000+i)+' / 8192 MiB',processes:i%3===0?[{Pid:42,Identity:'worker',Name:'worker',Path:'C:/apps/worker.exe',Cpu:1+i%10,ResidentBytes:2000000+i*1000}]:null,gpus:[{Index:0,Uuid:'a',Name:'GPU A',Used:100+i,Total:2000},{Index:1,Uuid:'b',Name:'GPU B',Used:200+i,Total:3000}],gpuProcesses:[{Gpu:0,Pid:42,IdentityToken:'same',Name:'python',Path:'/usr/bin/python',Memory:100+i},{Gpu:1,Pid:42,IdentityToken:'same',Name:'python',Path:'/opt/project/python',Memory:200+i}],adapters:[{Id:'lan',Name:'LAN',ReceivedBytes:i*90000000,SentBytes:i*500,DownloadBytesPerSecond:4000+i,UploadBytesPerSecond:2000,ReceiveErrors:0,SendErrors:0,ReceiveDiscards:0,SendDiscards:0}]}));setTimeout(()=>listeners.forEach(f=>f({data:{id:m.id,ok:true,result:{from,to,points}}})),220)}}};`;
const html=`<!doctype html><html lang="en"><meta charset="utf-8"><link rel="stylesheet" href="/fonts/interface-fonts.css"><link rel="stylesheet" href="/analysis-ui.css"><style>body{margin:20px;background:var(--bg);color:var(--text)}#root{max-width:1300px;margin:auto}</style><div id="root"></div><script>${mock}</script><script src="/analysis-ui.js"></script></html>`;
const server=http.createServer(async(req,res)=>{try{if(req.url==='/'){res.setHeader('Content-Type','text/html; charset=utf-8');res.end(html);return;}const name=decodeURIComponent(req.url.split('?')[0]);const file=name.startsWith('/fonts/')?path.join(root,'web/public',name):path.join(out,name);res.setHeader('Content-Type',name.endsWith('.css')?'text/css':name.endsWith('.js')?'text/javascript':'application/octet-stream');res.end(await fs.readFile(file));}catch{res.statusCode=404;res.end()}});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
let browser;
try{
 browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1400,height:1000}}),errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 await page.goto(`http://127.0.0.1:${server.address().port}`);await page.waitForFunction(()=>typeof window.select==='function');
 await page.evaluate(()=>window.language('en'));await page.evaluate(()=>document.fonts.ready);
 const label=page.locator('[data-selection]');
 for(const metric of ['gpu','memory','network','cpu']){
  await page.evaluate(metric=>window.select(metric),metric);
  await page.locator('.ev-traffic-plot').waitFor();
  await page.locator('.ev-app').first().waitFor();
  const target=metric==='cpu'?page.locator('[data-ev-marker="end"] [data-ev-hit]'):page.locator('[data-ev-band]');
  const box=await target.boundingBox();assert.ok(box&&box.width>10,metric);
  const before=await label.innerText();
  const from=box.x+box.width/2,y=box.y+box.height/2;
  await page.mouse.move(from,y);await page.waitForTimeout(45);const hit=await page.evaluate(({from,y})=>document.elementFromPoint(from,y)?.outerHTML.slice(0,220),{from,y});assert.match(hit,metric==='cpu'?/data-ev-hit/:/data-ev-band/,metric+': pointer targets chart overlay');await page.mouse.down();await page.mouse.move(from-74,y,{steps:12});
  const during=await label.innerText();assert.notEqual(during,before,`${metric}: ranking updates before pointerup`);
  if(metric==='memory')assert.equal(await page.locator('.ev-app').count(),1,'nearest recorded process is shown while dragging');
  await page.mouse.up();assert.equal(await label.innerText(),during,`${metric}: pointerup does not select another point`);
  if(metric==='gpu'){
   await page.getByRole('combobox',{name:/GPU/i}).click();await page.getByRole('option',{name:'All GPUs'}).click();
   assert.equal(await page.locator('.ev-gpu-tag').count(),2);assert.equal(await page.locator('.process-executable-path[title="/opt/project/python"]').count(),1);
  }
  if(metric==='network'){assert.match(await page.locator('.chart-axis-unit').textContent(),/KiB\/s/);assert.match(await page.locator('.ev-resource-volume').innerText(),/GiB/);}
  await page.screenshot({path:path.join(out,`${metric}-en.png`)});
  const count=await page.evaluate(()=>analysisReads.length);
  await page.evaluate(()=>window.select(null));await page.locator('.ev-traffic-plot').waitFor({state:'detached'});
  await page.evaluate(metric=>window.select(metric),metric);await page.locator('.ev-traffic-plot').waitFor();
  assert.equal(await page.locator('.ev-analysis-notice').count(),0);
  assert.equal(await page.evaluate(()=>analysisReads.length),count,`${metric}: warm reopen does not call the bridge`);
 }
 assert.deepEqual(errors,[]);console.log('All four analysis tools: live drag, pointer-up consistency, warm cache, GPU badges/paths, adaptive network units.');
}finally{await browser?.close();await new Promise(resolve=>server.close(resolve));}
