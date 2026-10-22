$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot 'redist'
New-Item -ItemType Directory -Path $folder -Force|Out-Null
$file=Join-Path $folder 'MicrosoftEdgeWebview2Setup.exe'
if(!(Test-Path -LiteralPath $file)){Invoke-WebRequest -UseBasicParsing -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $file}
$signature=Get-AuthenticodeSignature -LiteralPath $file
if($signature.Status-ne 'Valid' -or $signature.SignerCertificate.Subject-notlike '*O=Microsoft Corporation*'){throw 'Microsoft WebView2 bootstrapper signature is invalid'}
Write-Output ('WEBVIEW2_BOOTSTRAPPER_SIGNATURE='+$signature.Status)
