# Windows 模块结构

源码入口是 `desktop/AQServer.sln`。构建命令仍是 `desktop/build.ps1`，脚本读取每个项目的真实源文件、引用与项目依赖，输出 `desktop/bin/Release`。

| 项目 | 职责 | 安装位置 |
| --- | --- | --- |
| AQServer.Bootstrap | 稳定启动入口、原生库目录、错误提示 | AQServer.exe |
| AQServer.Core | 设备、采样、硬件、偏好设置的数据模型与数值处理 | app/modules/AQServer.Core.dll |
| AQServer.Storage | XML 设置、JSON 历史、存档队列 | app/modules/AQServer.Storage.dll |
| AQServer.Platform.Windows | Windows 本机采集、SSH/SFTP、远程操作、ConPTY、采样调度、开机启动 | app/modules/AQServer.Platform.Windows.dll |
| AQServer.Desktop | WPF 页面、主题、图表、动画、交互、终端和诊断入口 | app/AQServer.Desktop.dll |

依赖方向：Storage → Core；Platform.Windows → Core + Storage；Desktop → Core + Storage + Platform.Windows。Bootstrap 不编译引用业务项目，按固定安装路径加载 Desktop。核心和存储组件不引用 WPF、WMI 或 WinForms。

桌面项目内部按 Monitoring、History、Files、Terminal、Settings、Appearance、Interaction、Application、Diagnostics 分文件夹。硬件信息的采集与显示、进程操作与确认对话框已分开。原先 Main.cs 中的类型已分别归档，主题和监控窗口不再和存储、数据模型混在一起。

文件传输队列目前包含 WPF Dispatcher 通知，归属 Desktop/Files；SFTP 与本地文件访问在 Platform.Windows。将来迁移另一套界面时，应进一步抽出通知接口，而不是直接复用 WPF 队列。

本轮保留 .NET Framework 4.7.2 与既有 WPF 行为，以控制兼容风险。Core/Storage 保持清晰边界；0.1.2 的 Ubuntu 客户端使用下述独立 Linux 后端与原生 GTK/WebKit 入口。

## 分发目录

```text
AQ Server/
  AQServer.exe
  AQServer.exe.config
  VERSION
  components.json
  payload-manifest.json
  app/
    AQServer.Desktop.dll
    modules/
      AQServer.Core.dll
      AQServer.Storage.dll
      AQServer.Platform.Windows.dll
  runtime/
    managed/                 # SSH.NET、密码学和 WebView2 托管依赖
    win-x64/                 # WebView2 原生加载器
  resources/
    terminal/                # xterm、页面、shell 集成和许可证
    ui/                      # 图标许可与界面资源
  licenses/
  docs/
```

`AQServer.exe.config` 仅在安装目录的 app、app/modules、runtime/managed 中探测托管组件。原生 DLL 目录由启动器设置为 runtime/win-x64。资源定位不依赖当前工作目录。开机启动始终指向根目录 AQServer.exe，而不是组件 DLL。

组件跟随同一个应用版本构建；更新器继续验证签名清单、下载长度和 SHA256，再运行完整安装器。模块化不等于可以任意混用不同版本 DLL。本阶段采用整包升级，避免不完整的组件替换。


## Ubuntu edition (0.1.2)

`linux/aqserver/` separates Linux /proc + NVIDIA probing, device collectors, SQLite storage, protected HTTP/WebSocket API, real PTY/SSH sessions, filesystem transfers, and GTK/WebKit desktop hosting. `linux/web/` is the approved workbench design with reusable charts and independent files/history/terminal/preferences modules. System Python/GTK/WebKit are Ubuntu-managed runtime dependencies, avoiding an embedded Chromium runtime in the Debian package.

A per-user access key is stored mode 0600 and supplied by the native launcher; the UI removes it from its URL, uses an HttpOnly SameSite cookie, validates Host/Origin and protects mutation requests. Only loopback is bound. SSH host fingerprints require confirmation, and password credentials remain in memory. Separate enabled-device tasks append full timestamped samples to SQLite regardless of the selected page/device. Historical hardware/process data remains mounted while the next snapshot is read.

The system package owns `/usr/bin/aq-server`, `/usr/lib/aq-server`, `/usr/share/aq-server`, a desktop entry, icon, licenses and a user-service template. Per-user settings and access keys are in XDG_CONFIG_HOME/aq-server, history in XDG_DATA_HOME/aq-server. Package removal preserves these user roots. The Windows WPF projects and storage formats are not replaced by this Linux backend.
