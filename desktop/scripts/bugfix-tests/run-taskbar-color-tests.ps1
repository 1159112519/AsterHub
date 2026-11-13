param([Parameter(Mandatory=$true)][string]$BuiltApp,[Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$source=Split-Path (Split-Path $PSScriptRoot)
$evidence=(New-Item -ItemType Directory -Path $EvidenceRoot -Force).FullName
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$files=@('TaskbarModel.cs','TaskbarSqueeze.cs','TaskbarPainter.cs','TaskbarHistory.cs','TaskbarDetailContent.cs') | ForEach-Object {Join-Path $source "host/taskbar/$_"}
$exe=Join-Path $evidence 'TaskbarColorTests.exe'
& $compiler /nologo /platform:x64 /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll "/out:$exe" @files (Join-Path $source 'backend/src/AsterHub.Core/TelemetryContinuity.cs') (Join-Path $PSScriptRoot 'TaskbarColorTests.cs')
if($LASTEXITCODE -ne 0){throw 'Taskbar color regression compilation failed'}
& $exe $BuiltApp $evidence
if($LASTEXITCODE -ne 0){throw 'Taskbar color regression failed'}
