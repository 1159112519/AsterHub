param([string]$EvidenceRoot,[string]$BuiltApp)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
if(!$EvidenceRoot){$EvidenceRoot=Join-Path $env:TEMP ('AsterHub-Sampling-'+[guid]::NewGuid().ToString('N'))}
if(!$BuiltApp){$BuiltApp=Join-Path $sourceRoot 'app'}
New-Item -ItemType Directory -Force $EvidenceRoot | Out-Null
$refs=@('AsterHub.Core.dll','AsterHub.Storage.dll','AsterHub.Platform.Windows.dll') | ForEach-Object {
 $path=Join-Path $BuiltApp ('app/modules/'+$_)
 $target=Join-Path $EvidenceRoot $_
 Copy-Item -LiteralPath $path -Destination $target -Force
 '/r:'+$target
}
Get-ChildItem -LiteralPath (Join-Path $BuiltApp 'runtime/managed') -Filter '*.dll' | Where-Object {$_.Name -notlike 'Microsoft.Web.WebView2.*'} | Copy-Item -Destination $EvidenceRoot -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=@('host/taskbar/TaskbarModel.cs','host/taskbar/TaskbarSqueeze.cs','host/taskbar/TaskbarPainter.cs','host/taskbar/TaskbarHistory.cs','host/taskbar/TaskbarDetailContent.cs','scripts/bugfix-tests/SamplingContinuityTests.cs') | ForEach-Object {Join-Path $sourceRoot $_}
$exe=Join-Path $EvidenceRoot 'SamplingContinuityTests.exe'
& $compiler /nologo /platform:x64 /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll @refs ('/out:'+$exe) @sources
if($LASTEXITCODE -ne 0){throw 'Sampling regression compilation failed'}
& $exe | Tee-Object -FilePath (Join-Path $EvidenceRoot 'result.txt')
if($LASTEXITCODE -ne 0){throw 'Sampling regression failed'}
Write-Output ('SAMPLING_EVIDENCE='+$EvidenceRoot)
