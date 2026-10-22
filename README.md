# AQ Server

Windows 本机与 SSH 服务器资源监控工作台。当前版本 **0.1.0**，属于早期测试版本。

- 多核 CPU、多 GPU、内存监控，支持没有 GPU 的设备。
- 已连接设备持续采集，历史保存在本机；切换设备不影响采集。
- GPU 历史进程快照、用户名、进程结束确认。
- 内嵌终端、双栏文件浏览、复制传输及传输进度。
- 多主题、迷你置顶窗口、托盘、开机启动及签名更新清单。

## 目录

- `desktop/`：WPF / C# 5 / .NET Framework 源码，含本地依赖与自动验证入口。
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

完整发布见 `docs/RELEASING.md`。私钥、密码、SSH 密钥、运行配置与历史档案不属于源代码。

## 测试协作

Hermes 测试员通过服务器缺陷队列提交，Codex 同步 GitHub Issues、修复、验证和发布，最后由 Hermes 复测。见 `docs/QA-WORKFLOW.md`。GitHub Actions 检查网站和 Windows 受控回归；它不等同于客户端真机交互验收。

## 发布状态

v0.1.0 为早期预览，尚未完成跨机器长期稳定性验证。更新清单使用 RSA-SHA256 签名，安装器尚无 Windows Authenticode 代码签名。项目源代码当前仅供仓库授权人员访问；第三方组件按其各自许可证分发，见 `desktop/TerminalAssets` 和 `desktop/deps`。
