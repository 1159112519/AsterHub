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
test('native system minimize remains owned by Windows',()=>{
 const s=read('MinimizeIntent.cs');
 assert.match(s,/if\(WindowClickCommands\.IsMinimizeSystemCommand\(message,wParam\.ToInt64\(\)\)\)return IntPtr\.Zero;/);
 assert.doesNotMatch(s,/handled\s*=\s*true/);
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
test('branding keeps the native taskbar button and its activation state',()=>{
 const s=read('BrandIcons.cs');
 assert.doesNotMatch(s,/\b(?:DeleteTab|AddTab|ActivateTab|SetActiveAlt|TaskbarButtonCreated)\b/);
 assert.match(s,/WM_SETICON|SendMessage\(hwnd,0x80/);
 assert.match(s,/BrandShortcuts\.ApplyWindow\(hwnd,selected\)/);
});
test('the process shell identity is set before the application creates any window',()=>{
 const s=read('WorkbenchHost.cs');
 assert.match(s,/BrandShortcuts\.SetProcessIdentity\(\);[\s\S]*?var app=new Application[\s\S]*?new WorkbenchWindow\(\)/);
});
