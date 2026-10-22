# 安装与数据

默认安装到 `%LOCALAPPDATA%\Programs\AQ Server`，无需管理员权限。安装目录包含 AQServer.exe、SSH/密码学/WebView2 依赖 DLL、TerminalAssets、UiAssets、Licenses、README、版本及负载清单和卸载程序。文件数量由架构决定；目录版使资源、许可、版本和维护边界更清晰。

单 EXE 免安装版首次运行会把相同负载展开到 `%LOCALAPPDATA%\AQServer\Runtime`，之后校验并复用。它并非不需要运行库。

用户配置和布局在 `%APPDATA%\AQServer`，历史与更新缓存在 `%LOCALAPPDATA%\AQServer`。卸载只删除安装器管理的程序文件，保留用户配置与历史。

安装器沿用同一 AppId，后续版本覆盖更新程序目录。默认不启用开机启动；已开启时修正到新的安装路径。将来如果删除旧资源，需在安装定义中精确清理已废弃程序文件，不能对用户数据目录执行递归清理。

运行中的旧版本需要退出后才能升级。数据结构变更应先备份并提供迁移，不能通过删库升级。更新必须保留签名私钥对应关系，或明确交付带新公钥的过渡版本。
