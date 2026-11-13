// Static installed-CSS regression: never starts/connects to AsterHub or controls the desktop.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {pathToFileURL} from 'node:url';
const args=process.argv.slice(2),arg=name=>{const i=args.indexOf(name);return i<0?null:args[i+1]};
assert.ok(arg('--css'),'--css is required');
const out=path.resolve(arg('--out')||'qa/footer-css');await fs.mkdir(out,{recursive:true});
const css=await fs.readFile(arg('--css'),'utf8'),override=arg('--override')?await fs.readFile(arg('--override'),'utf8'):'';
const modulePath=process.env.ASTERHUB_PLAYWRIGHT_MODULE||path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const {chromium}=await import(pathToFileURL(modulePath).href);
const browser=await chromium.launch({channel:'msedge',headless:true});
const results=[],failures=[],errors=[];
try{
 const context=await browser.newContext({viewport:{width:1048,height:712},deviceScaleFactor:1.125});const page=await context.newPage();
 page.on('pageerror',e=>errors.push(e.message));await page.route('**/*',r=>r.abort());
 const icon='<svg class="footer-resource-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"><rect x="4" y="4" width="16" height="16" rx="3"/></svg>';
 const number=(v,u)=>`<span class="number"><span>${v}</span><small> ${u}</small></span>`;
 const line=(color,bounds=false)=>`<svg class="sparkline" viewBox="0 0 200 28" preserveAspectRatio="none"><path d="M0 24L20 23L32 10L46 24L70 16L83 24L120 21L144 17L162 23L200 22" fill="none" stroke="${color}" stroke-width="1.5"/>${bounds?'<path d="M0 1H200M0 27H200" stroke="gray" opacity=".3"/>':''}</svg>`;
 const card=(id,name,label,main,aux,unit)=>`<button aria-label="查看${name}" class="footer-metric footer-${id}${['network','storage'].includes(id)?' footer-throughput':''}"><div class="footer-metric-heading">${icon}<span>${name}</span></div><div class="footer-reading-row"><strong>${label?`<span class="rate-arrow">${label}</span>`:''}${number(main,unit)}</strong><span class="footer-trend-slot" aria-hidden="true">${['network','storage'].includes(id)?`<span class="footer-network-curves">${line('var(--amber)',true)}${line('var(--blue)')}</span>`:line('var(--blue)',true)}</span></div><span class="footer-aux">${id==='storage'?'<span class="rate-arrow">W</span>':id==='network'?'↑ ':''}${number(aux,unit)}</span></button>`;
 const cases=[{name:'screenshot',network:'23.8',storage:'126.4',aux:'363.3',unit:'KiB/s'},{name:'zero',network:'0',storage:'0',aux:'0',unit:'B/s'},{name:'large',network:'999.9',storage:'999.9',aux:'999.9',unit:'MiB/s'}];
 for(const width of[850,900,960,1048,1100,1181,1250,1251,1360,1600])for(const collapsed of[true,false])for(const sample of cases){
  await page.setViewportSize({width,height:712});
  const cards=card('cpu','CPU','','27.0','4.68','%')+card('gpu','GPU','','10.0','0.7','%')+card('memory','内存','','83.6','13.1','%')+card('network','网络','↓',sample.network,'11.5',sample.unit)+card('storage','存储','R',sample.storage,sample.aux,sample.unit);
  await page.setContent(`<!doctype html><html><head><style>${css}\n${override}</style></head><body><div class="app-shell native-workbench preview5-workbench${collapsed?' sidebar-collapsed':''}"><aside class="sidebar"></aside><div class="workspace"><header class="topbar">GPU</header><main class="main-content"><p>静态排版夹具，非运行程序</p></main><div class="resource-footer-fold"><div class="resource-footer-clip"><footer class="resource-footer preview5-resource-footer">${cards}</footer></div></div></div></div></body></html>`);
  await page.evaluate(()=>document.getAnimations().forEach(a=>a.finish()));
  const r=await page.evaluate(()=>{
   const rect=n=>{const b=n.getBoundingClientRect();return{x:b.x,y:b.y,w:b.width,h:b.height,bottom:b.bottom}};
   const card=id=>{const n=document.querySelector('.footer-'+id);return{box:rect(n),heading:rect(n.querySelector('.footer-metric-heading')),reading:rect(n.querySelector('.footer-reading-row')),curve:rect(n.querySelector('.footer-trend-slot')),aux:rect(n.querySelector('.footer-aux')),labels:[...n.querySelectorAll('.rate-arrow')].map(rect),grid:getComputedStyle(n.querySelector('.footer-reading-row')).gridTemplateRows}};
   return{network:card('network'),storage:card('storage')};
  });
  r.width=width;r.collapsed=collapsed;r.sample=sample.name;results.push(r);
  for(const key of['heading','reading','curve','aux']){
   const a=r.network[key],b=r.storage[key];
   if(Math.abs((a.y-r.network.box.y)-(b.y-r.storage.box.y))>.75)failures.push(`${width}/${collapsed}/${sample.name}: ${key} vertical offset differs by ${((b.y-r.storage.box.y)-(a.y-r.network.box.y)).toFixed(2)}px`);
   if(Math.abs(a.h-b.h)>.75)failures.push(`${width}/${collapsed}/${sample.name}: ${key} height ${a.h}px vs ${b.h}px`);
  }
  for(const id of['network','storage'])for(const label of r[id].labels)if(label.x<r[id].box.x-.5||label.x+label.w>r[id].box.x+r[id].box.w+.5||label.bottom>r[id].box.bottom+.5)failures.push(`${width}/${collapsed}/${sample.name}: ${id} direction label clipped`);
  if(width===1048&&collapsed&&sample.name==='screenshot')await page.locator('.resource-footer').screenshot({path:path.join(out,'footer-1048.png')});
 }
 await context.close();
}finally{await browser.close()}
await fs.writeFile(path.join(out,'results.json'),JSON.stringify({cases:results.length,failures,errors,results},null,2));
console.log(JSON.stringify({cases:results.length,failureCount:failures.length,examples:failures.slice(0,8),errors},null,2));
assert.equal(errors.length,0,'No JavaScript errors');assert.equal(failures.length,0,'Network/storage footer alignment');
