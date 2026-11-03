param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$FullManifest,[Parameter(Mandatory=$true)][string]$Bootstrap,[Parameter(Mandatory=$true)][string]$BootstrapUrl,[Parameter(Mandatory=$true)][string]$Output,[string[]]$Mirrors=@())
$ErrorActionPreference='Stop'
$envelope=Get-Content -LiteralPath $FullManifest -Raw -Encoding UTF8 | ConvertFrom-Json
$payload=[Convert]::FromBase64String($envelope.payload)
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKey)))
 if(!$rsa.VerifyData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'),[Convert]::FromBase64String($envelope.signature))){throw 'Full installer manifest signature invalid'}
 $release=[Text.Encoding]::UTF8.GetString($payload) | ConvertFrom-Json
 foreach($address in @($BootstrapUrl)+$Mirrors){$uri=[Uri]$address;if(!$uri.IsAbsoluteUri-or$uri.Scheme-ne'https'-or$uri.UserInfo-or!$uri.AbsolutePath.EndsWith('.exe')){throw 'Bootstrap must use HTTPS executable URLs'}}
 if($Mirrors.Count-gt 3){throw 'Too many mirrors'}
 $release.installerUrl=$BootstrapUrl
 $release.installerSha256=(Get-FileHash -LiteralPath $Bootstrap).Hash.ToLowerInvariant()
 $release.installerBytes=(Get-Item -LiteralPath $Bootstrap).Length
 $release | Add-Member -NotePropertyName installerUrls -NotePropertyValue @($Mirrors) -Force
 $bytes=[Text.Encoding]::UTF8.GetBytes(($release|ConvertTo-Json -Compress))
 $signed=@{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($rsa.SignData($bytes,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))}
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($Output),($signed|ConvertTo-Json -Compress),[Text.UTF8Encoding]::new($false))
}finally{$rsa.Dispose()}
Write-Output 'SIGNED_LEGACY_BOOTSTRAP_CHANNEL=True'
