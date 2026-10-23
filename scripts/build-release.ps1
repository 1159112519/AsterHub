param([string]$InnoCompiler,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $project 'VERSION'))).Trim()
$out=Join-Path $project 'artifacts'
$build=Join-Path $project 'desktop/bin/Release'
if(!$SkipBuild){& (Join-Path $project 'desktop/build.ps1');if($LASTEXITCODE-ne 0){throw 'Desktop build failed'}}
$stage=Join-Path $out ('staging/'+$version+'-'+[guid]::NewGuid().ToString('N')+'/AQ Server')
New-Item -ItemType Directory -Path $stage -Force | Out-Null
Copy-Item -Path (Join-Path $build '*') -Destination $stage -Recurse -Force
$items=@(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object {$_.Name-ne'payload-manifest.json'} | Sort-Object FullName | ForEach-Object {[ordered]@{File=$_.FullName.Substring($stage.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;Length=$_.Length}})
$items|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $stage 'payload-manifest.json') -Encoding UTF8
$portable=Join-Path $out ('AQServer-v'+$version+'-portable.zip')
Compress-Archive -LiteralPath $stage -DestinationPath $portable -Force
if($InnoCompiler){
 & (Join-Path $project 'installer/prepare-runtime.ps1')
 $defs=@(('/DRepoRoot='+$project),('/DPayloadDir='+$stage),('/DReleaseDir='+$out),('/DAppVersion='+$version))
 & $InnoCompiler /Q @defs (Join-Path $project 'installer/AQServer.iss')
 if($LASTEXITCODE-ne 0){throw 'Installer compilation failed'}
 & $InnoCompiler /Q /DQAInstall @defs (Join-Path $project 'installer/AQServer.iss')
 if($LASTEXITCODE-ne 0){throw 'Isolated installer compilation failed'}
}
[ordered]@{version=$version;installedPayload=$stage;portable=$portable;installer=(Join-Path $out ('AQServer-Setup-'+$version+'.exe'));layoutVersion=2}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'build.json') -Encoding UTF8
Write-Output ('PORTABLE='+$portable)
Write-Output ('INSTALLED_PAYLOAD='+$stage)
