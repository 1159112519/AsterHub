# AQ Server：给 Hermes 的交接

用户要求：官网部署在本机 8.140.49.4；用户随后通过微信联系 Hermes。请使用 AQ Server 测试员角色，和 Codex 按 GitHub 缺陷 → 修复 → 发布 → 复测协作。

## 入口

- 官网：http://8.140.49.4/aqserver/
- 安装包：http://8.140.49.4/aqserver/releases/AQServer-Setup-0.1.0.exe
- 更新清单：http://8.140.49.4/aqserver/releases/stable.json
- GitHub：https://github.com/1159112519/aq-server （私有仓库）
- 当前程序版本：0.1.0。
- 测试员技能：`aq-server-qa`，全局文件 `/root/.hermes/skills/aq-server-qa/SKILL.md`。
- 协作协议：`/root/aq-server/QA_WORKFLOW.md`。

## 已部署与未通过的边界

Nginx 和静态站点已部署；网站位于 `/var/www/aqserver-web/current/`。服务器通过 localhost + Host:8.140.49.4 可以获得 HTTP 200，安装包哈希匹配已签名版本清单。

**公网 TCP 80 暂未打通**：Windows 直连超时，经过本机代理曾返回 502；服务器对自身公网 IP 的连接也超时。需要核查阿里云安全组/云防火墙的入站 TCP 80。不要把“已部署”说成“公网已验收”。目前没有拿到阿里云控制台/开放 API 的访问能力，不能声称已修改云侧规则。请用户通过微信联系时告知这一实际阻塞；若用户在微信提供已有云管理入口，可以继续核查本实例。

Windows 安装包已经做本地编译、十组回归、原生托盘/主题/终端检查、隔离安装运行卸载；官网构建和站点包测试通过，但尚未完成浏览器多尺寸视觉验收。Linux 无法直接执行 WPF Windows 客户端，不要把网络请求或静态截图当作客户端真机操作。

## 缺陷提交与复测

按技能说明写报告 JSON，然后执行：

`python3 /root/aq-server/qa/report_bug.py /path/to/report.json`

队列 `/root/aq-server/qa/inbox/`；证据 `/root/aq-server/qa/evidence/`；Codex 同步到 GitHub 后写 `/root/aq-server/qa/status/issue-map.json`。修复发布后查看 `qa/status/retest-requests.json`，把复测结果写到 `qa/retests/<issue号>.json`。没有 GitHub 登录也可以先用队列，不需复制任何 GitHub token。

不影响其他 Hermes 任务和训练进程；测试文件、结束进程等仅对自己的测试目录与进程执行。当前交接只要求接收信息并记住入口，不要求立即运行耗时测试。
