param([string]$EvidenceRoot)
$ErrorActionPreference='Stop'
$previewRoot=Split-Path (Split-Path $PSScriptRoot)
if(-not $EvidenceRoot){$EvidenceRoot=Join-Path ([IO.Path]::GetTempPath()) ('AsterHub-LocalLight-'+[guid]::NewGuid().ToString('N'))}
New-Item -ItemType Directory -Path $EvidenceRoot -Force | Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$out=Join-Path $EvidenceRoot 'LocalLightCaptureTests.exe'
& $compiler /nologo /r:System.Web.Extensions.dll ('/out:'+$out) (Join-Path $previewRoot 'backend/src/AQServer.Platform.Windows/LocalLightCapture.cs') (Join-Path $PSScriptRoot 'LocalLightCaptureTests.cs')
if($LASTEXITCODE-ne 0){throw 'Local-light regression compilation failed'}
& $out | Tee-Object -FilePath (Join-Path $EvidenceRoot 'light-native-test.json')
if($LASTEXITCODE-ne 0){throw 'Local-light regression failed'}
Write-Output ('LOCAL_LIGHT_EVIDENCE='+$EvidenceRoot)
