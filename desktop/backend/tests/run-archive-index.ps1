$ErrorActionPreference='Stop'
$previewRoot=Split-Path (Split-Path $PSScriptRoot)
$modules=Join-Path $previewRoot 'app/app/modules'
$testRoot=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-ArchiveIndex-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
foreach($name in @('AsterHub.Core.dll','AsterHub.Storage.dll')){Copy-Item -LiteralPath (Join-Path $modules $name) -Destination $testRoot}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /r:System.Web.Extensions.dll ('/r:'+(Join-Path $testRoot 'AsterHub.Core.dll')) ('/r:'+(Join-Path $testRoot 'AsterHub.Storage.dll')) ('/out:'+(Join-Path $testRoot 'ArchiveIndexTests.exe')) (Join-Path $PSScriptRoot 'ArchiveIndexTests.cs')
if($LASTEXITCODE-ne 0){throw 'Archive regression compilation failed'}
& (Join-Path $testRoot 'ArchiveIndexTests.exe') (Join-Path $testRoot 'fixtures')
if($LASTEXITCODE-ne 0){throw 'Archive regression failed'}
Write-Output ('ISOLATED_TEST_EVIDENCE='+$testRoot)
