param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
if(!$EvidenceRoot){$EvidenceRoot=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-Memory-'+[guid]::NewGuid().ToString('N'))}
$EvidenceRoot=[IO.Path]::GetFullPath($EvidenceRoot)
if(Test-Path -LiteralPath $EvidenceRoot){throw 'Use a new evidence directory; existing data is not overwritten.'}
$previewRoot=Split-Path (Split-Path $PSScriptRoot)
$runnerRoot=Join-Path $EvidenceRoot 'runner'
$fixtureRoot=Join-Path $EvidenceRoot 'owned-fixture'
New-Item -ItemType Directory -Path $runnerRoot,$fixtureRoot | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /platform:x64 ('/out:'+(Join-Path $fixtureRoot 'Fixture.exe')) (Join-Path $PSScriptRoot 'MemoryOwnedFixture.cs')
if($LASTEXITCODE-ne 0){throw 'Owned memory fixture build failed'}
& $compiler /nologo /target:exe /platform:x64 /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $runnerRoot 'MemoryNativeQA.exe')) (Join-Path $PSScriptRoot 'MemoryNativeQA.cs') (Join-Path $previewRoot 'host/MemoryMaintenance.cs')
if($LASTEXITCODE-ne 0){throw 'Memory regression harness build failed'}
& (Join-Path $runnerRoot 'MemoryNativeQA.exe') (Join-Path $EvidenceRoot 'data') (Join-Path $fixtureRoot 'Fixture.exe')
if($LASTEXITCODE-ne 0){throw 'Memory regression failed'}
& $compiler /nologo /target:exe /platform:x64 /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $runnerRoot 'MemoryProtectionQA.exe')) (Join-Path $PSScriptRoot 'MemoryProtectionQA.cs') (Join-Path $previewRoot 'host/MemoryMaintenance.cs')
if($LASTEXITCODE-ne 0){throw 'Memory protection harness build failed'}
& (Join-Path $runnerRoot 'MemoryProtectionQA.exe') (Join-Path $EvidenceRoot 'protection-data') (Join-Path $fixtureRoot 'Fixture.exe')
if($LASTEXITCODE-ne 0){throw 'Memory protection regression failed'}
Write-Output ('MEMORY_TEST_EVIDENCE='+$EvidenceRoot)
