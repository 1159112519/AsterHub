$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$qa=Join-Path $root ('artifacts/qa/installer-'+[guid]::NewGuid().ToString('N'))
$destination=Join-Path $qa 'program'
New-Item -ItemType Directory -Force -Path $qa | Out-Null
$installer=Join-Path $root 'artifacts/AQServer-Setup-QA-0.1.0.exe'
$results=@('Installer flow uses the same payload and install/uninstall code; QA AppId and mutex are isolated, startup registration is excluded.')
$p=Start-Process -FilePath $installer -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOICONS',('/DIR="'+$destination+'"'),('/LOG="'+(Join-Path $qa 'install.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw ('Installer exit '+$p.ExitCode)}
$results+='INSTALL_EXIT=0'
$installed=Join-Path $destination 'AQServer.exe'
if((Get-FileHash -LiteralPath $installed).Hash -ne (Get-FileHash -LiteralPath (Join-Path $root 'desktop/AQServer.App.exe')).Hash){throw 'Installed EXE differs from release'}
$results+='INSTALLED_EXE_EQUALS_BUILT_APP=True'
$manifest=Get-Content (Join-Path $destination 'payload-manifest.json') -Raw | ConvertFrom-Json
foreach($file in $manifest){if((Get-FileHash -LiteralPath (Join-Path $destination $file.File)).Hash -ne $file.SHA256){throw ('Installed payload mismatch '+$file.File)}}
foreach($folder in @('TerminalAssets','UiAssets','Licenses')){if(!(Test-Path -LiteralPath (Join-Path $destination $folder))){throw ('Missing program directory '+$folder)}}
$results+=('INSTALLED_PAYLOAD_HASHES_VERIFIED='+$manifest.Count)
$env:AQSERVER_RELEASE_MANIFEST=Join-Path $root 'website/public/releases/stable.json'
$env:AQSERVER_RELEASE_INSTALLER=Join-Path $root 'artifacts/AQServer-Setup-0.1.0.exe'
$env:AQSERVER_RUNTIME_ROOT=Join-Path $qa 'runtime'
$p=Start-Process -FilePath $installed -ArgumentList @('--update-tests',('"'+(Join-Path $qa 'app-check')+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw 'Installed app update checks failed'}
$results+='INSTALLED_APP_CHECKS=PASS'
$p=Start-Process -FilePath $installed -ArgumentList @('--tray-tests',('"'+(Join-Path $qa 'tray-check')+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode-ne 0){throw 'Installed tray test failed'}
$results+='INSTALLED_TRAY_CHECKS=PASS'
$p=Start-Process -FilePath $installed -ArgumentList @('--startup-smoke',('"'+(Join-Path $qa 'startup-check')+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode-ne 0){throw 'Installed startup smoke failed'}
$results+='INSTALLED_STARTUP_CHECKS=PASS'
$data=Join-Path $qa 'user-data'
New-Item -ItemType Directory -Path $data | Out-Null
[IO.File]::WriteAllText((Join-Path $data 'sentinel.txt'),'preserve outside install directory')
$p=Start-Process -FilePath (Join-Path $destination 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $qa 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if($p.ExitCode -ne 0){throw ('Uninstaller exit '+$p.ExitCode)}
# Inno's uninstaller helper may complete cleanup shortly after its parent exits.
$deadline=[DateTime]::UtcNow.AddSeconds(10)
while((Test-Path -LiteralPath $installed) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 200}
if(Test-Path -LiteralPath $installed){throw 'Installed app not removed'}
if(!(Test-Path -LiteralPath (Join-Path $data 'sentinel.txt'))){throw 'External data was deleted'}
$results+='UNINSTALL_EXIT=0'
$results+='PROGRAM_REMOVED=True'
$results+='EXTERNAL_DATA_PRESERVED=True'
$results | Set-Content -LiteralPath (Join-Path $qa 'installer-results.txt') -Encoding UTF8
$results
Write-Output ('INSTALLER_QA='+$qa)
