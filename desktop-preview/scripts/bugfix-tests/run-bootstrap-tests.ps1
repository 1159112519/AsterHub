param([Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$t=Split-Path (Split-Path $PSScriptRoot)
New-Item -ItemType Directory -Force $EvidenceRoot | Out-Null
$sources=@('host/AppUpdates.cs','host/UpdateDownload.cs','host/UpdateSources.cs','host/UpdatePublicKey.cs','updater/BootstrapCore.cs','scripts/bugfix-tests/BootstrapTests.cs','scripts/bugfix-tests/BootstrapTestMetadata.cs') | ForEach-Object {Join-Path $t $_}
$exe=Join-Path $EvidenceRoot 'bootstrap-tests.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Web.Extensions.dll ('/out:'+$exe) ('/resource:'+(Join-Path $t 'updater/full-release.json')+',release.json') @sources
if($LASTEXITCODE-ne 0){throw 'Bootstrap tests do not compile'}
& $exe
exit $LASTEXITCODE
