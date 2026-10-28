param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$Website,[int]$ValidDays=365)
$ErrorActionPreference='Stop'
$site=[Uri]$Website
if($site.Scheme-ne'https'-or$site.UserInfo-or$site.Query-or$site.Fragment-or$site.AbsolutePath-ne'/aqserver/'){throw 'Use the verified public HTTPS /aqserver/ website.'}
$root=Split-Path $PSScriptRoot
$info=[ordered]@{appId='AQServerServices';website=$site.AbsoluteUri;feedbackEndpoint=([Uri]::new($site,'api/feedback')).AbsoluteUri;expiresUtc=[DateTime]::UtcNow.AddDays($ValidDays).ToString('o')}
$payload=[Text.Encoding]::UTF8.GetBytes(($info|ConvertTo-Json -Compress))
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKey)))
 $signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $envelope=[ordered]@{payload=[Convert]::ToBase64String($payload);signature=[Convert]::ToBase64String($signature)}
 [IO.File]::WriteAllText((Join-Path $root 'website/public/releases/services.json'),($envelope|ConvertTo-Json -Compress),(New-Object Text.UTF8Encoding($false)))
}finally{$rsa.Dispose()}
Write-Output ('SIGNED_SERVICE_DISCOVERY='+$info.feedbackEndpoint)
