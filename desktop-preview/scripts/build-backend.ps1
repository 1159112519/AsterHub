param([string]$Root)
$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$modules=@{}
foreach($name in @('AQServer.Core','AQServer.Storage','AQServer.Platform.Windows')){
 $folder=Join-Path $Root ('backend/src/'+$name)
 [xml]$definition=Get-Content -LiteralPath (Join-Path $folder ($name+'.csproj')) -Raw
 $out=Join-Path $Root ('app/app/modules/'+$name+'.dll')
 $argsBuild=@('/nologo','/target:library','/platform:x64','/optimize+',('/out:'+$out))
 foreach($reference in $definition.SelectNodes("//*[local-name()='Reference']")){
  $refName=[string]$reference.Include
  $refPath=if($reference.HintPath){Join-Path $Root ('app/runtime/managed/'+$refName+'.dll')}else{$refName+'.dll'}
  $argsBuild+=('/reference:'+$refPath)
 }
 foreach($reference in $definition.SelectNodes("//*[local-name()='ProjectReference']")){$argsBuild+=('/reference:'+$modules[[string]$reference.Name])}
 foreach($source in $definition.SelectNodes("//*[local-name()='Compile']")){$argsBuild+=[IO.Path]::GetFullPath((Join-Path $folder ([string]$source.Include)))}
 & (Join-Path $framework 'csc.exe') @argsBuild
 if($LASTEXITCODE -ne 0){throw ('Backend compile failed: '+$name)}
 $modules[$name]=$out
}
