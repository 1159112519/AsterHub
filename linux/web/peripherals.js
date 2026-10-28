import {$,esc,bytes,capacity,percent,animateNumber} from './ui.js';
import {Chart} from './charts.js';
const charts={};
export const rate=value=>value==null?'—':bytes(value)+'/s';
export function initPeripherals(){
 const dock=$('.resource-dock');new ResizeObserver(entries=>dock.classList.toggle('two-rows',entries[0].contentRect.width<1040)).observe(dock);
 for(const [id,isNetwork] of [['network',true],['storage',false]]){
  $('#view-'+id).innerHTML=`<article class="card"><div class="peripheral-head"><div><div class="metric-label">${isNetwork?'下载':'系统分区已用'}</div><div id="${id}-primary" class="metric">—</div></div><div><div class="metric-label">${isNetwork?'上传':'已用 / 总容量'}</div><div id="${id}-secondary" class="metric secondary">—</div></div></div><div class="chart-wrap"><canvas id="${id}-chart"></canvas></div><p class="hardware-model" id="${id}-caption"></p></article><article class="card"><div class="card-heading"><h3>${isNetwork?'活动网络接口':'分区与卷'}</h3></div><div id="${id}-rows" class="peripheral-rows"></div></article>`;
  charts[id]=new Chart($('#'+id+'-chart'),isNetwork?{lines:[{key:'download',color:'--accent',label:'下载'},{key:'upload',color:'--blue',label:'上传'}],dynamic:true,format:rate,windowSeconds:300}:{lines:[{key:'storage',color:'--blue',label:'存储'}],windowSeconds:300});
 }
}
function reading(node,value,format){animateNumber(node,value,1,format);}
function updateRows(id,rows){const parent=$('#'+id+'-rows'),key=rows.map(x=>x[0]).join('|');if(parent.dataset.key!==key){parent.dataset.key=key;parent.innerHTML=rows.length?rows.map(([name])=>`<div class="peripheral-row"><strong title="${esc(name)}">${esc(name)}</strong><span></span></div>`).join(''):'<p class="subtle">暂未读取到数据</p>';}
 [...parent.querySelectorAll('.peripheral-row > span')].forEach((node,i)=>node.textContent=rows[i][1]);}
export function updatePeripherals(device){
 const sample=device.sample,network=sample?.network,volume=sample?.volumes?.find(x=>x.path===(sample.primaryStoragePath||'/'))||sample?.volumes?.[0];
 reading($('#network-primary'),network?.receive,rate);reading($('#network-secondary'),network?.send,rate);
 $('#network-caption').textContent=network?.receive==null?'等待下一次计数采样':'最近 5 分钟 · 下载 / 上传';
 reading($('#storage-primary'),volume?.percent,percent);$('#storage-secondary').textContent=volume?bytes(volume.used)+' / '+bytes(volume.total):'—';
 $('#storage-caption').textContent=volume?`${volume.path} · ${volume.fileSystem} · 可用 ${bytes(volume.available)}`:'等待容量信息';
 updateRows('network',(network?.interfaces||[]).map(x=>[x.name,`↓ ${rate(x.receive)}　↑ ${rate(x.send)}`]));
 updateRows('storage',(sample?.volumes||[]).map(x=>[`${x.path} · ${x.fileSystem}`,`${bytes(x.used)} / ${bytes(x.total)} · ${percent(x.percent)}\n可用 ${bytes(x.available)}`]));
 for(const chart of Object.values(charts))chart.set(device.series||[]);
 reading($('#dock-upload'),network?.send,v=>'↑ '+rate(v).replace(' ',''));reading($('#dock-download'),network?.receive,v=>'↓ '+rate(v).replace(' ',''));
 reading($('#dock-storage'),volume?.percent,percent);$('#dock-capacity').textContent=volume?capacity(volume.used,volume.total):'—';
 $('#dock-storage-meter').style.setProperty('--usage',(volume?.percent||0)+'%');
 $('#dock-network-group').title=network?.receive==null?'等待下一次网络计数采样':`上传 ${rate(network.send)}\n下载 ${rate(network.receive)}`;
 $('#dock-storage-group').title=volume?`${volume.path} · ${volume.fileSystem}\n可用 ${bytes(volume.available)}`:'等待容量信息';
}
export function repaintPeripherals(){for(const chart of Object.values(charts))chart.paint();}