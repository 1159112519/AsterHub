# AsterHub Windows 预览工作台结构

`AsterHub.Preview.exe` 是 WPF 原生宿主，提供 WebView2、托盘、ConPTY/SSH 终端桥接、文件传输队列、更新和反馈。工作台位于 `resources/workbench`，由 `web/src/native` 的 React 源码构建。

`app/modules/AQServer.Core.dll` 定义数据模型；`AQServer.Storage.dll` 负责设置和档案；`AQServer.Platform.Windows.dll` 负责 Windows 与 SSH 采样、文件接口和进程身份操作。WPF 宿主通过有限的请求桥接访问这些模块，不开放任意原生命令执行接口。终端仅在用户创建真实会话后执行输入。

`runtime/managed`、`runtime/win-x64` 是 SSH.NET 和 WebView2 依赖；`resources/terminal` 是 xterm 资源。更新按整包安装，不混用不同版本模块。`desktop/` 仍保留原稳定版实现，两个通道独立发布。

用户配置与档案放在用户数据目录，安装目录不存连接密码或历史记录。卸载保留用户数据。
