# AQ Server：给 Hermes 的交接

用户授权：官网托管在 8.140.49.4，用户随后通过微信联系 Hermes。使用 AQ Server 测试员角色，按 GitHub 缺陷 → Codex 修复 → 验证发布 → Hermes 复测协作。

## 当前可用入口

- 外网 HTTPS 官网：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/
- 安装包：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/AQServer-Setup-0.1.0.exe
- 清单查看：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/stable.json
- GitHub：https://github.com/1159112519/aq-server （私有仓库）
- 程序版本：0.1.0。
- 测试员技能：`aq-server-qa`，`/root/.hermes/skills/aq-server-qa/SKILL.md`。
- 协作协议：`/root/aq-server/QA_WORKFLOW.md`。

## 入口状态与有效期

2026-10-22 已从外网禁用代理直连验证 HTTPS 首页、脚本、样式、字体、图片及完整安装包；安装包 9,289,732 字节，SHA256 与签名清单一致。

这是运行在本服务器的 Cloudflare Quick Tunnel 预览入口，服务 `aqserver-web-preview.service` 已启用开机运行，域名可能随入口进程重启改变，服务不承诺永久在线。最新地址看 `/root/aq-server/qa/status/public-preview.json`。如果服务重启，以当前服务 InvocationID 的 journal 中 trycloudflare.com 地址为准，不继续沿用旧的临时域名。正式长期发布仍需绑定自有域名或固定的命名隧道。

官网仍在 `/var/www/aqserver-web/current/`，通过仅监听 `127.0.0.1:18770` 的 Nginx 静态入口对外提供；没有把 Hermes、SSH 或私有测试队列放到入口里。

原始 IP `http://8.140.49.4/aqserver/` 的公网 TCP 80/443 仍然超时，确切云端拦截规则尚未查明。网站可以通过上述预览域名访问，并不代表原始 IP 入口已经修好。当前 Windows 程序的正式更新地址和已签名 stable.json 内的 installerUrl 仍引用原始 IP，因此本次不能宣称桌面自动更新已恢复；不要把临时域名悄悄写进正式安装包或重签覆盖当前清单。

## 测试与证据边界

Windows 安装包此前完成本地编译、受控回归、托盘/主题/终端检查及隔离安装卸载；GitHub Windows 和官网 CI 通过。官网尚未完成手机、平板、桌面浏览器的视觉与鼠标交互验收。Linux 上的 HTTP 检查不能当作 WPF 客户端实际操作。

## 缺陷提交与复测

按技能准备报告 JSON，运行 `python3 /root/aq-server/qa/report_bug.py /path/to/report.json`。

队列 `qa/inbox/`、证据 `qa/evidence/`、Issue 映射 `qa/status/issue-map.json`、待复测 `qa/status/retest-requests.json`、结果 `qa/retests/<issue号>.json` 均在 `/root/aq-server/` 下。没有 GitHub 登录也可先提交队列。原始 IP 问题是 Issue #1；目前有 HTTPS 预览入口缓解，但不要把未修复的正式更新链路记为通过。

保留其他 Hermes 任务和训练进程。结束进程与文件操作仅使用自建测试进程和测试目录。当前交接只要求接收新网址并保存可检索记忆，不要求立即运行完整测试或向微信主动发送消息。
