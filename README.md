# AsterHub

Windows / Ubuntu 本机与 SSH 服务器资源监控工作台。Windows 新工作台公开预览 **0.3.0-preview.4**，稳定版保留 **0.2.1**，Ubuntu 保留 **0.1.3** 预览版。两个平台独立发版，后续 Ubuntu 不随 Windows 同步更新。

- 多核 CPU、多 GPU、内存、网络上下行和分区容量监控，支持没有 GPU 的设备。
- 已连接设备持续采集，历史保存在本机；切换设备不影响采集。
- GPU 历史进程快照、用户名、进程结束确认。
- 内嵌终端、双栏文件浏览、复制传输及传输进度。
- 多主题、迷你置顶窗口、托盘、开机启动及签名更新清单；Windows 支持一键更新并重启。
- Windows 支持密码、空密码、私钥及 SSH 配置连接；密码可由当前 Windows 账户加密保存。
- 关于页面可提交问题反馈并获得编号，连接密码、终端内容和历史数据不会自动上传。

新的 Windows 工作台源码与构建说明见 [desktop-preview/](desktop-preview/README.md)。预览与稳定通道分开更新、独立保存数据。

## 官网

[访问 AsterHub 官网](https://spotlight-ruled-slightly-void.trycloudflare.com/asterhub/)（当前为临时预览域名）。

滚动浏览资源概览、GPU、终端、文件、网络、存储与历史，界面随章节切换。电脑采用固定展示区，平板和手机采用顺序图文。展示图来自当前 Windows 预览前端，使用示例数据。

## 下载

- [Windows 预览版 0.3.0-preview.4](https://github.com/1159112519/aq-server/releases/tag/v0.3.0-preview.4)
- [Windows 稳定版安装包与免安装版](https://github.com/1159112519/aq-server/releases)
- [Ubuntu Debian 安装包](https://github.com/1159112519/aq-server/releases/tag/ubuntu-v0.1.3)

请在Release的Assets中选择对应平台的正式安装包。GitHub自动生成的Source code压缩包是源码，不是可直接安装的软件。

## 目录

- `linux/`：Ubuntu .deb、原生 GTK/WebKit 入口、本机服务与 Linux/SSH 采集。安装和能力边界见 `linux/README.md`。
- `desktop/`：五个独立项目的 WPF / C# 5 / .NET Framework 解决方案；核心、存储、Windows 服务、界面和启动入口分离。
- `website/`：React 18、TypeScript、Vite、Tailwind、Lucide；字体 Inter / Playfair Display。
- `installer/`：Inno Setup 安装定义和运行时依赖下载脚本。
- `scripts/`：构建、打包、签名发布与验证。
- `docs/`：设计审查、数据位置、发布和部署说明。

## 开发

Windows 10 1809 或以上，x64，.NET Framework 4.7.2 或以上。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File desktop/build.ps1
cd website
npm ci
npm run dev
```

终端使用系统 WebView2 Evergreen Runtime。安装器在检测不到时通过微软引导程序安装，需要网络；免安装版本也需要此运行时。

构建输出为 `desktop/bin/Release/AQServer.exe` 及其组件目录。安装器和目录 ZIP 都交付完整文件夹，不再生成单 EXE 自解压版。架构见 `docs/ARCHITECTURE.md`，完整发布见 `docs/RELEASING.md`。私钥、密码、SSH 密钥、运行配置与历史档案不属于源代码。

## 更新日志与问题反馈

各版本新增功能、优化、修复与升级注意事项发布在 [GitHub Releases](https://github.com/1159112519/aq-server/releases)；完整记录见 [CHANGELOG.md](CHANGELOG.md)。Windows 与 Ubuntu 分别维护版本。反馈问题时请提供平台、版本和复现步骤，并遮盖私人信息。本仓库公开提供项目代码、版本说明及正式发行包。

## 发布状态

Windows v0.3.0-preview.4 / Ubuntu v0.1.3 为早期预览，尚未完成跨机器长期稳定性验证。更新清单使用 RSA-SHA256 签名，安装器尚无 Windows Authenticode 代码签名。项目尚未指定开源许可证；第三方组件按其各自许可证分发，见 `desktop/assets/terminal` 和 `desktop/deps`。
