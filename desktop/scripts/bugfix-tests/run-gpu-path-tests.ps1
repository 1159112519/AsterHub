param([Parameter(Mandatory=$true)][string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
$gpuEvidence=[IO.Path]::GetFullPath($EvidenceRoot)
New-Item -ItemType Directory -Force -Path $gpuEvidence | Out-Null
foreach($name in @('AsterHub.Core','AsterHub.Storage')){
 Copy-Item -LiteralPath (Join-Path $sourceRoot ('app/app/modules/'+$name+'.dll')) -Destination (Join-Path $gpuEvidence ($name+'.dll'))
}
$argsBuild=@('/nologo','/target:exe','/platform:x64',('/out:'+(Join-Path $gpuEvidence 'GpuPathCollectorTests.exe')),('/reference:'+(Join-Path $gpuEvidence 'AsterHub.Core.dll')),('/reference:'+(Join-Path $gpuEvidence 'AsterHub.Storage.dll')),'/reference:System.Web.Extensions.dll')
$argsBuild+=(Join-Path $sourceRoot 'backend/src/AsterHub.Platform.Windows/Remote.cs').Replace('/','\')
$argsBuild+=(Join-Path $PSScriptRoot 'GpuPathCollectorTests.cs')
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @argsBuild
if($LASTEXITCODE -ne 0){throw 'GPU path collector test compilation failed'}
& (Join-Path $gpuEvidence 'GpuPathCollectorTests.exe')
if($LASTEXITCODE -ne 0){throw 'GPU path collector test failed'}
