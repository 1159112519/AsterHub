# 官网与更新服务部署

目标：`http://8.140.49.4/aqserver/`，更新清单 `/aqserver/releases/stable.json`。

1. 构建桌面安装器并签名更新清单，再执行 website 的 `npm run build`。
2. 把 `website/dist/client/` 内容复制到部署包的 `site/` 目录；`deployment/deploy.sh` 与 `deployment/nginx.conf` 放在旁边。
3. 上传部署包到目标服务器。使用 SSH 先核查现有 Nginx、站点和端口，然后解压并运行 `sudo bash deploy.sh`。
4. 将已审查的 Nginx location 合入目标 server 块，执行 `sudo nginx -t`，通过后 reload。
5. 验证页面、静态资源、字体、安装器下载、SHA256 和签名清单；桌面实际检查更新也要验证一次。

脚本为每次发布创建独立目录，通过 symlink 原子切换，不覆盖现有其他站点，不自行修改 Nginx。保留旧版本目录，回退时将 current 指向上一个目录，再核查清单/下载文件一致性。

当前未取得 8.140.49.4 的 SSH 用户名、端口与可用认证方式，因此尚未部署。浏览器自动化工具在本会话不可用，手机/平板/桌面布局的实际截图和鼠标聚光效果还需要浏览器验收；构建和 HTTP 检查不能代替视觉验收。

生产环境建议绑定域名并配置 HTTPS。更新清单已有 RSA 签名与安装器 SHA256 校验；HTTP 下载仍可能被网络阻断，签名不提供传输加密。
