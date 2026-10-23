import {$,$$,esc,bytes,numeric,percent,animateNumber,icons,api,confirmAction,toast,timestamp} from './ui.js';
import {Chart} from './charts.js';
const state={device:null,last:0,gpuSignature:'',cores:false};
let cpuChart,memoryChart;
export function initViews(){
 $('#view-cpu').innerHTML=`<div class="view-heading"><div><h2>处理器</h2><p id="cpu-model">等待设备信息</p></div><button id="core-toggle" class="button" aria-expanded="false"><i data-lucide="layout-grid"></i><span>逐核占用</span></button></div><article class="card"><div class="card-heading"><div><div class="metric" id="cpu-number">—<small>%</small></div><div class="metric-label">当前利用率</div></div><span class="subtle" id="cpu-core-label"></span></div><div class="chart-wrap"><canvas id="cpu-main-chart"></canvas></div></article><article id="core-card" class="card" hidden><div class="card-heading"><h3>逐核占用</h3><span class="subtle">逻辑处理器</span></div><div id="core-grid" class="core-grid"></div></article><article class="card"><div class="card-heading"><h3>硬件规格</h3></div><dl id="cpu-hardware" class="hardware-grid"></dl></article><article class="card"><div class="card-heading"><h3>进程</h3><span class="subtle">按内存使用量排序</span></div><div id="cpu-processes"></div></article>`;
 $('#view-gpu').innerHTML=`<div class="view-heading"><div><h2>图形处理器</h2><p id="gpu-model">根据设备实际配置显示</p></div><span id="gpu-driver" class="subtle"></span></div><div id="gpu-cards" class="gpu-grid"></div><article class="card" style="margin-top:20px"><div class="card-heading"><h3>GPU 进程</h3></div><div id="gpu-processes"></div></article>`;
 $('#view-memory').innerHTML=`<div class="view-heading"><div><h2>内存</h2><p id="memory-total">等待设备信息</p></div></div><article class="card"><div class="metric" id="memory-number">—<small>GiB</small></div><div class="metric-label">已使用</div><div class="chart-wrap"><canvas id="memory-main-chart"></canvas></div></article><article class="card"><div class="card-heading"><h3>内存与存储</h3></div><dl id="memory-hardware" class="hardware-grid"></dl></article>`;
 cpuChart=new Chart($('#cpu-main-chart'));
 memoryChart=new Chart($('#memory-main-chart'),{lines:[{key:'memory',color:'--blue',label:'内存'}]});
 $('#core-toggle').onclick=()=>{state.cores=!state.cores;$('#core-card').hidden=!state.cores;$('#core-toggle').setAttribute('aria-expanded',String(state.cores));$('#core-toggle span').textContent=state.cores?'收起逐核':'逐核占用';};
 for(const root of ['#cpu-processes','#gpu-processes'])$(root).addEventListener('click',processAction);
 icons();
}
const specs=entries=>entries.map(([label,value])=>`<div><dt>${esc(label)}</dt><dd>${esc(value??'—')}</dd></div>`).join('');
export function processTable(sample,{gpu=false,readonly=false}={}){
 const rows=gpu?(sample?.gpuProcesses||[]):(sample?.processes||[]);
 if(!rows.length)return `<p class="subtle" style="padding:20px 0">${gpu?'当前没有可显示的 GPU 计算进程':'此时没有可读取的进程'}</p>`;
 const gpuMap=new Map((sample.gpus||[]).map(g=>[g.uuid,g.index]));
 return `<div class="table-scroll"><table class="data-table"><thead><tr>${gpu?'<th>显卡</th>':''}<th>PID</th><th>用户</th><th class="name-cell">进程</th><th>${gpu?'显存':'内存'}</th>${!readonly?'<th>操作</th>':''}</tr></thead><tbody>${rows.map(p=>`<tr>${gpu?`<td>GPU ${esc(gpuMap.get(p.gpuUuid)??'—')}</td>`:''}<td>${esc(p.pid)}</td><td>${esc(p.username)}</td><td class="name-cell" title="${esc(p.name)}">${esc(p.name)}</td><td>${gpu?(p.memoryMiB==null?'—':numeric(p.memoryMiB)+' MiB'):bytes(p.rss)}</td>${!readonly?`<td><button class="button flat danger end-process" data-pid="${esc(p.pid)}" data-start="${esc(p.startToken)}" data-name="${esc(p.name)}" ${p.startToken?'':'disabled'}>结束</button></td>`:''}</tr>`).join('')}</tbody></table></div>`;
}
async function processAction(event){const b=event.target.closest('.end-process');if(!b)return;const device=state.device;const yes=await confirmAction('结束进程',`确定结束 ${b.dataset.name} (PID ${b.dataset.pid}) 吗？这会向进程发送 SIGTERM，未保存的工作可能丢失。`,'结束进程');if(!yes)return;try{await api('/api/process/end',{device:device.configId||device.id,pid:Number(b.dataset.pid),startToken:b.dataset.start,confirm:true});toast('已发送结束请求');}catch(e){toast(e.message);}}
export function gpuCards(sample){
 const gpus=sample?.gpus||[];
 if(!gpus.length)return `<article class="card empty-state" style="grid-column:1/-1"><i data-lucide="circuit-board"></i><h3>${sample?.gpuError?'GPU 当前不可用':'此设备未检测到 NVIDIA GPU'}</h3><p>${sample?.gpuError?esc(sample.gpuError):'CPU 和内存监控、历史记录、终端与文件功能仍可正常使用。'}</p></article>`;
 return gpus.map(g=>`<article class="card gpu-card" data-gpu="${esc(g.index)}"><div class="gpu-title"><b>GPU ${esc(g.index)}</b><span title="${esc(g.name)}">${esc(g.name)}</span></div><div class="metric"><span class="gpu-number">${numeric(g.utilization,1)}</span><small>%</small></div><div class="bar-track"><div class="bar-fill" style="width:${Math.max(0,Math.min(100,g.utilization||0))}%"></div></div><div class="gpu-fields"><div><label>显存</label><strong class="gpu-memory">${numeric((g.memoryUsedMiB||0)/1024,1)} / ${numeric((g.memoryTotalMiB||0)/1024,1)} GiB</strong></div><div><label>温度</label><strong class="gpu-temp">${numeric(g.temperature)} °C</strong></div><div><label>功耗 / 限制</label><strong class="gpu-power">${numeric(g.powerWatts)} / ${numeric(g.powerLimitWatts)} W</strong></div><div><label>风扇</label><strong class="gpu-fan">${g.fanPercent==null?'不可读取':numeric(g.fanPercent)+'%'}</strong></div></div></article>`).join('');
}
export function updateViews(device,force=false){
 const sample=device?.sample;state.device=device;if(!device)return;
 if(state.id!==device.id){state.id=device.id;state.last=0;state.gpuSignature='';force=true;}
 if(!sample){if(force){$('#cpu-model').textContent=device.type==='ssh'?'连接后查看处理器与历史记录':'等待第一次采集';animateNumber($('#cpu-number'),null);animateNumber($('#memory-number'),null);cpuChart.set([]);memoryChart.set([]);$('#cpu-hardware').innerHTML='';$('#memory-hardware').innerHTML='';$('#core-grid').innerHTML='';$('#cpu-core-label').textContent='';$('#memory-total').textContent='连接后查看内存信息';$('#gpu-model').textContent='连接后查看显卡信息';$('#gpu-driver').textContent='';$('#cpu-processes').innerHTML='<p class="subtle">设备尚未连接</p>';$('#gpu-processes').innerHTML='<p class="subtle">设备尚未连接</p>';$('#gpu-cards').innerHTML='<article class="card empty-state" style="grid-column:1/-1"><i data-lucide="server"></i><h3>连接设备，查看资源</h3><p>已有历史记录始终保留在历史页面中。</p></article>';icons();}return;}
 if(!force&&sample.timestamp===state.last)return;state.last=sample.timestamp;
 const h=device.hardware||sample.hardware;
 $('#cpu-model').textContent=h.cpuModel;$('#cpu-core-label').textContent=`${h.physicalCores} 核 · ${h.logicalCpus} 逻辑处理器`;
 animateNumber($('#cpu-number'),sample.cpuPercent);cpuChart.set(device.series);
 const cpuHardware=specs([['型号',h.cpuModel],['CPU 数量',h.cpuSockets+' 个插槽'],['核心 / 线程',h.physicalCores+' / '+h.logicalCpus],[h.cpuMaxMhz?'最高频率':'当前频率',(h.cpuMaxMhz||h.cpuCurrentMhz)?numeric((h.cpuMaxMhz||h.cpuCurrentMhz)/1000,2)+' GHz':'系统未提供'],['系统',h.os],['架构',h.architecture]]);
 if($('#cpu-hardware').innerHTML!==cpuHardware)$('#cpu-hardware').innerHTML=cpuHardware;
 if($('#core-grid').children.length!==sample.perCore.length)$('#core-grid').innerHTML=sample.perCore.map((v,i)=>`<div class="core-tile"><div><span>CPU ${i}</span><strong>0%</strong></div><div class="bar-track" style="margin:10px 0 3px"><div class="bar-fill"></div></div></div>`).join('');
 [...$('#core-grid').children].forEach((node,i)=>{node.querySelector('strong').textContent=percent(sample.perCore[i]);node.querySelector('.bar-fill').style.width=sample.perCore[i]+'%';});
 $('#cpu-processes').innerHTML=processTable(sample);
 const signature=sample.gpus.map(g=>g.index+g.name).join(',')+'|'+sample.gpuError;
 if(signature!==state.gpuSignature){state.gpuSignature=signature;$('#gpu-cards').innerHTML=gpuCards(sample);icons();}
 else sample.gpus.forEach(g=>{const card=$(`[data-gpu="${g.index}"]`);if(!card)return;animateNumber($('.gpu-number',card),g.utilization);$('.bar-fill',card).style.width=(g.utilization||0)+'%';$('.gpu-memory',card).textContent=numeric(g.memoryUsedMiB/1024,1)+' / '+numeric(g.memoryTotalMiB/1024,1)+' GiB';$('.gpu-temp',card).textContent=numeric(g.temperature)+' °C';$('.gpu-power',card).textContent=numeric(g.powerWatts)+' / '+numeric(g.powerLimitWatts)+' W';$('.gpu-fan',card).textContent=g.fanPercent==null?'不可读取':numeric(g.fanPercent)+'%';});
 $('#gpu-model').textContent=sample.gpus.length?`${sample.gpus.length} 张显卡 · NVIDIA`:'当前设备';$('#gpu-driver').textContent=sample.gpus[0]?.driver?'驱动 '+sample.gpus[0].driver:'';
 $('#gpu-processes').innerHTML=processTable(sample,{gpu:true});
 animateNumber($('#memory-number'),sample.memory.used/1024**3);$('#memory-total').textContent='总容量 '+bytes(sample.memory.total);memoryChart.set(device.series);
 $('#memory-hardware').innerHTML=specs([['总容量',bytes(sample.memory.total)],['可用',bytes(sample.memory.available)],['内存类型',h.memoryType||'固件未提供'],['内存频率',h.memoryFrequency||'固件未提供'],['交换空间',bytes(sample.memory.swapTotal-sample.memory.swapFree)+' / '+bytes(sample.memory.swapTotal)],['主目录磁盘',bytes(sample.disk.used)+' / '+bytes(sample.disk.total)]]);
}
