param([switch]$Baseline)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
$evidenceRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '../asterhub-preview8-20261030-records/updater'))
New-Item -ItemType Directory -Force $evidenceRoot | Out-Null
$sourceFiles=@('host/AppUpdates.cs','host/UpdateSources.cs','host/UpdatePublicKey.cs','host/UpdatePreferences.cs','host/StartupUpdatePolicy.cs','scripts/bugfix-tests/UpdaterTests.cs') | ForEach-Object {(Join-Path $sourceRoot $_).Replace('/','\')}
$output=Join-Path $evidenceRoot 'updater-tests.exe'
$compilerArgs=@('/nologo',('/out:'+$output),'/r:System.Web.Extensions.dll')
if($Baseline){$compilerArgs+='/define:BASELINE'}
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @compilerArgs @sourceFiles
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $output | Tee-Object -FilePath (Join-Path $evidenceRoot $(if($Baseline){'red.txt'}else{'green.txt'}))
exit $LASTEXITCODE
