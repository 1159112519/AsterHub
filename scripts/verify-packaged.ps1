param([string]$ReleaseManifest,[string]$ReleaseInstaller,[string[]]$Tests)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$version=([IO.File]::ReadAllText((Join-Path $project 'VERSION'))).Trim()
$qa=Join-Path $project ('artifacts/qa/packaged-'+$version+'-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $qa -Force | Out-Null
Expand-Archive -LiteralPath (Join-Path $project ('artifacts/AQServer-v'+$version+'-portable.zip')) -DestinationPath $qa
$payload=Join-Path $qa 'AQ Server'
$exe=Join-Path $payload 'AQServer.exe'
$env:AQSERVER_RUNTIME_ROOT=Join-Path $qa 'runtime'
$env:AQSERVER_WEBVIEW_DATA=Join-Path $qa 'webview-profile'
$env:AQSERVER_LAYOUT_PATH=Join-Path $qa 'layout.xml'
$env:AQSERVER_RELEASE_MANIFEST=$ReleaseManifest
$env:AQSERVER_RELEASE_INSTALLER=$ReleaseInstaller
$manifest=Get-Content -LiteralPath (Join-Path $payload 'payload-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest){$path=Join-Path $payload $file.File;if(!(Test-Path -LiteralPath $path)-or(Get-FileHash -LiteralPath $path).Hash-ne$file.SHA256){throw ('Payload mismatch '+$file.File)}}
$results=@('PAYLOAD_HASHES_VERIFIED='+$manifest.Count)
$known=@('module-tests','peripheral-tests','tray-tests','v124-native','update-tests','footer-layout-tests','startup-smoke','v122-native','v12-tests','workspace-tests','terminal-tests','theme-tests','v5-tests','history-process-tests','v6-tests')
if(!$Tests){$Tests=$known}
foreach($test in $Tests){if($test-notin$known){throw ('Unknown packaged regression '+$test)}}
foreach($test in $Tests){
 $dir=Join-Path $qa $test
 New-Item -ItemType Directory -Path $dir -Force | Out-Null
 $p=Start-Process -FilePath $exe -WorkingDirectory $qa -ArgumentList @(('--'+$test),('"'+$dir+'"')) -WindowStyle Hidden -PassThru
 if(!$p.WaitForExit(120000)){Stop-Process -Id $p.Id -Force;throw ('Timed out: '+$test)}
 $p.Refresh();$line=$test+' EXIT='+$p.ExitCode
 $results+=$line;Write-Output $line
 $results|Set-Content -LiteralPath (Join-Path $qa 'packaged-results.txt') -Encoding UTF8
 if($p.ExitCode-ne 0){Get-ChildItem -LiteralPath $dir -Filter '*error.txt'|ForEach-Object{Get-Content -LiteralPath $_.FullName};throw ('Failed '+$test)}
}
Write-Output ('PACKAGED_QA='+$qa)
