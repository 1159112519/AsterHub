param([string]$Compiler,[string]$Bootstrapper,[switch]$QAInstall)
$ErrorActionPreference='Stop'
$previewRoot=Split-Path $PSScriptRoot
$workspaceRoot=Split-Path $previewRoot
if(!$Compiler){$Compiler=Join-Path $workspaceRoot 'work/installer-tools/inno/ISCC.exe'}
if(!$Bootstrapper){$Bootstrapper=Join-Path $workspaceRoot 'installer/redist/MicrosoftEdgeWebview2Setup.exe'}
$signature=Get-AuthenticodeSignature -LiteralPath $Bootstrapper
if($signature.Status-ne 'Valid'-or $signature.SignerCertificate.Subject-notmatch 'Microsoft Corporation'){throw 'WebView2 bootstrapper Microsoft signature is not valid.'}
$payload=Join-Path $PSScriptRoot ('payload-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payload | Out-Null
$source=Join-Path $previewRoot 'app'
$manifest=Get-Content -LiteralPath (Join-Path $source 'payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($manifest.version-ne '0.3.0-preview.7'){throw 'Unexpected preview payload version.'}
foreach($file in $manifest.files){
 $inputFile=Join-Path $source $file.file
 if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash-ne $file.sha256){throw ('Source payload differs from verified manifest: '+$file.file)}
 $outputFile=Join-Path $payload $file.file
 New-Item -ItemType Directory -Path (Split-Path $outputFile) -Force | Out-Null
 Copy-Item -LiteralPath $inputFile -Destination $outputFile
}
# Installation instructions only: executable and runtime files remain byte-for-byte identical.
$notice=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'notice.txt') -Raw
$installedReadme=$notice+"`r`n启动：开始菜单中的 AsterHub Preview，或安装目录的 AsterHub.Preview.exe。`r`n请不要单独移动 EXE，同目录的 app、runtime、resources 等目录均为程序所需模块。`r`n"
foreach($name in @('README.txt','README-预览版.txt')){[IO.File]::WriteAllText((Join-Path $payload $name),$installedReadme,[Text.UTF8Encoding]::new($false))}
foreach($file in $manifest.files){$item=Get-Item -LiteralPath (Join-Path $payload $file.file);$file.bytes=$item.Length;$file.sha256=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $payload 'payload-manifest.json') -Encoding utf8
$extra=@();if($QAInstall){$extra+="/DQAInstall"}
& $Compiler @extra ('/DProjectRoot='+$previewRoot) ('/DPayloadDir='+$payload) ('/DBootstrapper='+$Bootstrapper) (Join-Path $PSScriptRoot 'AsterHubPreview.iss') *> (Join-Path $PSScriptRoot 'compiler.log')
if($LASTEXITCODE-ne 0){throw 'Inno Setup build failed.'}
$suffix=if($QAInstall){'-QA'}else{''}
$installer=Get-Item -LiteralPath (Join-Path $previewRoot ('AsterHub-Setup-0.3.0-preview.7'+$suffix+'.exe'))
@{version=$manifest.version;installer=$installer.FullName;bytes=$installer.Length;sha256=(Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash;payload=$payload;bootstrapperSha256=(Get-FileHash -LiteralPath $Bootstrapper -Algorithm SHA256).Hash;bootstrapperSignature=[string]$signature.Status} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot $(if($QAInstall){'build-qa-result.json'}else{'build-result.json'})) -Encoding utf8
Write-Output ('INSTALLER='+$installer.FullName)
