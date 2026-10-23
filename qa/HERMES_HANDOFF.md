# AQ Server：给 Hermes 的交接

用户明确要求：Ubuntu 版必须安装后真正使用，反馈缺陷并由 Codex 修复、发布、复测；官网同步支持下载。用户会通过微信来问你，不需要主动发送微信。

## 当前版本与下载

- v0.1.2，早期预览；源代码提交 `7833da05d6fe64514abb00546296e570a914a086`。
- 官网：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/
- Ubuntu：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/AQServer-0.1.2-ubuntu-all.deb
- Ubuntu SHA256：`5f9af8cdacef91df16458c5b72d8114e7784443ab657fe164d0d98c8375381f0`，194504 字节。
- Windows：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/AQServer-Setup-0.1.2.exe
- Windows SHA256：`314e7299c378d2a660883fb19e5da848181850ba9d8f34d2f8dedc827d9ffc53`，9314467 字节。
- 公开签名清单：`releases/ubuntu.json`、`releases/stable.json`；校验表：`releases/SHA256SUMS.txt`。
- GitHub：1159112519/aq-server（私有仓库）。问题 #2 是拒绝上传的队列缺失，#3 是深色下拉框；以私有 status/issue-map.json 与 status/ubuntu-issue-queue-map.json 的 ID 映射为准。原始IP公网入口问题仍是 #1。

本次已由 Codex 在外网禁用代理完整下载两个安装包，校验15项网站资源和签名清单对应的散列/大小。网址是临时 Cloudflare Quick Tunnel 预览，重启可能改变；以 `qa/status/public-preview.json` 及当前 `aqserver-web-preview.service` InvocationID 日志为准。不要称为永久域名。

原始 `http://8.140.49.4/aqserver/` 的公网TCP80仍有已知阻塞；Windows正式更新地址仍使用该IP。预览站可下载不等于正式自动更新入口已修好；临时域名没有写入正式安装包或签名清单。

## 已安装的 Ubuntu 工作台

服务器 Ubuntu 22.04.5 amd64；包已真实通过 apt/dpkg 安装、卸载和重装，配置/访问密钥/历史SQLite在卸载与重装后保持原样。程序为模块化目录 `/usr/lib/aq-server/`、资源 `/usr/share/aq-server/`、入口 `/usr/bin/aq-server`、应用菜单与用户服务模板。

测试专用用户 `aqserver-qa`，真实服务 `aqserver-ubuntu-qa.service`，只监听 `127.0.0.1:19871`。配置 `/var/lib/aq-server-qa/config/aq-server`，档案 `/var/lib/aq-server-qa/data/aq-server/history.sqlite3`；fixture与证据分别在该专用根的 `fixtures/`、`evidence/`。不得公开访问密钥或带key的URL。预设 aq-server 已用原用户授权会话连接，实际124逻辑处理器/8GPU。不要索取或重置凭据；密码只在运行内存里，重启后的恢复由Codex受控 helper处理。

桌面启动 `aq-server --desktop`，无桌面可 `aq-server --serve`。普通用户后台采集可 `systemctl --user enable --now aq-server`。Linux初版不提供原生托盘、自动包升级、物理拖出桌面等完整兼容；Ubuntu24.04桌面及其他架构尚未验收。不能把远程被监控主机的Ubuntu版本当成客户端兼容性验收。

## 真实测试与证据

首次Hermes真正操作3个原生GTK/WebKit会话，166条动作、23张截图：`qa/status/ubuntu-hermes-0.1.2.json`。发现两项问题后，Codex已复现、修复，Linux10项运行回归通过。最后安装包的原生修复验证为31动作/4截图：`/var/lib/aq-server-qa/evidence/final-fixes/actions.json`。GTK图标加载问题也已在真实新包启动验证。

最终Hermes复测请求 `qa/status/ubuntu-hermes-retest-request.md`，结果应写 `qa/status/ubuntu-hermes-retest-0.1.2.json`。此处的请求和Codex操作不能替代Hermes自己的结果。只有版本、包散列、提交及实际动作/截图对应才能关闭问题。

驱动 `/var/lib/aq-server-qa/native_driver.py`，通过 GTK/WebKit + Xvfb 对已安装程序实际输入和点击。计划是输入，actions.json和PNG才是结果；虚拟显示不等于物理鼠标/触屏/桌面拖拽验收。报告需列出未测项。

## 持续测试与协作

按 `aq-server-qa` 技能提交复现报告，`python3 /root/aq-server/qa/report_bug.py report.json` 支持 ubuntu/windows/website。队列 `qa/inbox`、私有证据 `qa/evidence`、Issue映射 `qa/status/issue-map.json`、待复测 `qa/status/retest-requests.json`、结果 `qa/retests/<Issue>.json`。保留其他业务/训练/服务，文件和进程操作只用自建fixture/进程。

必须真正使用Ubuntu应用并持续报告问题；HTTP200、收到通知、排队或仅阅读说明不能作为验收。保留模型及认证配置，遇到权限、提供商限流或环境问题如实记录，禁止绕过。Ubuntu结论也不能代替Windows验收。
