param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string[]]$InstallerMirrors=@())
$ErrorActionPreference='Stop'
$previewRoot=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $previewRoot 'VERSION'))).Trim()
if($version-notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-preview\.\d+)?$'){throw 'Invalid release version'}
$name='AsterHub-Setup-'+$version+'.exe'
$installer=Join-Path $previewRoot $name
$build=Get-Content -LiteralPath (Join-Path $previewRoot 'installer/build-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$installer=$build.installer
if($build.version-ne $version-or[IO.Path]::GetFileName($installer)-ne $name-or(Get-FileHash -LiteralPath $installer).Hash-ne $build.sha256){throw 'Installer differs from build record'}
New-Item -ItemType Directory -Force -Path $ReleaseDirectory | Out-Null
$notes=([IO.File]::ReadAllText((Join-Path $previewRoot 'CHANGELOG.md')) -replace '(?s)^.*?## [^\r\n]+\r?\n','').Trim()
$notes=($notes -split '(?m)^## ')[0].Trim()
$release=[ordered]@{appId='AsterHubPreview';version=$version;publishedUtc=[DateTime]::UtcNow.ToString('o');notes=$notes;installerUrl=('https://github.com/1159112519/AsterHub/releases/download/v'+$version+'/'+$name);installerSha256=(Get-FileHash -LiteralPath $installer).Hash.ToLowerInvariant();installerBytes=(Get-Item -LiteralPath $installer).Length}
if($InstallerMirrors.Count -gt 3){throw 'Too many installer mirrors'}
foreach($address in $InstallerMirrors){$mirror=[Uri]$address;if(!$mirror.IsAbsoluteUri -or $mirror.Scheme -ne 'https' -or $mirror.UserInfo -or !$mirror.AbsolutePath.EndsWith('.exe')){throw 'Installer mirrors must be absolute HTTPS executable URLs'}}
if($InstallerMirrors.Count -gt 0){$release.installerUrls=@($InstallerMirrors | Select-Object -Unique)}
$payload=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress))
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{$rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKey)));$sig=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'));$envelope=@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($sig)};[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'windows-preview.json'),($envelope|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))}finally{$rsa.Dispose()}
$releaseInstaller=[IO.Path]::GetFullPath((Join-Path $ReleaseDirectory $name))
if([IO.Path]::GetFullPath($installer)-ne $releaseInstaller){Copy-Item -LiteralPath $installer -Destination $releaseInstaller}
[IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'PREVIEW-SHA256SUMS.txt'),($release.installerSha256+'  '+$name+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output ('SIGNED_PREVIEW='+$version)
