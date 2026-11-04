param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
if(!$EvidenceRoot){$EvidenceRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '../../preview23-20261104/updater-tests'))}
New-Item -ItemType Directory -Force $EvidenceRoot | Out-Null
$sourceFiles=@('host/AppUpdates.cs','host/UpdateDownload.cs','host/UpdateSources.cs','host/UpdatePublicKey.cs','host/BackgroundUpdateTask.cs','scripts/bugfix-tests/BackgroundUpdateTests.cs','scripts/bugfix-tests/DownloadCancellationScenario.cs') | ForEach-Object {Join-Path $sourceRoot $_}
$output=Join-Path $EvidenceRoot 'background-update-tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo ('/out:'+$output) /r:System.Web.Extensions.dll @sourceFiles
if($LASTEXITCODE -ne 0){throw 'Background update test compilation failed'}
& $output | Tee-Object -FilePath (Join-Path $EvidenceRoot 'result.txt')
exit $LASTEXITCODE
