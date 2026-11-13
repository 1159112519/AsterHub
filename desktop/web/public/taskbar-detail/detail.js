'use strict';
(() => {
 const panel=document.getElementById('detail'), body=document.getElementById('detail-content');
 const escape=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const send=value=>window.chrome?.webview?.postMessage(value);
 const available=v=>v!=null&&String(v).trim()!==''&&!/^(?:—|–|-|N\/A|NaN|undefined|null)(?:\s|$)/i.test(String(v).trim());
 let lang="zh-CN";const words={"下载":"Download","上传":"Upload","显存":"VRAM","内存构成":"Memory composition","容量":"Capacity","逐核负载":"Per-core load","暂无逐核采样":"No per-core samples","60 秒前":"60 seconds ago","最近60秒趋势":"Last 60-second trend","关闭详情":"Close details","监测详情":"Monitor details","在 AsterHub 中查看":"View in AsterHub"};const t=s=>lang==="en"?(words[s]??s):s;
 let last=null,session=0,lastHeight=0;
 const action=type=>send({type,session});
 function size(){if(!session||panel.hidden)return;const height=Math.ceil(panel.offsetHeight);if(height&&height!==lastHeight){lastHeight=height;send({type:'size',height,session});}}
 function summary(m){const value=v=>available(v?.text)?`<div class="detail-value">${escape(v.text)}<small>${escape(v.unit)}</small></div>`:'';
  if(!available(m.main?.text)&&(!m.dual||!available(m.secondary?.text)))return '';
  return m.dual?`<div class="detail-summary detail-dual">${available(m.main?.text)?`<div><span>↓ ${t('下载')}</span>${value(m.main)}</div>`:''}${available(m.secondary?.text)?`<div><span>↑ ${t('上传')}</span>${value(m.secondary)}</div>`:''}</div>`:`<div class="detail-summary">${value(m.main)}<div class="detail-state"><span><i></i>${escape(m.device)}</span><span>${escape(m.window)}</span></div></div>`;
 }
 function chart(m){const series=(m.series||[]).map(rows=>{
  const runs=[];let segment=[];
  for(const point of rows){
   if(point.breakBefore&&segment.length){runs.push(segment);segment=[];}
   if(Number.isFinite(point.value)&&Number.isFinite(point.time))segment.push(point);
  }
  if(segment.length)runs.push(segment);
  return runs;
 });
  if(!series.some(a=>a.length))return '';
  const start=m.end-60000,cap=Math.max(1,m.ceiling);
  const x=p=>Math.min(294,Math.max(0,(p.time-start)*294/60000));
  const y=p=>6+(1-Math.min(1,Math.max(0,p.value/cap)))*87;
  const svg=series.map((runs,i)=>runs.map(points=>{
   const color=i?'var(--second)':'var(--plot)',first=points[0],last=points.at(-1);
   if(points.length===1)return `<circle cx="${x(first)}" cy="${y(first)}" r="2" fill="${color}"/>`;
   const d=points.map((p,j)=>`${j?'L':'M'}${x(p).toFixed(2)},${y(p).toFixed(2)}`).join(' ');
   return `<path class="fill" style="color:${color}" d="${d} L${x(last)} 94 L${x(first)} 94Z"/><path class="line" style="color:${color}" d="${d}"/>`;
  }).join('')).join('');
  return `<svg class="detail-chart" viewBox="0 0 294 100" preserveAspectRatio="none" aria-label="${escape(m.title)} ${t('最近60秒趋势')}"><path class="grid" d="M0 7H294M0 50H294M0 94H294"/>${svg}</svg><div class="chart-caption"><span>${t('60 秒前')}</span><span>${escape(m.scale)}</span></div>`;
 }
 function extra(m){if(m.kind==='cpu'){if(!m.cores?.some(Number.isFinite))return '';return `<h3 class="detail-subtitle">${t('逐核负载')}</h3>${m.cores.length?'<div class="core-grid">'+m.cores.map((v,i)=>Number.isFinite(v)?`<div class="core-column"><div class="core-track"><i style="height:${Math.max(0,Math.min(100,v))}%"></i></div><span>C${i+1}</span><b>${Math.round(v)}%</b></div>`:'').join('')+'</div>':'<p class="detail-note">'+t('暂无逐核采样')+'</p>'}`;}
  const legend=(m.legend||[]).filter(r=>available(r.value));if(!legend.length)return '';const titles={gpu:'显存',memory:'内存构成',storage:'容量'},pct=Math.max(0,Math.min(100,(m.fraction??0)*100));
  return `<h3 class="detail-subtitle">${t(titles[m.kind]||'')}</h3>${m.fraction==null?'':`<div class="memory-bar"><i style="width:${pct}%;background:var(--plot)"></i><i style="flex:1;background:var(--hairline)"></i></div>`}<div class="memory-legend">${legend.map((r,i)=>`<span><i style="background:${i%2?'var(--second)':'var(--plot)'}"></i>${escape(r.label)} ${escape(r.value)}</span>`).join('')}</div>`;
 }
 function render(m){lang=m.language||"zh-CN";document.documentElement.lang=lang;document.getElementById("detail-close").setAttribute("aria-label",t("关闭详情"));last=m;document.body.dataset.theme=m.dark?'dark':'light';document.documentElement.style.setProperty('--detail-body-limit',`${m.bodyLimit??620}px`);document.documentElement.style.setProperty('--panel-width',`${m.width??330}px`);document.getElementById('detail-title').textContent=m.title;panel.setAttribute('aria-label',m.title);const scroll=body.scrollTop;
  const rows=(m.rows||[]).filter(r=>available(r.value));
  body.innerHTML=summary(m)+chart(m)+(rows.length?`<dl class="stats">${rows.map(r=>`<div class="stat"><dt>${escape(r.label)}</dt><dd>${escape(r.value)}</dd></div>`).join('')}</dl>`:'')+extra(m)+`<p class="detail-note">${escape(m.subject||m.device)} · ${escape(m.window)}</p>`;body.scrollTop=scroll;panel.hidden=false;size();
 }
 window.taskbarDetail={render,position:x=>{document.documentElement.style.setProperty('--panel-x',`${x.x}px`);document.documentElement.style.setProperty('--panel-y',`${x.y}px`);document.documentElement.style.setProperty('--origin',x.origin||'bottom');}};
 document.getElementById('detail-close').addEventListener('click',()=>action('close'));
 document.addEventListener('keydown',e=>{if(e.key==='Escape')action('close');});
 document.addEventListener('pointerdown',e=>{if(!e.target.closest('#detail'))action('close');});
 document.querySelector('.detail-head').addEventListener('contextmenu',e=>{e.preventDefault();let menu=panel.querySelector('.detail-menu');if(menu){menu.remove();return;}menu=document.createElement('button');menu.className='detail-menu';menu.textContent=t('在 AsterHub 中查看');menu.onclick=()=>action('open');panel.append(menu);});
 new ResizeObserver(size).observe(body);Promise.all([document.fonts.load('13px Harmony'),document.fonts.load('13px Inter')]).then(()=>document.fonts.ready).then(()=>{send({type:'ready',dpr:devicePixelRatio,fonts:document.fonts.check('13px Harmony')&&document.fonts.check('13px Inter')});if(last)size();});
 window.chrome?.webview?.addEventListener('message',e=>{if(e.data.type==='render'){if(e.data.session!==session){session=e.data.session;lastHeight=0;body.scrollTop=0;panel.querySelector('.detail-menu')?.remove();}render(e.data.value);}if(e.data.type==='position')window.taskbarDetail.position(e.data.value);});
})();
