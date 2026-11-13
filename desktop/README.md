# AsterHub Windows

正式版本：0.4.3.5。此目录是 Windows 程序的唯一源码入口。

从 `web/` 执行 `npm ci`，返回本目录后运行 `./build.ps1`，产物为 `app/AsterHub.exe` 与同目录运行资源。完整步骤、依赖和安装包入口见 [构建说明](../docs/BUILDING.md)。

`host/` 负责 Windows 窗口和原生能力，`backend/` 负责采样与存档，`web/src/native/` 是真实界面。测试中的合成样本不代表运行时读数；缺失历史不能由当前进程或演示数据补齐。
