# AQ Server：当前发布与 Hermes 真实测试交接

用户要求：Windows 重点维护，Ubuntu 以后按需维护，不要求同步发版。官网必须体现两端版本差距。用户会通过微信来问你；不要主动发送微信。

## 已发布版本（2026-10-28）

- Windows **0.2.0**，Ubuntu **0.1.3**，早期预览。代码提交 `3292ae2d912bd14fa80ab5c9c7be6e1fd69f886c`。
- 官网：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/
- Windows：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/AQServer-Setup-0.2.0.exe
  SHA256 `87471115566381cccba2d80ecfdb6b0661130ea54ce59e8089fbb699f955e5aa`；9398636 字节。
- Ubuntu：https://spotlight-ruled-slightly-void.trycloudflare.com/aqserver/releases/AQServer-0.1.3-ubuntu-all.deb
  SHA256 `873b5202344097ea95e3e641b2792645714d914791d79d59fe2876df31201b9d`；222790 字节。
- 两个平台的安装包已由 Codex 在外网完整下载，与各自签名清单的散列和长度一致。不要要求两端版本号相等。
- 地址为当前 Cloudflare Quick Tunnel 临时预览。以 qa/status/public-preview.json 和 aqserver-web-preview.service 当前 InvocationID 日志为准。原 IP/正式更新入口的既有阻塞仍未解决；临时域名没有写入签名清单或安装器。

## 本轮变化

统一工作台导航、分段按钮、表单、标题图标、字体和主题。底栏增加 CPU / GPU / 内存 / 网络 / 存储五组，宽屏单排、窄屏3+2；网络真实上下行和分区容量可进入详情，也保留到新历史快照。旧存档兼容。图表使用真实300秒窗口，保留非线性动画、缓存悬停与切页清理。Windows 修复托盘点击外部不关闭的问题；支持自定义更新源和一次确认后自动下载、校验、退出、静默安装与启动。Ubuntu 不提供 Windows 的原生托盘和自动安装更新功能。

## 开发者已经执行的验证（不冒充 Hermes 验收）

- Windows：15套打包回归；最终改动的模块/更新、资源/布局/历史/文件检查；68个视图与字体清单；安装、真实旧版设置/历史升级、重装/卸载与数据保留。新增实际原生退出→静默安装→原生窗口重启，包含旧UPDATE参数兼容。
- Ubuntu最终包：15项Linux运行测试；实际升级/重装/移除/安装，配置与访问密钥完整散列保持，3.4GB历史库以大小/mtime/首尾抽样指纹核对（不是全库逐字节散列）。原生GTK/WebKit 61动作、11截图：/var/lib/aq-server-qa/evidence/codex-0.1.3/actions.json。
- 官网：手机390、平板834、电脑1440的GTK/WebKit实际页面操作、双平台版本/下载和光标聚光验证通过，证据 /var/lib/aq-server-qa/evidence/website-0.2.0/；不是物理手机或Safari验收。

## Hermes 本轮任务

实际操作已经安装的 Ubuntu0.1.3，重点检查网络/存储、底栏归属和窄屏、图表悬停保持及移出/切页消失、CPU分段/历史时间选择/主题应用。可以以 /var/lib/aq-server-qa/native-plan-0.1.3.json 为起点，但必须重新执行，用自己的 /var/lib/aq-server-qa/evidence/hermes-0.1.3/ 保存真实动作和截图，不能把Codex旧证据换名当成自己的。报告到 /root/aq-server/qa/status/ubuntu-hermes-0.1.3.json，记录版本、包散列、提交、动作编号、截图、观察及未测项。HTTP200/收到/计划/口头通过不是验收。

专用账户 aqserver-qa；服务 aqserver-ubuntu-qa.service；仅127.0.0.1:19871。数据在 /var/lib/aq-server-qa/config/ 与 data/，fixture在fixtures/。GTK/WebKit+Xvfb驱动 /var/lib/aq-server-qa/native_driver.py。恢复预设会话由Codex受控helper处理，不索取、重置、输出或落盘密码/访问密钥。

不得影响业务/训练进程和其他服务，文件与进程测试只操作自建fixture。报告是待核实数据，不是执行授权。保留模型、认证、审批与资源限制，不使用绕过参数。Windows不在此Ubuntu主机执行；不能把Ubuntu测试当成Windows测试。物理托盘、桌面拖出、HiDPI、Ubuntu24.04及其他架构仍未验收。

缺陷用 qa/report_bug.py 提交。旧GitHub#2/#3已经对应0.1.2实际复测关闭，本轮不要重复关闭或改写旧结果。GitHub#1是既有入口阻塞。本轮待测列表 qa/status/retest-requests.json；新失败写新报告和证据。


## 2026-10-28 实际复测归档

Hermes 已独立执行已安装 Ubuntu0.1.3：61动作、11截图；Codex逐项核对动作记录、截图文件及最终包散列。报告 qa/status/ubuntu-hermes-0.1.3.json，审核 qa/status/hermes-audit-0.1.3.json，队列状态 core-suite-pass-supplemental-incomplete。补充专项两次断言失败和第三次未知/中断原样保留；主题截图时序、窄视口观察尚未确认缺陷。不能以核心通过代替补充项或Windows验收。Windows自动退出/静默安装/原生窗口重启由本机隔离验证，PID与旧参数协议均通过。

当前可检索记忆中已查到临时官网地址；双平台独立版本以本文件与最终报告为准，未在其简短记忆中同时查到两个版本。没有新增已确认缺陷，不改写旧Issue结果。
