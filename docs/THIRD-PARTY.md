# 第三方组件

依赖的精确版本由 `desktop/web/package-lock.json`、`desktop/app/licenses/sshnet-dependencies-sha256.json` 和随附依赖记录确定。

- WebView2 SDK、SSH.NET、BouncyCastle 及其 .NET 依赖：`desktop/app/licenses/`。
- xterm.js、addon-fit、addon-search、WebView2 终端许可及固定版本记录：`desktop/app/resources/terminal/`。
- HarmonyOS Sans：`desktop/web/public/fonts/HarmonyOS-Sans-LICENSE.txt`。
- Inter：字体目录内 OFL 许可；任务栏字体构建脚本及来源保留在 `desktop/scripts/`。
- Lucide、React、GSAP、Vite 及其他 npm 包：使用锁文件固定版本，并保留安装包自带许可；发布前按实际分发文件核验义务。

第三方授权不授予 AsterHub 自有源码的通用许可。仓库不包含签名私钥，不能用公开密钥生成正式签名。
