// Static layout regression only: never starts/connects to AsterHub or its live WebView.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import {pathToFileURL} from 'node:url';
const args=process.argv.slice(2);
const arg=name=>{const i=args.indexOf(name);return i<0?null:args[i+1]};
const cssPath=arg('--css');
assert.ok(cssPath,'--css must identify the exact installed or built stylesheet');
const out=path.resolve(arg('--out')||'qa/sidebar-css');
await fs.mkdir(out,{recursive:true});
const css=await fs.readFile(cssPath,'utf8');
const override=arg('--override')?await fs.readFile(arg('--override'),'utf8'):'';
const modulePath=process.env.ASTERHUB_PLAYWRIGHT_MODULE||path.join(os.homedir(),'.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs');
const {chromium}=await import(pathToFileURL(modulePath).href);
const browser=await chromium.launch({channel:'msedge',headless:true});
const results=[],failures=[],errors=[];
try {
 const context=await browser.newContext({viewport:{width:1048,height:712},deviceScaleFactor:1.125});
 const page=await context.newPage();
 page.on('pageerror',e=>errors.push(e.message));
 await page.route('**/*',route=>route.abort());
 const icon='<svg width="21" height="21" viewBox="0 0 24 24" fill="none" stroke="currentColor"><rect x="5" y="5" width="14" height="14" rx="2"/><path d="M9 1v4m6-4v4M9 19v4m6-4v4M1 9h4m14 0h4M1 15h4m14 0h4"/></svg>';
 const nav=(name,active=false)=>`<button class="nav-item${active?' active':''}" aria-label="${name}">${icon}<span>${name}</span></button>`;
 // Match NativeApp's structural selectors and intrinsic content; no runtime bridge or user data.
 await page.setContent(`<!doctype html><html><head><style>${css}\n${override}</style></head><body><div class="app-shell native-workbench preview5-workbench sidebar-collapsed"><aside class="sidebar"><div class="brand"><div class="brand-mark">A</div><div class="sidebar-brand-name"><strong>AsterHub</strong></div><button class="sidebar-collapse-toggle">‹</button></div><div class="collapsed-device"><button>${icon}<span>本机</span><svg></svg></button></div><div class="sidebar-section-label">资源监控</div><nav>${['总览','CPU','GPU','内存','网络','存储','历史'].map(x=>nav(x,x==='CPU')).join('')}</nav><div class="sidebar-section-label">工作空间</div><nav>${nav('终端')}${nav('文件')}</nav><div class="sidebar-bottom">${nav('偏好设置')}${nav('关于 AsterHub')}</div></aside><main class="workspace"><header class="topbar">本机 › CPU</header><section style="padding:32px;min-width:0"><h1>CPU</h1><p>静态排版回归夹具（不是运行中的程序）</p></section><footer></footer></main></div></body></html>`);
 const widths=[850,899,900,901,959,960,1048,1100,1101,1181,1299,1300,1301,1360];
 for(const width of widths)for(const collapsed of[false,true]){
  await page.setViewportSize({width,height:712});
  await page.evaluate(state=>document.querySelector('.app-shell').classList.toggle('sidebar-collapsed',state),collapsed);
  await page.evaluate(()=>document.getAnimations().forEach(a=>a.finish()));
  const r=await page.evaluate(()=>{
   const box=n=>{const r=n.getBoundingClientRect();return{x:r.x,y:r.y,w:r.width,h:r.height,right:r.right}};
   const shell=document.querySelector('.app-shell'),side=document.querySelector('.sidebar'),style=getComputedStyle(shell);
   const pseudo=getComputedStyle(side,'::before'),matrix=new DOMMatrix(pseudo.transform==='none'?undefined:pseudo.transform);
   return{width:innerWidth,collapsed:shell.classList.contains('sidebar-collapsed'),expected:shell.classList.contains('sidebar-collapsed')?84:parseFloat(style.getPropertyValue('--sidebar-expanded-width')),side:box(side),work:box(document.querySelector('.workspace')),decorativeWidth:parseFloat(pseudo.width)*matrix.a,controls:[...side.querySelectorAll('.nav-item,.collapsed-device>button')].map(n=>({name:n.getAttribute('aria-label')||n.textContent,box:box(n),icon:box(n.querySelector('svg'))}))};
  });
  results.push(r);
  if(Math.abs(r.side.w-r.expected)>1)failures.push(`${width}/${collapsed?'collapsed':'expanded'}: sidebar ${r.side.w}px, expected ${r.expected}px`);
  if(Math.abs(r.side.w-r.decorativeWidth)>1)failures.push(`${width}/${collapsed}: decorative ${r.decorativeWidth}px does not match grid ${r.side.w}px`);
  if(Math.abs(r.work.x-r.side.right)>1)failures.push(`${width}/${collapsed}: workspace is not adjacent to sidebar`);
  for(const c of r.controls){
   if(c.box.x<r.side.x-1||c.box.right>r.side.right+1)failures.push(`${width}/${collapsed}: ${c.name} overflows sidebar`);
   if(collapsed&&Math.abs(c.icon.x+c.icon.w/2-(r.side.x+r.side.w/2))>1)failures.push(`${width}/collapsed: ${c.name} icon is not centered`);
  }
  if(width===1048&&collapsed)await page.screenshot({path:path.join(out,'sidebar-1048-collapsed.png')});
 }
 await context.close();
} finally {await browser.close()}
const report={cssPath,overridePath:arg('--override'),cases:results.length,failures,errors,results};
await fs.writeFile(path.join(out,'results.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({cases:results.length,failures,errors},null,2));
assert.equal(errors.length,0,'Unexpected JavaScript errors');
assert.equal(failures.length,0,'Sidebar CSS regression failures');
