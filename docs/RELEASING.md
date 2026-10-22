# 发布 v0.1.0

保留 WPF/.NET Framework 实现。现有应用负载体积与性能没有证据要求更换语言，避免在发布准备阶段引入兼容性重写。

公开版本从 0.1.0 开始：它表达已有可用功能但仍处于早期阶段，内部 v12.x 仅是迭代序号。后续小修复 0.1.1，新功能 0.2.0；核心流程和兼容性稳定后再考虑 1.0.0。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/build-release.ps1 -InnoCompiler C:\Tools\InnoSetup\ISCC.exe
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/sign-release.ps1 -SigningKey C:\Private\release-signing-key.xml
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-packaged.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-installer.ps1
cd website
npm ci
npm run build
npm run test:sites
```

签名私钥只保存在维护者私有位置。首次构建他人无法生成同一官方签名；若自行发行，应替换 desktop/UpdatePublicKey.cs 与更新地址，并使用自己的私钥。不要把生产私钥、SSH 私钥、用户设置或历史存档提交 Git。

发布前执行 `python scripts/set-version.py 0.1.1` 同步公开版本位置，再执行 `python qa/check_version.py` 检查一致性。手动补充 CHANGELOG，检查安装器版本和下载链接；不要改写历史更新记录。

验证：精确打包 EXE 的托盘、布局、设置、终端、启动、主题、签名更新；同一负载的隔离安装、运行、卸载；真实手机/平板/桌面浏览器验收官网。QA 安装器使用独立 AppId/mutex，不改正常版开机启动，不交付用户。

最终产物：完整目录安装器、单 EXE 免安装版、目录 ZIP、署名清单和 SHA256、官网部署包、干净源码。Windows Authenticode 与更新清单签名是不同机制；当前安装器没有代码签名证书。
