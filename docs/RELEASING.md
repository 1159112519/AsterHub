# 构建与发布

本轮将 Windows 程序拆成独立组件，保留 .NET Framework 4.7.2/WPF，以既有功能兼容为优先。Ubuntu 客户端是后续迁移工作，当前版本不声称已支持 Linux 运行。

1. 用 `python scripts/set-version.py X.Y.Z` 统一版本，维护 CHANGELOG，运行 `python qa/check_version.py`。
2. 运行 `desktop/build.ps1`。源码在 desktop/AQServer.sln，完整运行目录在 desktop/bin/Release。
3. 运行 `scripts/build-release.ps1 -InnoCompiler C:\Tools\InnoSetup\ISCC.exe`，得到正式安装器、隔离 QA 安装器、目录 ZIP。QA 安装器不能交付用户。
4. 运行 `scripts/verify-packaged.ps1`；运行 `scripts/verify-installer.ps1 -BaselineInstaller <上一版 QA 安装器>`，验证旧目录覆盖升级、重装、终端、卸载及数据保留。
5. 验证通过后，运行 `scripts/sign-release.ps1 -SigningKey <受控私钥路径> -ReleaseDirectory <候选发布目录>`。签名私钥不可提交、公开或写入日志。
6. 带上 `-ReleaseManifest <候选目录/stable.json>` 再执行验证，确认签名版本、安装包长度及 SHA256 与程序一致。
7. 将候选签名清单、SHA256SUMS.txt、正式安装器一起放入网站构建的 releases 目录，完成网站构建与网页检查。
8. 只有明确发布时才把已验证的候选部署到服务器并切换正式清单。外网下载和散列验证通过后才能称为公开发布。

更新使用一致版本的整包安装器，不允许单独覆盖随机版本 DLL。正式 AppId 沿用原值；旧平铺布局仅清理明确的程序资产。用户配置和历史路径不变。

默认公开版本从 0.1.0 开始；本次兼容性重构为 0.1.1。后续新平台或功能批次可使用 0.2.0，正式 1.0.0 需核心流程和兼容性稳定后再决定。

完整分发物为安装 EXE、目录 ZIP、签名清单及散列、官网候选包和源码。根启动 EXE 依赖同包子目录。Windows Authenticode 与更新清单签名是不同机制；当前安装器没有 Authenticode 代码签名证书。

GitHub CI 执行编译和受控测试；本机 GUI、隔离安装、真实硬件采集和真实 SSH 环境按相应证据分别记录。CI 不自动部署到服务器。
