# AsterHub

在一个工作台中查看电脑与远端服务器的资源状态，分析占用变化，管理文件和终端会话。

**Windows 10 / 11 · 当前版本 0.4.3.5 · 中文 / English**

[下载安装](https://github.com/1159112519/AsterHub/releases/latest) · [查看更新](CHANGELOG.md) · [问题反馈](https://github.com/1159112519/AsterHub/issues)

## 主要功能

| 功能 | 可以做什么 |
| --- | --- |
| 资源监测 | 集中查看 CPU、GPU、内存、网络与存储的实时状态和历史趋势。 |
| 历史分析 | 选择时间区间，对比资源变化与进程占用；多显卡可分别查看或汇总分析。 |
| 任务栏小组件 | 自定义指标、排列、颜色与数字动效，不打开主窗口也能查看常用读数。 |
| 存储工具 | 分析磁盘空间、查找大文件，将文件移入回收区，并按需恢复或清理。 |
| 网络工具 | 查看实时速率、分析区间流量，进行网络测速。 |
| 终端与文件 | 连接 SSH 设备，使用终端、浏览文件并传输文件。 |
| 任务中心 | 集中查看下载更新、文件传输和扫描任务的进度与结果。 |

## 安装与更新

在 [GitHub Releases](https://github.com/1159112519/AsterHub/releases/latest) 下载 Windows x64 安装包，按提示完成安装。已安装用户可在应用内选择「检查更新」；下载完成后点击「安装并重启」即可更新。

系统要求：Windows 10 1809 或更新版本、64 位系统、.NET Framework 4.7.2 或更新版本。安装器会检查 Microsoft Edge WebView2 Runtime。

远端 Linux 主机可通过 SSH 连接；如需桌面退出后继续采集历史，可配置常驻采集代理。具体监测项目取决于设备、驱动与访问权限。

## 开发与贡献

Windows 程序源码位于 `desktop/`。构建步骤、目录结构与验证方法见 [开发指南](docs/BUILDING.md)，第三方组件说明见 [THIRD-PARTY.md](docs/THIRD-PARTY.md)。

欢迎通过 [Issues](https://github.com/1159112519/AsterHub/issues) 反馈问题，或通过 Pull Request 提交改进。

## 许可

本项目尚未授予通用开源许可证。源码公开不等于获得复制、修改或商业分发授权。第三方组件遵循各自许可证，详见 [第三方组件说明](docs/THIRD-PARTY.md) 及随附许可文件。
