// Preview copy only. Keep stable control values and CSS identifiers shared between builds.
const entries=`
偏好设置|Preferences
外观与动效|Appearance & Motion
采样与历史|Sampling & History
窗口与托盘|Window & Tray
任务栏监测|Taskbar Monitor
反馈与建议|Feedback
关于与更新|About & Updates
全局皮肤|App Themes
应用图标|App Icons
界面语言|Language
界面字号|Text Size
界面密度|Density
数字动效|Number Animation
逐位滚动|Rolling Digits
直接更新|Instant
柔和淡入|Fade
轻盈滑动|Slide
经典深色|Classic Dark
雾面玻璃|Frosted Glass
减少动态效果|Reduce Motion
窗口毛玻璃|Window Blur
GPU 显示风格|GPU Card Style
经典卡片|Classic
趋势卡片|Trend
标准|Standard
舒适|Comfortable
紧凑|Compact
取消|Cancel
应用|Apply
确定|OK
已应用|Applied
正在应用…|Applying…
关闭窗口|Close Window
我的设备|Devices
资源监控|Resources
资源概览|Overview
历史记录|History
工作空间|Workspace
关于 AsterHub|About AsterHub
UI 演示|UI Preview
持续记录|Recording
本机|This PC
渲染工作站|Render Station
训练服务器|Training Server
运行监测|Live Monitor
占用分析|Usage Analysis
显存分析|VRAM Analysis
内存分析|Memory Analysis
流量分析|Traffic Analysis
CPU 利用率|CPU Usage
进程占用|Process Usage
逐核占用|Per-core Usage
核心负载热图|Core Load Map
显存余量时间带|VRAM Headroom
显存占用|VRAM Used
显存变化排行|VRAM Changes
内存变化排行|Memory Changes
进程内存趋势|Process Memory Trend
内存使用|Memory Used
网卡流量排行|Adapter Traffic
网络速率|Network Throughput
网卡错误与丢弃|Adapter Errors and Drops
趋势时段|Time Range
近 15 分钟|Last 15 min
近 30 分钟|Last 30 min
近 1 小时|Last Hour
近 3 小时|Last 3 Hours
刷新|Refresh
对比时段|Comparison Window
对比起点|Comparison Start
对比终点|Comparison End
对比前 |Compare 
自定义区间|Custom Range
 分钟| min
 小时| hr
 秒| s
起点占用|Start Usage
当前占用|Current Usage
终点占用|End Usage
显存增减|VRAM Change
内存增减|Memory Change
起点|Start
终点|End
当前|Latest
返回最近|Reset Selection
下载峰值|Download Peak
定位峰值|Jump to Peak
峰值|Peak
全部网卡|All Adapters
分析显卡|GPU
分析网卡|Adapter
以太网|Ethernet
网络测速|Speed Test
进程排行|Process Ranking
只比较起止均有记录的同一进程。|Includes processes recorded at both endpoints.
按网卡累计计数差计算。|Calculated from adapter byte counters.
正在读取监测记录…|Loading samples…
所选时段没有可用样本。|No samples in this time range.
监测与外观|Monitoring & Appearance
监测设备|Device
任务栏数字变化|Taskbar Number Animation
数字变化|Number Animation
更新间隔|Refresh Interval
显示监测|Show Monitor
点击打开资源详情|Open Details on Click
自动切页|Auto Page
实时预览|Live Preview
排版微调|Spacing
应用范围|Scope
全部模块|All Metrics
组件分类|Widget Categories
组件|Widgets
增加一页|Add Page
减少一页|Remove Page
上一页预览|Previous Page
下一页预览|Next Page
一列|One Column
两列|Two Columns
三列|Three Columns
撤销|Undo
重置|Reset
空位置|Empty Slot
交换位置|Swap Positions
名称|Label
数字|Number
单位|Unit
图形|Chart
应用更新|App Update
文件传输|File Transfer
磁盘扫描|Disk Scan
下载中断|Download Interrupted
继续下载|Resume Download
正在复制|Copying
已完成|Completed
查看结果|View Result
收起任务|Close Tasks
任务|Tasks
下载|Download
上传|Upload
内存|Memory
网络|Network
存储|Storage
终端|Terminal
文件|Files
总利用率|Total Usage
已列进程|Listed Processes
以整机 CPU 为基准；仅列出已采样进程。|CPU usage is system-wide. The list includes sampled processes only.
 个逻辑核心| logical cores
每格取该时段峰值|Peak per time bin
所选时段|Selected Period
时段最低|Period Minimum
余量低于|Headroom below
已记录样本中，余量均不低于|All recorded samples have headroom of at least
曲线、数字、浮层使用同一套动效节奏。减少动态效果时立即更新数值。|Reduce motion to update values instantly.
图标与皮肤独立设置，应用后同步更换窗口、托盘与本程序快捷方式。|Icons are independent of the theme.
简体中文|简体中文`;
const map=new Map(entries.trim().split('\n').map(line=>line.split('|')));
const keys=[...map.keys()].sort((a,b)=>b.length-a.length);
const tokens=new RegExp(keys.map(x=>x.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')).join('|'),'gu');
export const english=source=>source.replace(tokens,key=>map.get(key));

