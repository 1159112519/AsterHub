param([string]$InnoCompiler,[switch]$SkipBuild)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$source=Join-Path $project 'desktop'
$out=Join-Path $project 'artifacts'
$version=([IO.File]::ReadAllText((Join-Path $project 'VERSION'))).Trim()
if(!$SkipBuild){& (Join-Path $source 'build.ps1');if($LASTEXITCODE-ne 0){throw 'Desktop build failed'}}
New-Item -ItemType Directory -Path $out -Force | Out-Null
$stage=Join-Path $out ('payload-'+$version+'-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'AQServer.App.exe') -Destination $stage
Get-ChildItem -LiteralPath (Join-Path $source 'deps/sshnet') -Filter *.dll | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $stage}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll')){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $stage}
foreach($name in @('TerminalAssets','UiAssets')){Copy-Item -LiteralPath (Join-Path $source $name) -Destination $stage -Recurse}
$licenses=Join-Path $stage 'Licenses'
New-Item -ItemType Directory -Path $licenses | Out-Null
Get-ChildItem -LiteralPath (Join-Path $source 'deps') -Recurse -File | Where-Object {$_.Name -match '^(LICENSE|COPYING|NOTICE)' -or $_.Name -like '*LICENSE*.txt'} | ForEach-Object {$relative=$_.FullName.Substring((Join-Path $source 'deps').Length+1).Replace('\','_');Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenses $relative)}
Copy-Item -LiteralPath (Join-Path $source 'deps/sshnet/sha256.json') -Destination (Join-Path $licenses 'sshnet-dependencies-sha256.json')
Copy-Item -LiteralPath (Join-Path $project 'docs/DATA-AND-INSTALL.md') -Destination (Join-Path $stage 'README.md')
Copy-Item -LiteralPath (Join-Path $project 'VERSION') -Destination $stage
function Write-Manifest([string]$folder){$items=@(Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object {$_.Name-ne 'payload-manifest.json'} | ForEach-Object {[ordered]@{File=$_.FullName.Substring($folder.Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;Length=$_.Length}});$items|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $folder 'payload-manifest.json') -Encoding UTF8}
Write-Manifest $stage
$payload=Join-Path $out ('AQPayload-'+$version+'.zip')
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $payload -Force
$framework=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$portable=Join-Path $out ('AQServer-v'+$version+'.exe')
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /main:AQLauncher ('/out:'+$portable) ('/win32manifest:'+(Join-Path $source 'app.manifest')) ('/win32icon:'+(Join-Path $source 'AQServer.ico')) ('/resource:'+$payload+',AQPayload.zip') /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll (Join-Path $source 'Launcher.cs') (Join-Path $source 'AssemblyInfo.cs')
if($LASTEXITCODE-ne 0){throw 'Portable launcher build failed'}
$installed=Join-Path $out ('installed-'+$version+'-'+[guid]::NewGuid().ToString('N'))
Copy-Item -LiteralPath $stage -Destination $installed -Recurse
Rename-Item -LiteralPath (Join-Path $installed 'AQServer.App.exe') -NewName 'AQServer.exe'
Write-Manifest $installed
Compress-Archive -Path (Join-Path $installed '*') -DestinationPath (Join-Path $out ('AQServer-v'+$version+'-portable.zip')) -Force
if($InnoCompiler){
 & (Join-Path $project 'installer/prepare-runtime.ps1')
 $defs=@(('/DRepoRoot='+$project),('/DPayloadDir='+$installed),('/DReleaseDir='+$out),('/DAppVersion='+$version))
 & $InnoCompiler /Q @defs (Join-Path $project 'installer/AQServer.iss')
 if($LASTEXITCODE-ne 0){throw 'Installer compilation failed'}
 & $InnoCompiler /Q /DQAInstall @defs (Join-Path $project 'installer/AQServer.iss')
 if($LASTEXITCODE-ne 0){throw 'Isolated installer compilation failed'}
}
[ordered]@{version=$version;portablePayload=$stage;installedPayload=$installed;portable=$portable}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'build.json') -Encoding UTF8
Write-Output ('PORTABLE='+$portable)
Write-Output ('INSTALLED_PAYLOAD='+$installed)
