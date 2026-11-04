param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
if(!$EvidenceRoot){$EvidenceRoot=[IO.Path]::GetFullPath((Join-Path $sourceRoot '../../preview23-20261104/history-tests'))}
New-Item -ItemType Directory -Force $EvidenceRoot | Out-Null
$core=Join-Path $EvidenceRoot 'AQServer.Core.dll'
Copy-Item -LiteralPath (Join-Path $sourceRoot 'app/app/modules/AQServer.Core.dll') -Destination $core -Force
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources=@('backend/src/AQServer.Storage/Archive.cs','backend/src/AQServer.Storage/ArchiveSummaryReader.cs','host/PreviewHistory.cs','host/HistoryRange.cs','scripts/bugfix-tests/HistoryTests.cs') | ForEach-Object {Join-Path $sourceRoot $_}
& $compiler /nologo /optimize+ /r:System.Web.Extensions.dll ('/r:'+$core) ('/out:'+(Join-Path $EvidenceRoot 'HistoryTests.exe')) @sources
if($LASTEXITCODE -ne 0){throw 'History tests compilation failed'}
& (Join-Path $EvidenceRoot 'HistoryTests.exe') (Join-Path $EvidenceRoot 'fixtures') | Tee-Object -FilePath (Join-Path $EvidenceRoot 'result.txt')
if($LASTEXITCODE -ne 0){throw 'History tests failed'}
$indexSources=@('backend/src/AQServer.Storage/Archive.cs','backend/src/AQServer.Storage/ArchiveSummaryReader.cs','backend/tests/ArchiveIndexTests.cs') | ForEach-Object {Join-Path $sourceRoot $_}
& $compiler /nologo /optimize+ /r:System.Web.Extensions.dll ('/r:'+$core) ('/out:'+(Join-Path $EvidenceRoot 'ArchiveIndexTests.exe')) @indexSources
if($LASTEXITCODE -ne 0){throw 'Index tests compilation failed'}
& (Join-Path $EvidenceRoot 'ArchiveIndexTests.exe') (Join-Path $EvidenceRoot 'index-fixtures') | Tee-Object -FilePath (Join-Path $EvidenceRoot 'index-result.txt')
if($LASTEXITCODE -ne 0){throw 'Index tests failed'}
