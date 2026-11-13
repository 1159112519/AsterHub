param([Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
$files=@('host/AppUpdates.cs','host/UpdateDownload.cs','host/UpdateSources.cs','host/UpdatePublicKey.cs','scripts/bugfix-tests/DeliveryTests.cs') | ForEach-Object {Join-Path $sourceRoot $_}
$exe=Join-Path $EvidenceRoot 'delivery-tests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /r:System.Web.Extensions.dll ('/out:'+$exe) @files
if($LASTEXITCODE -ne 0){throw 'Delivery fixture compilation failed'}
& $exe
exit $LASTEXITCODE
