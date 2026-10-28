param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$Package,[Parameter(Mandatory=$true)][string]$Metadata,[Parameter(Mandatory=$true)][string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $root 'linux/VERSION'))).Trim()
$build=Get-Content -LiteralPath $Metadata -Raw -Encoding UTF8|ConvertFrom-Json
if($build.version-ne$version){throw 'Ubuntu metadata version mismatch'}
$hash=(Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash.ToLowerInvariant()
if($hash-ne$build.sha256-or(Get-Item -LiteralPath $Package).Length-ne$build.bytes){throw 'Ubuntu package hash/size mismatch'}
$release=[ordered]@{appId='AQServer';platform='ubuntu';version=$version;publishedUtc=[DateTime]::UtcNow.ToString('o');package=$build.package;installerUrl='http://8.140.49.4/aqserver/releases/'+$build.package;installerSha256=$hash;installerBytes=$build.bytes;testedSystems=$build.testedSystems;architecture=$build.architecture;minimumPython=$build.minimumPython;installCommand=$build.installCommand}
$payload=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress -Depth 5))
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
 $rsa.FromXmlString([IO.File]::ReadAllText($SigningKey))
 $signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $envelope=[ordered]@{algorithm='RSA-SHA256';payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)}
 New-Item -ItemType Directory -Path $ReleaseDirectory -Force|Out-Null
 [IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'ubuntu.json'),($envelope|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding($false)))
 [IO.File]::WriteAllText((Join-Path $ReleaseDirectory 'release-public-key.xml'),$rsa.ToXmlString($false),(New-Object Text.UTF8Encoding($false)))
 Copy-Item -LiteralPath $Package -Destination $ReleaseDirectory -Force
 Write-Output ('SIGNED_UBUNTU_RELEASE='+$version)
}finally{$rsa.Dispose()}
