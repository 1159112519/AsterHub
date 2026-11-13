$ErrorActionPreference='Stop'
$previewRoot=$PSScriptRoot
Push-Location (Join-Path $previewRoot 'web')
try { & npm.cmd run build; if($LASTEXITCODE -ne 0){throw 'Frontend build failed'} } finally { Pop-Location }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$appRoot=Join-Path $previewRoot 'app'
$fontOutput=Join-Path $appRoot 'resources/fonts'
New-Item -ItemType Directory -Path $fontOutput -Force | Out-Null
Copy-Item -Path (Join-Path $previewRoot 'host/fonts/*') -Destination $fontOutput -Force
New-Item -ItemType Directory -Path (Join-Path $appRoot 'licenses') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $previewRoot 'web/public/fonts/HarmonyOS-Sans-LICENSE.txt') -Destination (Join-Path $appRoot 'licenses/HarmonyOS-Sans-LICENSE.txt') -Force
$localeOutput=Join-Path $appRoot 'resources/locales'
New-Item -ItemType Directory -Path $localeOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $previewRoot 'web/src/i18n/en.json') -Destination (Join-Path $localeOutput 'en.json') -Force
$brandOutput=Join-Path $appRoot 'resources/brand-icons'
New-Item -ItemType Directory -Path $brandOutput -Force | Out-Null
Copy-Item -Path (Join-Path $previewRoot 'host/brand-icons/*') -Destination $brandOutput -Recurse -Force
& (Join-Path $previewRoot 'scripts/build-backend.ps1') -Root $previewRoot
$framework=Split-Path $compiler
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll',(Join-Path $framework 'WPF/WindowsBase.dll'),(Join-Path $framework 'WPF/PresentationCore.dll'),(Join-Path $framework 'WPF/PresentationFramework.dll'),(Join-Path $framework 'WPF/UIAutomationClient.dll'),(Join-Path $framework 'WPF/UIAutomationTypes.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Core.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Core.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Storage.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Platform.Windows.dll'))
$argsBuild=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+(Join-Path $appRoot 'AsterHub.exe')),('/win32icon:'+(Join-Path $previewRoot 'host/AsterHub.Process.ico')))
foreach($reference in $refs){$argsBuild+=('/reference:'+$reference)}
$argsBuild+=('/win32manifest:'+(Join-Path $previewRoot 'host/AsterHub.manifest'))
$argsBuild+=@(Get-ChildItem (Join-Path $previewRoot 'host') -Filter *.cs -Recurse | ForEach-Object {$_.FullName})
& $compiler @argsBuild
if($LASTEXITCODE -ne 0){throw 'Windows host build failed'}
Copy-Item -LiteralPath (Join-Path $previewRoot 'host/App.config') -Destination (Join-Path $appRoot 'AsterHub.exe.config') -Force
& node (Join-Path $previewRoot 'scripts/package-app.mjs')
if($LASTEXITCODE -ne 0){throw 'Payload inventory failed'}
Write-Output ('ASTERHUB_EXE='+(Join-Path $appRoot 'AsterHub.exe'))
