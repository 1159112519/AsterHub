param([switch]$Baseline,[string]$EvidenceRoot)
$ErrorActionPreference='Stop'
if(!$EvidenceRoot){$EvidenceRoot=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-Storage-'+[guid]::NewGuid().ToString('N'))}
$sourceRoot=Split-Path (Split-Path $PSScriptRoot)
New-Item -ItemType Directory -Force -Path $EvidenceRoot | Out-Null
# Compile current backend sources privately; the installed app modules can be older.
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$testModules=@{}
foreach($name in @('AsterHub.Core','AsterHub.Storage','AsterHub.Platform.Windows')){
 $folder=Join-Path $sourceRoot ('backend/src/'+$name)
 [xml]$definition=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $folder ($name+'.csproj')) -Raw
 $modulePath=Join-Path $EvidenceRoot ($name+'.dll')
 $moduleArgs=@('/nologo','/target:library','/platform:x64','/optimize+',('/out:'+$modulePath))
 foreach($reference in $definition.SelectNodes("//*[local-name()='Reference']")){
  $refName=[string]$reference.Include
  $refPath=if($reference.HintPath){Join-Path $sourceRoot ('app/runtime/managed/'+$refName+'.dll')}else{$refName+'.dll'}
  $moduleArgs+=('/reference:'+$refPath)
 }
 foreach($reference in $definition.SelectNodes("//*[local-name()='ProjectReference']")){$moduleArgs+=('/reference:'+$testModules[[string]$reference.Name])}
 foreach($source in $definition.SelectNodes("//*[local-name()='Compile']")){$moduleArgs+=[IO.Path]::GetFullPath((Join-Path $folder ([string]$source.Include)))}
 & (Join-Path $framework 'csc.exe') @moduleArgs
 if($LASTEXITCODE -ne 0){throw ('Isolated storage dependency compile failed: '+$name)}
 $testModules[$name]=$modulePath
}
# Compile the real scan/index and real file connection; isolate only unrelated host wiring.
# Windows PowerShell 5 defaults to ANSI for BOM-less files; source is UTF-8.
$files=Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $sourceRoot 'host/FilesBridge.cs')
$start=$files.IndexOf('public static class FileTransferCopy {')
$end=$files.IndexOf(' static string BaseName(', $start)
if($start -lt 0 -or $end -le $start){throw 'Could not locate the real file normalization source'}
$normalizer='using System;using System.IO;using System.Linq;'+$files.Substring($start,$end-$start)+'}'
$normalizerPath=Join-Path $EvidenceRoot 'FileNormalization.cs'
[IO.File]::WriteAllText($normalizerPath,$normalizer,[Text.Encoding]::UTF8)
$refs=@('System.dll','System.Core.dll','System.Web.Extensions.dll',(Join-Path $EvidenceRoot 'AsterHub.Core.dll'),(Join-Path $EvidenceRoot 'AsterHub.Platform.Windows.dll'))
$testExe=Join-Path $EvidenceRoot 'StorageRegressionTests.exe'
$compilerArgs=@('/nologo',('/out:'+$testExe))
if($Baseline){$compilerArgs+='/define:BASELINE'}
foreach($ref in $refs){$compilerArgs+='/r:'+$ref}
$compilerArgs+=@((Join-Path $sourceRoot 'host/StorageIndex.cs'),$normalizerPath,(Join-Path $PSScriptRoot 'StorageTests.cs'))
$compilerArgs+=(Join-Path $sourceRoot 'host/LocalStorageScan.cs')
& "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe" @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Storage regression compile failed'}
Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'app/runtime/managed') -Filter *.dll | Copy-Item -Destination $EvidenceRoot -Force
& $testExe | Tee-Object -FilePath (Join-Path $EvidenceRoot $(if($Baseline){'red.txt'}else{'green.txt'}))
if($LASTEXITCODE -ne 0 -or $Baseline){exit $LASTEXITCODE}
# Native enumeration uses the same real index/connection and metadata normalizer.
$nativeExe=Join-Path $EvidenceRoot 'NativeStorageEnumerationTests.exe'
$nativeArgs=@('/nologo','/optimize+','/platform:x64','/main:ScanAccelerationTests',('/out:'+$nativeExe))
foreach($ref in $refs){$nativeArgs+='/r:'+$ref}
$nativeArgs+=@((Join-Path $sourceRoot 'host/StorageIndex.cs'),(Join-Path $sourceRoot 'host/LocalStorageScan.cs'),$normalizerPath,(Join-Path $PSScriptRoot 'StorageTests.cs'),(Join-Path $PSScriptRoot 'NativeStorageEnumerationTests.cs'))
& (Join-Path $framework 'csc.exe') @nativeArgs
if($LASTEXITCODE -ne 0){throw 'Native enumeration regression compile failed'}
& $nativeExe | Tee-Object -FilePath (Join-Path $EvidenceRoot 'native-green.txt')
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}

# Compile into the evidence directory only; never replace the installed/shared application.
$appRoot=Join-Path $sourceRoot 'app'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$hostExe=Join-Path $EvidenceRoot 'AsterHub.StorageRegressionHost.exe'
$hostRefs=@('System.Windows.Forms.dll','System.Drawing.dll','System.dll','System.Core.dll','System.Web.Extensions.dll','System.Xaml.dll',(Join-Path $framework 'WPF/WindowsBase.dll'),(Join-Path $framework 'WPF/PresentationCore.dll'),(Join-Path $framework 'WPF/PresentationFramework.dll'),(Join-Path $framework 'WPF/UIAutomationClient.dll'),(Join-Path $framework 'WPF/UIAutomationTypes.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Core.dll'),(Join-Path $appRoot 'runtime/managed/Microsoft.Web.WebView2.Wpf.dll'),(Join-Path $EvidenceRoot 'AsterHub.Core.dll'),(Join-Path $EvidenceRoot 'AsterHub.Storage.dll'),(Join-Path $EvidenceRoot 'AsterHub.Platform.Windows.dll'))
$hostArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:'+$hostExe))
foreach($ref in $hostRefs){$hostArgs+='/r:'+$ref}
$hostArgs+=@(Get-ChildItem (Join-Path $sourceRoot 'host') -Filter *.cs -Recurse | ForEach-Object {$_.FullName})
& (Join-Path $framework 'csc.exe') @hostArgs | Tee-Object -FilePath (Join-Path $EvidenceRoot 'host-compile.txt')
if($LASTEXITCODE -ne 0){throw 'Isolated storage host compile failed'}
$bridgeExe=Join-Path $EvidenceRoot 'StorageBridgeTests.exe'
$bridgeArgs=@('/nologo',('/out:'+$bridgeExe),'/r:System.dll','/r:System.Core.dll','/r:System.Xaml.dll',('/r:'+$hostExe),('/r:'+(Join-Path $EvidenceRoot 'AsterHub.Core.dll')),('/r:'+(Join-Path $framework 'WPF/PresentationFramework.dll')),('/r:'+(Join-Path $framework 'WPF/PresentationCore.dll')),('/r:'+(Join-Path $framework 'WPF/WindowsBase.dll')),(Join-Path $PSScriptRoot 'StorageBridgeTests.cs'))
& (Join-Path $framework 'csc.exe') @bridgeArgs
if($LASTEXITCODE -ne 0){throw 'Storage bridge regression compile failed'}
& $bridgeExe | Tee-Object -FilePath (Join-Path $EvidenceRoot 'bridge-green.txt')
exit $LASTEXITCODE
