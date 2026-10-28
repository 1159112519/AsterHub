# 构建与发布

本轮将 Windows 程序拆成独立组件，保留 .NET Framework 4.7.2/WPF，以既有功能兼容为优先。Ubuntu 0.1.2 提供独立 Debian 安装包和 GTK/WebKit 工作台，实际验证环境及发布流程见下方。

1. 用 `python scripts/set-version.py X.Y.Z --platform windows` 更新 Windows 版本（默认平台为 Windows），维护 CHANGELOG，运行 `python qa/check_version.py`。
2. 运行 `desktop/build.ps1`。源码在 desktop/AQServer.sln，完整运行目录在 desktop/bin/Release。
3. 运行 `scripts/build-release.ps1 -InnoCompiler C:\Tools\InnoSetup\ISCC.exe`，得到正式安装器、隔离 QA 安装器、目录 ZIP。QA 安装器不能交付用户。
4. 运行 `scripts/verify-packaged.ps1`；运行 `scripts/verify-installer.ps1 -BaselineInstaller <上一版 QA 安装器>`，验证旧目录覆盖升级、重装、终端、卸载及数据保留。
5. 验证通过后，运行 `scripts/sign-release.ps1 -SigningKey <受控私钥路径> -ReleaseDirectory <候选发布目录>`。签名私钥不可提交、公开或写入日志。
6. 带上 `-ReleaseManifest <候选目录/stable.json>` 再执行验证，确认签名版本、安装包长度及 SHA256 与程序一致。
7. 将候选签名清单、SHA256SUMS.txt、正式安装器一起放入网站构建的 releases 目录，完成网站构建与网页检查。
8. 只有明确发布时才把已验证的候选部署到服务器并切换正式清单。外网下载和散列验证通过后才能称为公开发布。

更新使用一致版本的整包安装器，不允许单独覆盖随机版本 DLL。正式 AppId 沿用原值；旧平铺布局仅清理明确的程序资产。用户配置和历史路径不变。

默认公开版本从 0.1.0 开始；Windows 兼容性重构为 0.1.1；0.1.2 加入 Ubuntu 早期预览。后续成熟功能批次可使用 0.2.0，正式 1.0.0 需核心流程和兼容性稳定后再决定。

完整分发物为安装 EXE、目录 ZIP、签名清单及散列、官网候选包和源码。根启动 EXE 依赖同包子目录。Windows Authenticode 与更新清单签名是不同机制；当前安装器没有 Authenticode 代码签名证书。

GitHub CI 执行编译和受控测试；本机 GUI、隔离安装、真实硬件采集和真实 SSH 环境按相应证据分别记录。CI 不自动部署到服务器。


## Ubuntu release

Windows and Ubuntu are independently released. `VERSION` is Windows, `linux/VERSION` is Ubuntu; the website reads `website/src/release-versions.json`. Use `python scripts/set-version.py X.Y.Z --platform ubuntu` only for an intentional Ubuntu release, or `--platform all` when both platforms are independently verified at the same new version. The default only bumps Windows. On Ubuntu run the Linux regressions, then `python3 linux/build-deb.py`. Install with `sudo apt install ./artifacts/AQServer-VERSION-ubuntu-all.deb`, operate the native GTK/WebKit window, verify archive persistence and package removal/reinstall without deleting user data, and collect Hermes actions/screenshots before claiming acceptance.

`build-deb.py` emits the .deb and an `ubuntu-VERSION.json` containing version, length, SHA256 and tested system declaration. After these checks, `scripts/sign-ubuntu-release.ps1` signs the verified package metadata using the controlled local key. Publish the .deb, signed ubuntu.json, public key and combined SHA256SUMS alongside the tested Windows installer/stable.json. Temporary tunnel URLs never enter signed manifests. Linux automatic updates are not implemented in 0.1.3; upgrades use a downloaded Debian package through apt.

Windows 0.2.0 / Ubuntu 0.1.3 add five persistent resource groups and real network/volume snapshots. The canonical update origin is unchanged; user-configured sources retain signature/hash/size verification. Temporary HTTPS preview access does not resolve canonical IP ingress.

The website shows each platform's own version, date, download and release notes. The Ubuntu preview is retained and is not required to track future Windows releases. The Ubuntu signing script validates linux/VERSION, never Windows VERSION.
