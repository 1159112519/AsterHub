import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const host = name => readFileSync(new URL(`../../host/${name}`, import.meta.url),'utf8');
const desktop = host('DesktopSettings.cs');

test('both desktop right-click surfaces share a native popup with no browser warm-up', () => {
 assert.doesNotMatch(desktop,/NativeCompactMenu|native-menu-warmup|WebView2/);
 assert.match(desktop,/void ShowMenu\(\)[\s\S]*?OpenMenu\(new\[\]/);
 assert.match(desktop,/void ShowTaskbarMenu\([\s\S]*?OpenMenu\(new\[\]/);
 assert.match(host('NativePopupMenu.cs'),/TrackPopupMenuEx/);
});

test('changing desktop context menus preserves action entries and keeps metric details independent', () => {
 const tray = desktop.slice(desktop.indexOf('void ShowMenu()'),desktop.indexOf('void ShowTaskbarMenu'));
 const widget = desktop.slice(desktop.indexOf('void ShowTaskbarMenu'),desktop.indexOf('void OpenMenu'));
 for (const label of ['打开主窗口','偏好设置','关于 AsterHub','退出 AsterHub']) assert.ok(tray.includes(label),label);
 for (const label of ['打开主窗口','上一页','下一页','小组件设置','隐藏小组件']) assert.ok(widget.includes(label),label);
 assert.match(host('taskbar/TaskbarMetricPopup.cs'),/PopupWebViewRuntime/);
});
