param([Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
$appRoot=Join-Path $sourceRoot 'app'
Copy-Item -LiteralPath (Join-Path $appRoot 'AsterHub.exe') -Destination $EvidenceRoot -Force
Get-ChildItem -LiteralPath (Join-Path $appRoot 'app/modules') -Filter *.dll | Copy-Item -Destination $EvidenceRoot -Force
Get-ChildItem -LiteralPath (Join-Path $appRoot 'runtime/managed') -Filter *.dll | Copy-Item -Destination $EvidenceRoot -Force
$test=Join-Path $EvidenceRoot 'StorageScopeTests.exe'
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" /nologo /platform:x64 "/out:$test" "/r:$(Join-Path $EvidenceRoot 'AsterHub.exe')" "/r:$(Join-Path $EvidenceRoot 'AsterHub.Core.dll')" /r:System.dll /r:System.Core.dll (Join-Path $PSScriptRoot 'StorageScopeTests.cs')
if($LASTEXITCODE -ne 0){throw 'Storage scope test compile failed'}
& $test | Tee-Object -FilePath (Join-Path $EvidenceRoot 'scope-green.txt')
exit $LASTEXITCODE
