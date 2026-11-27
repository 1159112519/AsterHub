param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
if(!$EvidenceRoot){$EvidenceRoot=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-Icons-'+[guid]::NewGuid().ToString('N'))}
$EvidenceRoot=[IO.Path]::GetFullPath($EvidenceRoot)
if(Test-Path -LiteralPath $EvidenceRoot){throw 'Use a new evidence directory; existing data is not overwritten.'}
$previewRoot=Split-Path (Split-Path $PSScriptRoot)
$runnerRoot=Join-Path $EvidenceRoot 'runner'
$resources=Join-Path $runnerRoot 'resources/brand-icons'
New-Item -ItemType Directory -Path $resources -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $previewRoot 'host/brand-icons') -Directory | Where-Object Name -Match '^\d{2}$' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $resources -Recurse}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework=Split-Path $compiler
$compilerArgs=@('/nologo','/target:exe','/platform:x64','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Xaml.dll',('/out:'+(Join-Path $runnerRoot 'BrandIconsQA.exe')))
foreach($reference in @('WindowsBase','PresentationCore','PresentationFramework')){$compilerArgs+=('/reference:'+(Join-Path $framework ('WPF/'+$reference+'.dll')))}
$compilerArgs+=@((Join-Path $PSScriptRoot 'BrandIconsQA.cs'),(Join-Path $previewRoot 'host/BrandIcons.cs'),(Join-Path $previewRoot 'host/BrandShortcuts.cs'),(Join-Path $previewRoot 'host/DesktopSettings.cs'),(Join-Path $previewRoot 'host/NativeWindowTheme.cs'),(Join-Path $previewRoot 'host/NativePopupMenu.cs'),(Join-Path $previewRoot 'host/WindowClickCommands.cs'),(Join-Path $previewRoot 'host/UiLanguage.cs'),(Join-Path $previewRoot 'host/DiagnosticsLog.cs'),(Join-Path $previewRoot 'host/taskbar/TaskbarModel.cs'),(Join-Path $previewRoot 'host/taskbar/TaskbarSqueeze.cs'),(Join-Path $previewRoot 'host/taskbar/TaskbarPainter.cs'),(Join-Path $previewRoot 'host/taskbar/TaskbarHistory.cs'),(Join-Path $previewRoot 'host/taskbar/TaskbarDetailContent.cs'),(Join-Path $previewRoot 'backend/src/AsterHub.Core/TelemetryContinuity.cs'))
& $compiler @compilerArgs
if($LASTEXITCODE-ne 0){throw 'Icon fixture build failed'}
& (Join-Path $runnerRoot 'BrandIconsQA.exe') (Join-Path $EvidenceRoot 'data')
if($LASTEXITCODE-ne 0){throw 'Icon fixture regression failed'}
Write-Output ('ICON_TEST_EVIDENCE='+$EvidenceRoot)
