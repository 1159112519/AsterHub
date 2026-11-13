# 历史兼容标识

正式产品名是 AsterHub，正式源码目录为 `desktop/`，可执行文件为 `AsterHub.exe`。

以下字符串仍是已发布版本的数据或协议的一部分，不是另一个产品入口。直接改名会影响旧设置、加密密码、历史文件或升级：

| 标识 | 保留原因 |
| --- | --- |
| `AsterHubPreview`、`windows-preview.json` | 已安装客户端的签名更新 appId 和固定清单 URL |
| `Preview1`、`PreviewUpdates` 及原有单实例互斥/事件名 | 已安装程序的个人数据、下载缓存和单实例协调 |
| 原安装 AppId、旧快捷方式与 `AsterHub.Preview.exe` 检测 | 安装器识别和迁移既有安装；不再生成旧程序 |
| `AQServerHistory`、旧 `AQServer` 数据目录 | 读取已存在的历史文件和配置 |
| `AQServer/SSH/password/v1` | Windows DPAPI 的旧密码熵；改动会使已保存密码不可解密 |
| `AQServerServices`、`X-AQServer-Feedback`、服务签名字段 | 当前已发布客户端与反馈服务协议 |
| `AQSERVER_*` 环境变量与旧 Run 项 | 兼容原有启动/测试调用；无需用户新增设置 |
| `.aqserver-test-` | 远端文件操作测试的清理边界，防止误删非测试目录 |

当前构建使用 `AsterHub.Core`、`AsterHub.Storage`、`AsterHub.Platform.Windows`，不依赖旧 `AQServer.*` 程序集。首次启动默认仅本机，不预设私人服务器。维护兼容代码时，应保留已安装用户的配置、历史、凭据与升级路径。
