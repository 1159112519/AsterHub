# 开发指南

## Windows 程序

- Windows 10 1809+ / x64，.NET Framework 4.7.2+。
- Node.js 22.12+ / npm；前端锁文件为 `desktop/web/package-lock.json`，用 `npm ci` 安装。
- `desktop/build.ps1` 使用系统 `Framework64/v4.0.30319/csc.exe` 编译当前后端和宿主。
- `desktop/app/runtime` 是固定版本的可再分发依赖，`desktop/app/licenses` 包含许可证。终端资源位于 `desktop/app/resources/terminal`。

```powershell
cd desktop/web
npm ci
cd ..
./build.ps1
```

程序、前端、字体、模块清单及 SHA-256 清单位于 `desktop/app/`。构建只写此源码目录的输出，不替换安装版。运行测试时使用独立数据根：

```powershell
./app/AsterHub.exe --data-root "$env:TEMP/AsterHub-source-check" --smoke "$env:TEMP/AsterHub-source-check"
```

先退出该测试实例再复用同一测试目录，不清理真实用户数据。

## 定向验证

`python scripts/verify-source.py` 检查正式版本、程序集元信息、官方更新清单以及源码目录一致性。前端测试和基础组件检查从 `desktop/web` 执行；原生回归脚本位于 `desktop/scripts/bugfix-tests` 和 `desktop/backend/tests`。脚本只覆盖其名称所指范围，不能代替全部实机验收。

## 安装包

安装器使用 Inno Setup 7+，并要求有效 Microsoft 签名的 WebView2 引导器。明确传入本机工具路径：

```powershell
./desktop/installer/build.ps1 -Compiler 'C:/tools/Inno Setup 7/ISCC.exe' -Bootstrapper 'C:/tools/MicrosoftEdgeWebview2Setup.exe' -OutputDirectory './artifacts'
```

`-QAInstall` 使用独立测试安装标识。不要在真实安装目录验证 QA 包。

发布签名入口为 `desktop/scripts/sign-release.ps1`；签名私钥必须在仓库之外，不能提交。应用检查更新保留既有签名算法、公开密钥及官方 `website/public/releases/windows-preview.json` 路径。同一版本的已发布安装包及签名清单不可随意改写。

版本入口为根目录和 `desktop/VERSION`；后续版本修改使用 `desktop/scripts/set-desktop-release-version.py`。发布时同时更新程序、安装包、公开说明和应用内更新源，并核对同一版本与安装包散列。

## 源码结构

| 目录 | 职责 |
| --- | --- |
| `desktop/host` | Windows 窗口、原生桥接、任务栏监测与更新 |
| `desktop/backend` | 资源采样、SSH、历史存档与设置 |
| `desktop/web` | 工作台界面与公共组件 |
| `desktop/installer` | 安装、升级与卸载 |
| `desktop/scripts` | 打包、签名与定向验证 |
| `agent/linux` | 远端 Linux 常驻采集代理 |
| `website` | 官网与公开更新清单 |
| `deployment/feedback` | 反馈接收服务 |

数据格式、安装升级及更新协议的兼容约定见 [兼容性说明](COMPATIBILITY.md)。功能分支使用 `feature/`，修复分支使用 `fix/`。不提交凭据、个人设备配置、签名私钥或生成的安装包。
