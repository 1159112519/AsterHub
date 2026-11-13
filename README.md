# AsterHub

AsterHub 是 Windows 上的多设备资源工作台：在同一应用中查看 CPU、GPU、内存、网络与存储，分析历史区间，使用终端、文件管理和任务栏监测。

当前 Windows 版本：**0.4.3.5**。本仓库的 `main` 维护正式程序源码。

## 下载

在 [GitHub Releases](https://github.com/1159112519/AsterHub/releases/latest) 获取安装包与版本说明。已安装用户也可使用应用内「检查更新」。Windows 10 1809+、x64、.NET Framework 4.7.2+；安装器会检查 Microsoft Edge WebView2 Runtime。

## 源码结构

| 目录 | 内容 |
| --- | --- |
| `desktop/host` | Windows 宿主、原生桥接、任务栏监测、更新及文件操作 |
| `desktop/backend` | 资源采样、SSH、存档与设置 |
| `desktop/web` | 正式工作台界面及公共组件 |
| `desktop/installer` | 安装、升级与卸载脚本 |
| `desktop/scripts` | 打包、签名与定向回归检查 |
| `agent/linux` | 可选的远端 Linux 采样代理 |
| `website` | 官网与公开更新清单 |
| `deployment/feedback` | 反馈接收服务 |

旧 AQServer 独立桌面程序、旧 Linux 图形程序及单独的 Preview 演示入口已移除。当前程序只有一个 Windows 源码入口；仍需兼容的旧数据及更新标识见 [兼容性说明](docs/COMPATIBILITY.md)。

## 构建 Windows 程序

需要 Windows、Node.js 22.12+（含 npm）和系统 .NET Framework C# 编译器：

```powershell
cd desktop/web
npm ci
cd ..
./build.ps1
```

输出为 `desktop/app/AsterHub.exe`。此目录同时包含运行所需资源和模块，不能只复制 EXE。依赖程序集、终端资源及许可证随源码提供；不需要从旧程序目录复制构建产物。构建不会更改已安装软件及其用户数据。

安装包构建及发布说明见 [BUILDING.md](docs/BUILDING.md)。

## 校验与贡献

```powershell
python scripts/verify-source.py
cd desktop/web
npm run test:foundation
npm run check:foundation
```

功能分支使用 `feature/`，修复分支使用 `fix/`。请通过 Pull Request 提交修改，不提交凭据、个人设备配置、签名私钥或生成的安装包。

## 许可

本项目尚未授予通用开源许可证。源码公开不等于获得复制、修改或商业分发授权。第三方组件遵循各自许可证，见 [THIRD-PARTY.md](docs/THIRD-PARTY.md) 及随附许可文件。
