param([switch]$Console,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
if(!$OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot 'bin/Release'}
$out=[IO.Path]::GetFullPath($OutputDirectory)
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
if(!(Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))){throw 'The .NET Framework x64 compiler is required.'}
foreach($folder in @('app/modules','runtime/managed','runtime/win-x64','resources/terminal','resources/ui','licenses','docs')){New-Item -ItemType Directory -Path (Join-Path $out $folder) -Force | Out-Null}
$moduleOutputs=@{}
$modules=@('AQServer.Core','AQServer.Storage','AQServer.Platform.Windows','AQServer.Desktop','AQServer.Bootstrap')
foreach($module in $modules){
 $folder=Join-Path $PSScriptRoot ('src/'+$module)
 [xml]$definition=Get-Content -LiteralPath (Join-Path $folder ($module+'.csproj')) -Raw
 $isBootstrap=$module-eq'AQServer.Bootstrap'
 $destination=if($isBootstrap){Join-Path $out 'AQServer.exe'}elseif($module-eq'AQServer.Desktop'){Join-Path $out 'app/AQServer.Desktop.dll'}else{Join-Path $out ('app/modules/'+$module+'.dll')}
 $target=if($isBootstrap){if($Console){'exe'}else{'winexe'}}else{'library'}
 $arguments=@('/nologo',('/target:'+$target),'/optimize+','/platform:x64',('/out:'+$destination))
 if($isBootstrap){$arguments+=@('/main:AQBootstrap',('/win32manifest:'+(Join-Path $PSScriptRoot 'assets/app.manifest')),('/win32icon:'+(Join-Path $PSScriptRoot 'assets/AQServer.ico')))}
 if($module-eq'AQServer.Desktop'){$arguments+=('/resource:'+(Join-Path $PSScriptRoot 'assets/AQServer.ico')+',AQServer.ico')}
 foreach($reference in $definition.SelectNodes("//*[local-name()='Reference']")){
  $name=[string]$reference.Include
  if($reference.HintPath){$path=[IO.Path]::GetFullPath((Join-Path $folder ([string]$reference.HintPath)))}
  elseif($name-in@('WindowsBase','PresentationCore','PresentationFramework')){$path=Join-Path $framework ('WPF/'+$name+'.dll')}
  else{$path=$name+'.dll'}
  $arguments+=('/reference:'+$path)
 }
 foreach($reference in $definition.SelectNodes("//*[local-name()='ProjectReference']")){$arguments+=('/reference:'+$moduleOutputs[[string]$reference.Name])}
 foreach($source in $definition.SelectNodes("//*[local-name()='Compile']")){$arguments+=[IO.Path]::GetFullPath((Join-Path $folder ([string]$source.Include)))}
 & (Join-Path $framework 'csc.exe') @arguments
 if($LASTEXITCODE-ne 0){throw ('Compilation failed: '+$module)}
 $moduleOutputs[$module]=$destination
 Write-Output ('BUILT='+$module)
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/AQServer.Bootstrap/App.config') -Destination (Join-Path $out 'AQServer.exe.config') -Force
foreach($dll in (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'deps/sshnet') -Filter *.dll)){Copy-Item -LiteralPath $dll.FullName -Destination (Join-Path $out 'runtime/managed') -Force}
foreach($name in @('Core','Wpf')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('deps/webview2/sdk/lib/net462/Microsoft.Web.WebView2.'+$name+'.dll')) -Destination (Join-Path $out 'runtime/managed') -Force}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'deps/webview2/sdk/runtimes/win-x64/native/WebView2Loader.dll') -Destination (Join-Path $out 'runtime/win-x64') -Force
foreach($asset in @('terminal','ui')){Copy-Item -Path (Join-Path $PSScriptRoot ('assets/'+$asset+'/*')) -Destination (Join-Path $out ('resources/'+$asset)) -Recurse -Force}
Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'deps') -Recurse -File | Where-Object {$_.Name-match'^(LICENSE|COPYING|NOTICE)' -or $_.Name-like'*LICENSE*.txt'} | ForEach-Object {$relative=$_.FullName.Substring((Join-Path $PSScriptRoot 'deps').Length+1).Replace('\','_');Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $out ('licenses/'+$relative)) -Force}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'deps/sshnet/sha256.json') -Destination (Join-Path $out 'licenses/sshnet-dependencies-sha256.json') -Force
foreach($document in @('DATA-AND-INSTALL.md','ARCHITECTURE.md')){if(Test-Path -LiteralPath (Join-Path $project ('docs/'+$document))){Copy-Item -LiteralPath (Join-Path $project ('docs/'+$document)) -Destination (Join-Path $out 'docs') -Force}}
Copy-Item -LiteralPath (Join-Path $project 'VERSION') -Destination $out -Force
$components=@($moduleOutputs.GetEnumerator() | Sort-Object Name | ForEach-Object {[ordered]@{name=$_.Key;file=$_.Value.Substring($out.Length+1).Replace('\','/');version=[Reflection.AssemblyName]::GetAssemblyName($_.Value).Version.ToString()}})
[ordered]@{version=([IO.File]::ReadAllText((Join-Path $project 'VERSION'))).Trim();architecture='x64';components=$components}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'components.json') -Encoding UTF8
Write-Output ('BUILD_ROOT='+$out)
