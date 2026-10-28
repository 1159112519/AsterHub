import {$,$$,esc,api,modal,icons,toast,confirmAction,errorHandler} from './ui.js';
import {updateTerminalSettings} from './terminals.js';
let getState,refresh;
export function initSettings(state,reload){getState=state;refresh=reload;$('#settings-button').onclick=showSettings;$('#add-device').onclick=addDevice;$('#about-button').onclick=about;}
export function showSettings(){
 const state=getState(),s=state.settings;
 const deviceRows=state.devices.filter(d=>d.type==='ssh').map(d=>`<div class="field"><label>${esc(d.name)}<small>${esc(d.username)}@${esc(d.host)}:${esc(d.port)}</small></label><button class="button connect-device" data-id="${esc(d.id)}">${d.status==='connected'?'连接设置':'连接'}</button></div>`).join('');
 const node=modal('偏好设置',`
  <nav class="resource-tabs settings-tabs" aria-label="设置分组">
   <button data-settings-tab="connections" class="active" aria-selected="true">连接</button>
   <button data-settings-tab="appearance" aria-selected="false">外观</button>
   <button data-settings-tab="terminal" aria-selected="false">终端</button>
   <button data-settings-tab="recording" aria-selected="false">记录</button>
  </nav>
  <section class="settings-section" data-settings-panel="connections">${deviceRows||'<p class="subtle">尚未添加远程设备</p>'}<button id="settings-add-device" class="button"><i data-lucide="plus"></i>添加设备</button></section>
  <section class="settings-section" data-settings-panel="appearance" hidden>
   <div class="field"><label>主题<small>工作台与终端保持一致</small></label><select id="theme-setting"><option value="light">浅色 · 纸面</option><option value="dark">暖灰 · 工作台</option><option value="dusk">暮色 · 暖紫</option></select></div>
   <div class="field"><label>更新频率<small>所有已连接设备持续采集</small></label><select id="interval-setting">${[1,2,3,5,10,15,30,60].map(n=>`<option value="${n}">${n} 秒</option>`).join('')}</select></div>
  </section>
  <section class="settings-section" data-settings-panel="terminal" hidden>
   <div class="field"><label>终端字体</label><select id="terminal-family">${['monospace','DejaVu Sans Mono','Noto Sans Mono','Liberation Mono'].map(n=>`<option>${n}</option>`).join('')}</select></div>
   <div class="field"><label>终端字号</label><input id="terminal-size" type="number" min="10" max="24" value="${s.terminalFontSize}"></div>
  </section>
  <section class="settings-section" data-settings-panel="recording" hidden>
   <div class="field"><label>历史保留天数<small>只清理超出范围的旧记录</small></label><input id="retention-setting" type="number" min="1" max="3650" value="${s.retentionDays}"></div>
   <p class="subtle">档案位置：<br><span class="archive-path">${esc(state.archive.path)}</span></p>
  </section>
  <div class="form-error" id="settings-error" hidden></div>`,
  '<button class="button modal-close">取消</button><button class="button" id="apply-settings">应用</button><button class="button primary" id="save-settings">确定</button>');
 $('#theme-setting').value=s.theme;$('#interval-setting').value=String(s.interval);$('#terminal-family').value=s.terminalFontFamily;
 for(const tab of $$('[data-settings-tab]',node))tab.onclick=()=>{
  for(const button of $$('[data-settings-tab]',node)){const active=button===tab;button.classList.toggle('active',active);button.setAttribute('aria-selected',String(active));}
  for(const panel of $$('[data-settings-panel]',node))panel.hidden=panel.dataset.settingsPanel!==tab.dataset.settingsTab;
 };
 async function save(close){try{
  const next={...s,theme:$('#theme-setting').value,interval:Number($('#interval-setting').value),terminalFontFamily:$('#terminal-family').value,terminalFontSize:Number($('#terminal-size').value),retentionDays:Number($('#retention-setting').value)};
  await api('/api/settings',next);Object.assign(s,next);document.body.dataset.theme=s.theme;updateTerminalSettings(s);await refresh();toast('设置已应用');if(close)node.close();
 }catch(e){$('#settings-error').hidden=false;$('#settings-error').textContent=e.message;}}
 $('#apply-settings').onclick=()=>save(false);$('#save-settings').onclick=()=>save(true);$('#settings-add-device').onclick=addDevice;
 $$('.connect-device',node).forEach(b=>b.onclick=()=>connectDevice(b.dataset.id));icons(node);
}
export function addDevice(){const node=modal('添加 SSH 设备',`<div class="field"><label>设备名称</label><input id="new-name" placeholder="例如：训练服务器"></div><div class="field"><label>IP 或主机名</label><input id="new-host" required placeholder="192.168.1.10"></div><div class="field"><label>SSH 端口</label><input id="new-port" type="number" min="1" max="65535" value="22"></div><div class="field"><label>用户名</label><input id="new-user" autocomplete="username" required></div><div class="field block"><label>私钥路径<small class="subtle">可选，填写此 Ubuntu 用户可读取的密钥路径</small></label><input id="new-key" placeholder="~/.ssh/id_ed25519"></div><p class="subtle">设备连接后持续采集。密码仅保留在当前运行会话，不写入配置文件。</p><div id="device-error" class="form-error" hidden></div>`,'<button class="button modal-close">取消</button><button id="save-device" class="button primary">添加设备</button>');$('#save-device').onclick=async()=>{const button=$('#save-device');button.disabled=true;try{const data=await api('/api/devices',{action:'add',name:$('#new-name').value,host:$('#new-host').value,username:$('#new-user').value,port:Number($('#new-port').value),keyPath:$('#new-key').value});node.close();await refresh();connectDevice(data.device.id);}catch(e){$('#device-error').hidden=false;$('#device-error').textContent=e.message;}finally{button.disabled=false;}};}
export function connectDevice(id){const device=getState().devices.find(d=>d.id===id);if(!device)return;const node=modal(device.name+' · SSH 连接',`<div class="release-copy"><strong>${esc(device.username)}@${esc(device.host)}:${esc(device.port)}</strong><p>${device.keyPath?'密钥：'+esc(device.keyPath):'优先使用 SSH Agent 或默认密钥。也可以输入本次会话密码。'}</p></div><div class="field"><label>会话密码<small>可留空使用密钥，不写入磁盘</small></label><input id="ssh-password" type="password" autocomplete="off"></div><div id="connect-feedback" class="subtle"></div>`,`${device.enabled?'<button id="disconnect-device" class="button danger">断开连接</button>':''}<button class="button modal-close">取消</button><button id="connect-device" class="button primary">测试并连接</button>`);$('#connect-device').onclick=async()=>{const password=$('#ssh-password').value;$('#ssh-password').value='';const button=$('#connect-device');button.disabled=true;$('#connect-feedback').textContent='正在连接…';try{let result;try{result=await api('/api/devices',{action:'connect',id,password});}catch(e){if(e.detail?.code!=='host-key')throw e;const approved=await confirmAction('确认 SSH 主机身份',`首次连接 ${device.host}，主机指纹为 ${e.detail.fingerprint}。确认与服务器提供的指纹一致后继续。`,'信任并连接');if(!approved)return;result=await api('/api/devices',{action:'connect',id,password,fingerprint:e.detail.fingerprint});}node.close();toast(`连接成功 · ${result.connectMs} ms`);await refresh();}catch(e){if($('#connect-feedback'))$('#connect-feedback').textContent=e.message;else toast(e.message);}finally{button.disabled=false;}};if($('#disconnect-device'))$('#disconnect-device').onclick=errorHandler(async()=>{await api('/api/devices',{action:'disconnect',id});node.close();await refresh();toast('已断开；已有历史档案保留');});setTimeout(()=>$('#ssh-password')?.focus(),100);}
function about(){const state=getState();modal('关于 AQ Server',`<div style="display:flex;align-items:center;gap:18px"><img src="/assets/icon.ico" alt="" width="52" height="52"><div><h3 style="font-size:23px;font-weight:550">AQ Server</h3><p class="subtle">v${esc(state.version)} · Ubuntu 版</p></div></div><p class="release-copy">本机与 SSH 服务器的资源工作台。监控与历史保存在本机，终端使用真实 Linux shell，文件传输保留源文件。</p><p class="release-copy">Ubuntu 22.04 为本次实际验证环境。桌面使用 GTK / WebKit，无桌面服务器使用同一个本地工作台。窗口关闭后采集服务继续运行。</p><p class="subtle">第三方组件：aiohttp、Paramiko、xterm.js、Lucide。许可证随安装包提供。Linux 原生托盘及系统自动升级尚未提供。</p>`);}
