# AsterHub 官网

官网使用 React 18 / TypeScript / Vite，保持现有 Tailwind、Lucide、Inter 与 Playfair Display。动效由 GSAP 统一处理，浏览器滚动仍然使用原生行为。

## 内容与组件

- `website/src/App.tsx`：首屏双图柔和聚焦、真实下载和独立平台版本。
- `website/src/StoryExperience.tsx`：七段工作流、滚动配图、GPU 风格切换、大图、FAQ 与品牌故事。
- `website/src/story.css`：章节布局与响应式规则；在原有样式后加载。
- `website/public/media/asterhub-preview4-*.webp`：当前已发布 Windows 预览前端的界面示意。设备地址使用文档保留地址，指标与终端内容为示例，不来自客户环境。

## 交互规则

桌面宽度至少 1100px：图片固定于阅读区，当前章节随滚动位置更新；下方导航可跳转章节。低窗口限制展示图高度，保留所有操作。

平板和手机：图片置于各章节正文前，不捕获滚动。图片可点开，原生对话框支持 Escape、关闭与焦点归还；GPU 风格可分别切换。

减少动态效果：保留内容和章节状态，关闭位移、跟随与渐变转场。不使用自动循环展示或滚动劫持。

## 发布与兼容

主路径 `/asterhub/`，旧首页 `/aqserver/` 重定向。旧 `/aqserver/releases/` 下载与签名更新地址继续可用，已有反馈 API 位置保持不变。路径更名不等于拥有独立注册域名。

版本来源为 `src/release-versions.json`。Windows 预览版、Windows 稳定版、Ubuntu 分别显示，官网变更不触发应用重新打包，不修改已签名的发布元数据。

```sh
npm ci
npm run build
npm run test:sites
```

上线前验证真实浏览器的滚动换图、各尺寸布局、移动导航、风格切换、大图和减少动态效果；上线后复核静态文件与下载散列。
