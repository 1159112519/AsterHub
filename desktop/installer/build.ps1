param([string]$Compiler,[string]$Bootstrapper,[switch]$QAInstall,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$previewRoot=Split-Path $PSScriptRoot
$workspaceRoot=Split-Path $previewRoot
if(!$Compiler){
 $compilerCommand=Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
 if($compilerCommand){$Compiler=$compilerCommand.Source}else{$Compiler=Join-Path $workspaceRoot 'work/installer-tools/inno/ISCC.exe'}
}
if(!(Test-Path -LiteralPath $Compiler)){throw 'Inno Setup 7+ compiler not found. Pass -Compiler with the verified ISCC.exe path.'}
if(!$Bootstrapper){$Bootstrapper=Join-Path $workspaceRoot 'installer/redist/MicrosoftEdgeWebview2Setup.exe'}
if(!(Test-Path -LiteralPath $Bootstrapper)){throw 'Microsoft WebView2 bootstrapper not found. Pass -Bootstrapper with the verified offline-bundled bootstrapper path.'}
$signature=Get-AuthenticodeSignature -LiteralPath $Bootstrapper
if($signature.Status-ne 'Valid'-or $signature.SignerCertificate.Subject-notmatch 'Microsoft Corporation'){throw 'WebView2 bootstrapper Microsoft signature is not valid.'}
if(!$OutputDirectory){$OutputDirectory=$previewRoot}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$payload=Join-Path $PSScriptRoot ('payload-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payload | Out-Null
$source=Join-Path $previewRoot 'app'
$manifest=Get-Content -LiteralPath (Join-Path $source 'payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version=(Get-Content -LiteralPath (Join-Path $previewRoot 'VERSION') -Raw).Trim()
if($manifest.version-ne $version-or$version-notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$'){throw 'Payload manifest and VERSION must match a Windows release version.'}
foreach($file in $manifest.files){
 $inputFile=Join-Path $source $file.file
 if((Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash-ne $file.sha256){throw ('Source payload differs from verified manifest: '+$file.file)}
 # Replace the obsolete installer-generated preview README with one formal guide.
 if($file.file-eq 'README-预览版.txt'){continue}
 $outputFile=Join-Path $payload $file.file
 New-Item -ItemType Directory -Path (Split-Path $outputFile) -Force | Out-Null
 Copy-Item -LiteralPath $inputFile -Destination $outputFile
}
$notice=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'notice.txt') -Raw -Encoding UTF8
[IO.File]::WriteAllText((Join-Path $payload 'README.txt'),$notice,[Text.UTF8Encoding]::new($false))
# Hash the complete staged payload, including the formal guide and new files.
$payloadPrefix=$payload.TrimEnd('\')+'\'
$stagedFiles=@(Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'payload-manifest.json' | Sort-Object FullName | ForEach-Object {@{file=$_.FullName.Substring($payloadPrefix.Length).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{version=$version;files=$stagedFiles} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $payload 'payload-manifest.json') -Encoding utf8
$extra=@();if($QAInstall){$extra+='/DQAInstall'}
try {
 $ErrorActionPreference='Continue' # Inno writes non-fatal hints to stderr; its exit code determines success.
 & $Compiler @extra ('/DReleaseVersion='+$version) ('/DProjectRoot='+$previewRoot) ('/DPayloadDir='+$payload) ('/DBootstrapper='+$Bootstrapper) ('/DInstallerOutputDir='+$OutputDirectory) (Join-Path $PSScriptRoot 'AsterHub.iss') *> (Join-Path $PSScriptRoot 'compiler.log')
 $compilerExit=$LASTEXITCODE
} finally { $ErrorActionPreference='Stop' }
if($compilerExit-ne 0){throw 'Inno Setup build failed. See installer/compiler.log.'}
$suffix=if($QAInstall){'-QA'}else{''}
$installer=Get-Item -LiteralPath (Join-Path $OutputDirectory ('AsterHub-Setup-'+$version+$suffix+'.exe'))
@{version=$version;qa=$QAInstall.IsPresent;installer=$installer.FullName;bytes=$installer.Length;sha256=(Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash;payload=$payload;bootstrapperSha256=(Get-FileHash -LiteralPath $Bootstrapper -Algorithm SHA256).Hash;bootstrapperSignature=[string]$signature.Status} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot $(if($QAInstall){'build-qa-result.json'}else{'build-result.json'})) -Encoding utf8
Write-Output ('INSTALLER='+$installer.FullName)
