$ErrorActionPreference='Stop'
$root=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$out=Join-Path $root 'outputs/AsterHub-毛玻璃UI演示'
$release=Join-Path $root 'work/release-0.4.3.2-20261110/AsterHub-0.4.3.2-final'
if ($env:ASTERHUB_PREVIEW_OUT) { $out=(Resolve-Path -LiteralPath $env:ASTERHUB_PREVIEW_OUT).Path }
if ($env:ASTERHUB_PREVIEW_RELEASE) { $release=(Resolve-Path -LiteralPath $env:ASTERHUB_PREVIEW_RELEASE).Path }
$source=if ($env:ASTERHUB_PREVIEW_FRONTEND) { $env:ASTERHUB_PREVIEW_FRONTEND } else { 'desktop-preview' }
$src=(Resolve-Path -LiteralPath $source).Path
$fx=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
foreach($file in @('managed/Microsoft.Web.WebView2.Core.dll','managed/Microsoft.Web.WebView2.Wpf.dll','win-x64/WebView2Loader.dll')){Copy-Item -LiteralPath (Join-Path $release "runtime/$file") -Destination $out -Force}
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Xaml.dll',(Join-Path $fx 'WPF/WindowsBase.dll'),(Join-Path $fx 'WPF/PresentationCore.dll'),(Join-Path $fx 'WPF/PresentationFramework.dll'),(Join-Path $out 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $out 'Microsoft.Web.WebView2.Wpf.dll'))
& (Join-Path $fx 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ "/win32icon:$src/host/AsterHub.ico" "/win32manifest:$PSScriptRoot/UiDemo.manifest" "/out:$out/AsterHub-UI-Demo.exe" ($refs | ForEach-Object { '/reference:'+$_ }) (Join-Path $PSScriptRoot 'UiDemo.cs')
if($LASTEXITCODE -ne 0){throw "csc failed: $LASTEXITCODE"}
$licenses=New-Item -ItemType Directory -Force (Join-Path $out 'licenses')
foreach($name in @('HarmonyOS-Sans-LICENSE.txt','webview2_sdk_LICENSE.txt','webview2_sdk_NOTICE.txt')){Copy-Item -LiteralPath (Join-Path $release "licenses/$name") -Destination $licenses.FullName}
Get-ChildItem -LiteralPath (Join-Path $release 'resources/terminal') -File | Where-Object Name -Match '^(LICENSE|NOTICE)' | Copy-Item -Destination $licenses.FullName
Get-Item -LiteralPath (Join-Path $out 'AsterHub-UI-Demo.exe') | Select-Object FullName,Length









