$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot
$qa=Join-Path $project 'artifacts/qa/release-final'
New-Item -ItemType Directory -Path $qa -Force | Out-Null
$env:AQSERVER_RUNTIME_ROOT=Join-Path $qa 'runtime'
$env:AQSERVER_WEBVIEW_DATA=Join-Path $qa 'webview-profile'
$exe=Join-Path $project 'artifacts/AQServer-v0.1.0.exe'
$env:AQSERVER_LAYOUT_PATH=Join-Path $qa 'layout.xml'
$env:AQSERVER_RELEASE_MANIFEST=Join-Path $project 'website/public/releases/stable.json'
$env:AQSERVER_RELEASE_INSTALLER=Join-Path $project 'artifacts/AQServer-Setup-0.1.0.exe'
$results=@()
foreach($test in @('tray-tests','v124-native','update-tests','footer-layout-tests','startup-smoke','v122-native','v12-tests','workspace-tests','terminal-tests','theme-tests')){
 $dir=Join-Path $qa $test
 New-Item -ItemType Directory -Path $dir -Force | Out-Null
 $p=Start-Process -FilePath $exe -ArgumentList @(('--'+$test),('"'+$dir+'"')) -WindowStyle Hidden -PassThru -Wait
 $line=$test+' EXIT='+$p.ExitCode
 $results+=$line
 Write-Output $line
 $results | Set-Content (Join-Path $qa 'packaged-results.txt') -Encoding UTF8
 if($p.ExitCode -ne 0){Get-ChildItem -LiteralPath $dir -Filter '*error.txt' | ForEach-Object {Get-Content -LiteralPath $_.FullName};exit 1}
}
$expected=(Get-FileHash (Join-Path $project 'desktop/AQServer.App.exe')).Hash
$roots=@(Get-ChildItem $env:AQSERVER_RUNTIME_ROOT -Directory | Where-Object {(Get-FileHash (Join-Path $_.FullName 'AQServer.App.exe')).Hash -eq $expected})
if($roots.Count -ne 1){throw 'packaged app does not match source build'}
$manifest=Get-Content (Join-Path $roots[0].FullName 'payload-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest){if((Get-FileHash (Join-Path $roots[0].FullName $file.File)).Hash -ne $file.SHA256){throw ('payload mismatch: '+$file.File)}}
$results+='PACKAGED_APP_EQUALS_SOURCE_BUILD=True'
$results+=('PAYLOAD_HASHES_VERIFIED='+$manifest.Count)
$results | Set-Content (Join-Path $qa 'packaged-results.txt') -Encoding UTF8

