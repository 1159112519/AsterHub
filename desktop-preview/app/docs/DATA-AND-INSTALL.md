# 安装与数据

默认安装到 `%LOCALAPPDATA%\Programs\AQ Server`，无需管理员权限。0.1.1 起使用模块化目录：根目录是稳定启动入口 AQServer.exe、配置和清单；app 是桌面程序与自有组件，runtime 是第三方托管与原生依赖，resources 是终端和界面资源，licenses 与 docs 保存许可和说明。详细边界见 ARCHITECTURE.md。

推荐使用安装包。目录 ZIP 是完整的免安装版本，解压整个 AQ Server 文件夹后运行 AQServer.exe；需要一起保留全部子目录，不能只复制启动 EXE。本版不再生成单 EXE 自解压免安装包。

用户数据保持原路径：

- `%APPDATA%\AQServer`：servers.xml、偏好设置和布局。
- `%LOCALAPPDATA%\AQServer\History`：历史数据与进程快照。
- `%LOCALAPPDATA%\AQServer\Updates`：已校验的更新下载。
- `%LOCALAPPDATA%\AQServer\Logs`：启动器错误日志。
- 既有 WebView2 用户资料继续使用应用指定的数据目录。

程序文件与用户数据分离。覆盖升级和卸载不清理上述用户数据目录。安装器沿用正式 AppId，已有开机启动仍指向安装目录根部 AQServer.exe。升级 0.1.0 时仅精确清理旧版的已知根目录 DLL、TerminalAssets 和 UiAssets，保留未知文件；不会对整个安装目录执行递归清空。

运行中的程序需要退出后才能升级。更新时先验证 RSA-SHA256 清单和安装包散列，再运行完整安装器；各组件作为一个一致版本交付。后续若更改数据结构，需要单独提供有备份的迁移流程，不能通过删除存档升级。

终端依赖 Microsoft WebView2 Evergreen Runtime。安装器检测缺失时执行微软引导程序，需要网络；目录 ZIP 同样需要此运行时。
