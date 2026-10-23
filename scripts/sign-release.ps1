param([Parameter(Mandatory=$true)][string]$SigningKey,[string]$BaseUrl='http://8.140.49.4/aqserver',[string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $root 'VERSION'))).Trim()
$name='AQServer-Setup-'+$version+'.exe'
$installer=Join-Path $root ('artifacts/'+$name)
$releaseDir=if($ReleaseDirectory){[IO.Path]::GetFullPath($ReleaseDirectory)}else{Join-Path $root 'website/public/releases'}
$changelog=[IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
$entry=[regex]::Match($changelog,'(?ms)^## '+[regex]::Escape($version)+'[^\r\n]*\r?\n(.*?)(?=^## |\z)')
if(!$entry.Success){throw 'Current version has no changelog entry'}
$notes=$entry.Groups[1].Value.Trim()
New-Item -ItemType Directory -Force -Path $releaseDir|Out-Null
$release=[ordered]@{appId='AQServer';version=$version;publishedUtc=[DateTime]::UtcNow.ToString('o');notes=$notes;installerUrl=$BaseUrl.TrimEnd('/')+'/releases/'+$name;installerSha256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant();installerBytes=(Get-Item -LiteralPath $installer).Length}
$payload=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress))
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
$rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKey)))
try{$signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'));$envelope=[ordered]@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)};[IO.File]::WriteAllText((Join-Path $releaseDir 'stable.json'),($envelope|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding($false)))}finally{$rsa.Dispose()}
Copy-Item -LiteralPath $installer -Destination $releaseDir -Force
[IO.File]::WriteAllText((Join-Path $releaseDir 'SHA256SUMS.txt'),($release.installerSha256+'  '+$name+[Environment]::NewLine),(New-Object Text.UTF8Encoding($false)))
Write-Output ('SIGNED_RELEASE='+$version)
