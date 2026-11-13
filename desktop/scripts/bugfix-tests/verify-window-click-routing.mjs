// Source wiring checks: no window/process creation and no connection to the live app.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const host=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../host');
const read=n=>fs.readFileSync(path.join(host,n),'utf8');
test('in-app minimize delegates to the shared caption action',()=>{
 assert.match(read('WorkbenchHost.cs'),/case "window\.minimize":MinimizeLikeCaption\(\);break;/);
});
test('native taskbar SC_MINIMIZE is handled by the same action, not restore',()=>{
 assert.match(read('MinimizeIntent.cs'),/WindowClickCommands\.IsMinimizeSystemCommand\(message,wParam\.ToInt64\(\)\)[\s\S]*?handled=true;MinimizeLikeCaption\(\);return IntPtr\.Zero;/);
});
test('shared minimize records explicit intent and uses normal WindowState',()=>{
 assert.match(read('MinimizeIntent.cs'),/void MinimizeLikeCaption\(\)[\s\S]*?minimizeIntent\.Explicit\(\);WindowState=WindowState\.Minimized;/);
});
test('a second taskbar launch has a toggle channel, not the restore-only channel',()=>{
 const s=read('WorkbenchHost.cs');
 assert.match(s,/AsterHubPreviewToggle-/);
 assert.match(s,/new Action\(window\.ToggleFromTaskbar\)/);
 assert.match(s,/new Action\(window\.RestoreFromTray\)/);
 assert.match(s,/if\(!first\)\{if\(!StartBackground\)/);
});
test('taskbar/tray toggle invokes shared minimize while menu restore stays restore-only',()=>{
 const s=read('DesktopSettings.cs');
 assert.match(s,/public void Toggle\(\)[\s\S]*?WindowClickCommands\.ForTaskbarClick\([\s\S]*?owner\.MinimizeLikeCaption\(\)/);
 assert.match(s,/public void Restore\(\)[\s\S]*?SystemCommands\.RestoreWindow\(owner\)/);
});
test('activation reconciliation never raises or restores the HWND',()=>{
 const s=read('BrandIcons.cs'),start=s.indexOf('void QueueActiveTabSync()'),end=s.indexOf('void QueueTaskbarRefresh()',start);
 assert.ok(start>=0&&end>start);
 const body=s.slice(start,end);
 assert.match(body,/taskbar\.ActivateTab\(hwnd\);taskbar\.SetActiveAlt\(hwnd\);/);
 assert.doesNotMatch(body,/ShowWindow\(|SetForegroundWindow\(|owner\.Activate\(|SystemCommands\.RestoreWindow\(/);
});
