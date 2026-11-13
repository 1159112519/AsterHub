param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
if(!$EvidenceRoot){$EvidenceRoot=Join-Path $sourceRoot 'work/resource-lifetime-tests'}
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
$appRoot=Join-Path $sourceRoot 'app';$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319';$exe=Join-Path $EvidenceRoot 'StoragePagedTests.exe'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll',(Join-Path $framework 'WPF/WindowsBase.dll'),(Join-Path $framework 'WPF/PresentationCore.dll'),(Join-Path $framework 'WPF/PresentationFramework.dll'),(Join-Path $framework 'WPF/UIAutomationClient.dll'),(Join-Path $framework 'WPF/UIAutomationTypes.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Core.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Core.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Storage.dll'),(Join-Path $appRoot 'app/modules/AsterHub.Platform.Windows.dll'))
$argsBuild=@('/nologo','/platform:x64','/optimize+','/main:StoragePagedTests',('/out:'+$exe))
foreach($ref in $refs){$argsBuild+='/r:'+$ref}
$argsBuild+=@(Get-ChildItem (Join-Path $sourceRoot 'host') -Filter *.cs -Recurse | ForEach-Object {$_.FullName})
$argsBuild+=Join-Path $PSScriptRoot 'StoragePagedTests.cs'
& (Join-Path $framework 'csc.exe') @argsBuild
if($LASTEXITCODE -ne 0){throw 'Paged index compile failed'}
foreach($name in @('AsterHub.Core.dll','AsterHub.Storage.dll','AsterHub.Platform.Windows.dll')){Copy-Item -LiteralPath (Join-Path $appRoot ('app/modules/'+$name)) -Destination $EvidenceRoot -Force}
& $exe | Tee-Object -FilePath (Join-Path $EvidenceRoot 'paged-index.txt')
if($LASTEXITCODE -ne 0){throw 'Paged index regression failed'}
