param([Parameter(Mandatory=$true)][string]$FullManifest,[Parameter(Mandatory=$true)][string]$Output,[switch]$QA)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$files=@('host/AppUpdates.cs','host/UpdateDownload.cs','host/UpdateSources.cs','host/UpdatePublicKey.cs','updater/BootstrapCore.cs','updater/BootstrapWindow.cs') | ForEach-Object {Join-Path $root $_}
$args=@('/nologo','/target:winexe','/optimize+','/r:System.Web.Extensions.dll','/r:System.Windows.Forms.dll','/r:System.Drawing.dll',('/out:'+([IO.Path]::GetFullPath($Output))),('/resource:'+([IO.Path]::GetFullPath($FullManifest))+',release.json'))
if($QA){$args+='/define:BOOTSTRAP_QA'}
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" @args @files
if($LASTEXITCODE-ne 0){throw 'Upgrade bootstrap build failed'}
Get-Item -LiteralPath $Output | Select-Object Name,Length
