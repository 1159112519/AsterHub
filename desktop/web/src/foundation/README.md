# AsterHub 内部基础层

入口规范：`docs/architecture/ASTERHUB-FOUNDATIONS.md`。开发场景：启动 Vite 后打开 `/foundation.html`；该入口不在生产构建输入中，不出现在用户菜单。

- `tokens.css`：保留已有视觉数值的语义令牌，不绑定图标与皮肤。
- `ui.jsx`：`Button({icon,busy,busyLabel,tone,...})`、`IconButton`、`Toolbar({label,align})`。公共组件由原 `components.jsx` 兼容导出。
- `overlay.jsx`：`useDismissableLayer({open,refs,onDismiss})`。浮层使用同一 document 栈；嵌套只处理顶层。Modal 保留焦点圈定，普通菜单不冒充模态。
- `directory-motion.js`：进入/退出几何衔接与一个拥有清理责任的时间线。基础参数继续在 `motion.js`，不另建动画引擎。
- `measurement.js`：ready/stale/missing/unsupported；0 是有效数据。连接中断硬件保留由既有 device-state 执行。
- `bounded-cache.js`：LRU 的条数和可选权重上限；超大条目不会驱逐所有正常条目后再被拒绝。
- `request-scope.js`：同一 lane 的旧等待被取消，不同 lane 互不影响。finish/cancelAll 释放引用。
- `polling-resource.js`：引用计数、串行轮询、保留最后成功数据、忽略订阅结束后的返回。后台任务真实取消仍使用宿主 control。
- `preference-store.js`：所有消费者共用一个系统/应用偏好监听，订阅归零释放源监听。

验证：`npm run test:foundation`、`npm run check:foundation`、`npm run build`。浏览器和原生宿主场景结果另存工作记录；通过单元测试不代表完成 Windows 安装验收。

迁移范围：所有使用公共 Button/NumberValue/MotionSurface 的页面自动使用新基础；ThemedSelect/Dropdown/Modal 接入共享浮层；StorageTools 接入公共工具栏/目录动效/轮询；HistoryView 接入 LRU/取消作用域；终端清理已关闭索引。现有图表几何与皮肤配色保持原实现，不进行无依据重写。全量历史 CSS 拆分与未来功能迁移保留逐步清单，不以删除兼容层换取代码整洁。
