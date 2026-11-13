param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
if(!$EvidenceRoot){$EvidenceRoot=Join-Path $sourceRoot 'work/resource-lifetime-tests'}
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
& (Join-Path $PSScriptRoot 'run-storage-tests.ps1') -EvidenceRoot $EvidenceRoot
if($LASTEXITCODE -ne 0){throw 'Storage regression failed'}
$testExe=Join-Path $EvidenceRoot 'ResourceLifetimeTests.exe'
$refs=@('System.dll','System.Core.dll','System.Web.Extensions.dll',(Join-Path $EvidenceRoot 'AsterHub.Core.dll'),(Join-Path $EvidenceRoot 'AsterHub.Storage.dll'),(Join-Path $EvidenceRoot 'AsterHub.Platform.Windows.dll'))
$argsBuild=@('/nologo','/platform:x64','/optimize+','/main:ResourceLifetimeTests',('/out:'+$testExe))
foreach($ref in $refs){$argsBuild+='/r:'+$ref}
$argsBuild+=@((Join-Path $sourceRoot 'host/StorageIndex.cs'),(Join-Path $sourceRoot 'host/StorageSnapshotCache.cs'),(Join-Path $sourceRoot 'host/LocalStorageScan.cs'),(Join-Path $EvidenceRoot 'FileNormalization.cs'),(Join-Path $PSScriptRoot 'StorageTests.cs'),(Join-Path $PSScriptRoot 'ResourceLifetimeTests.cs'))
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @argsBuild
if($LASTEXITCODE -ne 0){throw 'Resource lifetime test compile failed'}
& $testExe | Tee-Object -FilePath (Join-Path $EvidenceRoot 'resource-lifetime.txt')
if($LASTEXITCODE -ne 0){throw 'Resource lifetime test failed'}
