# AsterHub Windows 工作台预览

当前版本 **0.3.0-preview.6**。这是 Windows WPF + WebView2 迁移候选，与 `desktop/` 稳定版并行；Ubuntu 保持独立版本。

## 模块

- `host/`：WPF 窗口、WebView2 桥接、托盘、真实终端与文件传输、更新与反馈。
- `web/src/native/`：React 工作台、资源详情、历史、偏好设置和交互。
- `backend/src/`：Core 数据模型、Storage 档案、Windows/SSH 采样与身份核验。
- `app/runtime`、`app/resources/terminal`：有许可证的 SSH.NET / WebView2 / xterm 分发依赖。
- `installer/`：Inno Setup。预览与稳定版分别注册；QAInstall 使用第三个独立 AppId。

## 构建

Windows x64，需要 .NET Framework 4.7.2+ 与 Node.js；运行时需要 Microsoft WebView2 Evergreen Runtime。

```powershell
cd desktop-preview/web
npm ci
cd ..
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

程序位于 `app/AsterHub.Preview.exe`，需要整个 app 载荷目录；不要单独复制 EXE。安装器额外需要 Inno Setup 和带有效微软签名的 WebView2 引导程序，通过 `installer/build.ps1 -Compiler <ISCC路径> -Bootstrapper <引导程序路径>` 指定。签名私钥不在仓库中。

默认用户数据保存在 `%LOCALAPPDATA%/AsterHub/Preview1`，与稳定版隔离。升级会保留同一预览通道的配置与档案，不会自动迁移稳定版凭据。

## 本版边界

原生终端复用 ConPTY/SSH；当前实际交互回归覆盖本机 PowerShell，SSH 需在目标服务器验证。双栏内部拖放为复制，支持文件和文件夹；Windows 外部拖入无法安全取得完整路径时使用文件/文件夹选择器。撤销仅删除本次新建且未变化的文件，不恢复已覆盖文件。

检查更新使用发行方维护的固定预览地址，验证 RSA 签名、安装包长度与 SHA256；下载确认后退出程序、安装并重新启动。活动传输应先完成或取消。反馈仅提交用户填写内容和版本平台信息。
