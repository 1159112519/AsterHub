$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot)
$out=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-AnalysisProcess-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $out | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@{}
foreach($name in @('AsterHub.Core','AsterHub.Storage','AsterHub.Platform.Windows')){
 $folder=Join-Path $root ('backend/src/'+$name)
 [xml]$def=Get-Content -LiteralPath (Join-Path $folder ($name+'.csproj')) -Raw
 $dll=Join-Path $out ($name+'.dll')
 $argsBuild=@('/nologo','/target:library','/platform:x64',('/out:'+$dll))
 foreach($reference in $def.SelectNodes("//*[local-name()='Reference']")){
  $refName=[string]$reference.Include
  $argsBuild+=('/r:' + $(if($reference.HintPath){Join-Path $root ('app/runtime/managed/'+$refName+'.dll')}else{$refName+'.dll'}))
 }
 foreach($reference in $def.SelectNodes("//*[local-name()='ProjectReference']")){$argsBuild+=('/r:'+$refs[[string]$reference.Name])}
 foreach($source in $def.SelectNodes("//*[local-name()='Compile']")){$argsBuild+=Join-Path $folder ([string]$source.Include)}
 & $compiler @argsBuild
 if($LASTEXITCODE -ne 0){throw ('Backend compilation failed: '+$name)}
 $refs[$name]=$dll
}
$exe=Join-Path $out 'ResourceAnalysisProcessQA.exe'
$analysisSources=@('ResourceAnalysisBridge.cs','HistoryRange.cs','HistoryService.cs') | ForEach-Object { Join-Path $root ('host/'+$_) }
# The fixture exercises the real compiled sampler in the backend module.
& $compiler /nologo /platform:x64 /r:System.Web.Extensions.dll ('/r:'+$refs['AsterHub.Core']) ('/r:'+$refs['AsterHub.Storage']) ('/r:'+$refs['AsterHub.Platform.Windows']) ('/out:'+$exe) @analysisSources (Join-Path $PSScriptRoot 'ResourceAnalysisProcessQA.cs')
if($LASTEXITCODE -ne 0){throw 'Bridge regression compilation failed'}
& $exe (Join-Path $out 'archive')
if($LASTEXITCODE -ne 0){throw 'Bridge regression failed'}
$gpu=Join-Path $out 'GpuPathCollectorTests.exe'
& $compiler /nologo /platform:x64 /r:System.Web.Extensions.dll ('/r:'+$refs['AsterHub.Core']) ('/r:'+$refs['AsterHub.Storage']) ('/out:'+$gpu) (Join-Path $root 'backend/src/AsterHub.Platform.Windows/Remote.cs') (Join-Path $PSScriptRoot 'GpuPathCollectorTests.cs')
if($LASTEXITCODE -ne 0){throw 'GPU remote fixture compilation failed'}
& $gpu
if($LASTEXITCODE -ne 0){throw 'GPU remote fixture failed'}
Write-Output ('ISOLATED_TEST_EVIDENCE='+$out)
