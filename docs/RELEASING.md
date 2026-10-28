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

## Windows automatic update handoff

The About window offers 更新并重启 for a newer signed release. After one confirmation, it downloads and verifies the package, revalidates before launching, then uses the normal application Exit path. The Inno update branch waits up to 60 seconds for that caller PID before file replacement, runs a silent progress installation in the existing directory, and launches the updated application. A failed download/validation/start leaves the client running. Installer/runtime/file errors remain visible; install logs are saved beside the downloaded package in Updates/Logs. It never uses process-name termination or closes unrelated applications explicitly. Ubuntu is unchanged and upgrades through apt.

The isolated installer lifecycle also runs an actual native caller holding the QA mutex, automatic shutdown, silent reinstall and a native restart screenshot/version/path proof. QA-only restart proof arguments are excluded from the production installer. This proves the pipeline under the isolated AppId, not future CDN availability or every external antivirus/filesystem condition.


## GitHub Releases 与公开发行

源码仓库 `1159112519/aq-server` 保持私有；公开版本说明放在独立的 `1159112519/aq-server-releases`。公开仓库只保存发行文档，不镜像源码、私有提交历史、构建产物中的PDB、凭据或内部测试记录。公众下载 GitHub 自动生成的 Source code 包只能取得公开发行文档。

每次发版必须先更新 CHANGELOG.md，对每个平台分别写明“新增功能、体验优化、问题修复、升级与已知限制”，没有对应变化的类别可省略。不写内部人员/智能体名称、任务编号、私有目录或内部验收过程；不得把本机配置修复写成所有已发布客户端都已修复。

生成公开说明：

```sh
python scripts/generate-release-notes.py 0.2.0 --platform windows --output artifacts/release-notes/windows-0.2.0.md
python scripts/generate-release-notes.py 0.1.3 --platform ubuntu --output artifacts/release-notes/ubuntu-0.1.3.md
```

在公开发行仓库提交更新后的发布文档后，基于该仓库的文档提交创建对应Release。Windows标签为vX.Y.Z，Ubuntu独立标签为ubuntu-vX.Y.Z；历史0.1.2的共同发布记录使用v0.1.2。不要拿私有源码提交作为公开仓库的target_commitish，也不要上传源码ZIP。只允许上传已批准分发且完成校验的正式安装包；QA安装器、调试符号、源码映射和内部证据不得上传。当前版本仍标为预览版。

发布后通过GitHub读取Release正文、平台、标签和附件，核对生成文件一致，并确认源码仓库仍为私有。现有版本的说明更正不改写二进制或签名；已发布包仍以各自签名清单为准。
