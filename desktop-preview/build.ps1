$ErrorActionPreference='Stop'
$previewRoot=$PSScriptRoot
Push-Location (Join-Path $previewRoot 'web')
try { & npm.cmd run build; if($LASTEXITCODE -ne 0){throw 'Frontend build failed'} } finally { Pop-Location }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$appRoot=Join-Path $previewRoot 'app'
& (Join-Path $previewRoot 'scripts/build-backend.ps1') -Root $previewRoot
$framework=Split-Path $compiler
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll',(Join-Path $framework 'WPF/WindowsBase.dll'),(Join-Path $framework 'WPF/PresentationCore.dll'),(Join-Path $framework 'WPF/PresentationFramework.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Core.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $appRoot 'app/modules/AQServer.Core.dll'),(Join-Path $appRoot 'app/modules/AQServer.Storage.dll'),(Join-Path $appRoot 'app/modules/AQServer.Platform.Windows.dll'))
$argsBuild=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+(Join-Path $appRoot 'AsterHub.Preview.exe')),('/win32icon:'+(Join-Path $previewRoot 'host/AsterHub.ico')))
foreach($reference in $refs){$argsBuild+=('/reference:'+$reference)}
$argsBuild+=@(Get-ChildItem (Join-Path $previewRoot 'host') -Filter *.cs | ForEach-Object {$_.FullName})
& $compiler @argsBuild
if($LASTEXITCODE -ne 0){throw 'Windows host build failed'}
& node (Join-Path $previewRoot 'scripts/package-preview.mjs')
if($LASTEXITCODE -ne 0){throw 'Payload inventory failed'}
Write-Output ('PREVIEW_EXE='+(Join-Path $appRoot 'AsterHub.Preview.exe'))
