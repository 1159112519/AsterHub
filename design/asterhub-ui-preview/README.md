# AsterHub desktop UI review · Frosted glass

This directory builds the standalone Windows UI preview. It overlays the existing layout, injecting only demo data and presentation changes. It does not connect to devices, modify files, install updates, or integrate with Explorer. The production desktop build does not import this directory.

- Taskbar editor restores the underline tabs. Analysis page navigation grows slightly; analysis and preference selectors gain label/chevron spacing. The two chart footers share a lower text line and leave a clear gap below.
- Appearance shows language with the common settings. Themes and app icons are at the bottom. The preview offers Classic Dark and Frosted Glass only. Glass uses a softly lit backdrop, frosted surfaces with fine edge highlights, and solid option menus.
- The preview switches between Chinese and English for the workbench, preferences and the reviewed analysis surfaces. This is not a full translation of the production product; screens outside the review can still have Chinese copy.

The overlay needs an AsterHub desktop frontend checkout containing the archive-backed resource analysis pages and the 0.4.3.2 taskbar editor. The public integration branch alone does not currently contain these features. Set up the frontend's existing Vite dependencies first. The preview also needs a separately obtained unpacked Windows build with the existing fonts and WebView2 SDK redistribution files; this PR does not contain those binaries, third-party reference assets, or the local integration baseline.

```powershell
$env:ASTERHUB_PREVIEW_FRONTEND = 'path-to-desktop-preview'
$env:ASTERHUB_PREVIEW_RELEASE = 'path-to-unpacked-0.4.3.2'
$env:ASTERHUB_PREVIEW_OUT = 'path-for-new-demo-folder'
node design/asterhub-ui-preview/build.mjs $env:ASTERHUB_PREVIEW_FRONTEND
& design/asterhub-ui-preview/package.ps1
```

Run `AsterHub-UI-Demo.exe` from that folder on Windows x64 with the Microsoft Edge WebView2 Runtime installed. Keep `ui`, the runtime DLLs and the licenses alongside the EXE. The WebView has no native message or host-object access, serves the packaged content locally, and uses a separate scratch profile.
