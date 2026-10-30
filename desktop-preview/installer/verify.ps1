$ErrorActionPreference='Stop'
$installerRoot=$PSScriptRoot
$projectRoot=Split-Path $installerRoot
$build=Get-Content -LiteralPath (Join-Path $installerRoot 'build-qa-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if((Get-FileHash -LiteralPath $build.installer).Hash-ne $build.sha256){throw 'Installer hash changed since build.'}
$uninstallKey='Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9B812031-C2DF-49B2-BBCC-BAB37C63C42B}_is1'
if(Test-Path -LiteralPath $uninstallKey){throw 'Preview already registered; test will not modify an existing install.'}
$runId=[guid]::NewGuid().ToString('N')
$qa=Join-Path $projectRoot ('evidence/installer-'+$runId)
$program=Join-Path $qa 'program with spaces'
$data=Join-Path $qa 'user-data'
$group='AsterHub Preview QA '+$runId
$menu=Join-Path ([Environment]::GetFolderPath('Programs')) ($group+'\AsterHub Preview.lnk')
if(Test-Path -LiteralPath $menu){throw 'Existing preview shortcut; test will not overwrite it.'}
New-Item -ItemType Directory -Path $data -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $data 'keep-settings.txt'),'owned test sentinel; preserve data on uninstall')
$results=[Collections.Generic.List[string]]::new()
function ReadSharedLog([string]$path){
 $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 try{$reader=[IO.StreamReader]::new($stream);try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}finally{$stream.Dispose()}
}
function Install([string]$log){
 $installArgs=@('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="'+$program+'"'),('/GROUP="'+$group+'"'),('/LOG="'+(Join-Path $qa $log)+'"'))
 $p=Start-Process -FilePath $build.installer -ArgumentList $installArgs -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode-ne 0){throw ('Installer exit '+$p.ExitCode)}
}
function CheckPayload {
 $manifest=Get-Content -LiteralPath (Join-Path $program 'payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 if($manifest.version-ne '0.3.0-preview.8'){throw 'Wrong installed version'}
 foreach($file in $manifest.files){$installed=Get-Item -LiteralPath (Join-Path $program $file.file);if($installed.Length-ne $file.bytes-or(Get-FileHash -LiteralPath $installed.FullName).Hash-ne $file.sha256){throw ('Installed payload differs: '+$file.file)}}
 foreach($folder in @('app/modules','runtime/managed','runtime/win-x64','resources/workbench','licenses','docs')){if(!(Test-Path -LiteralPath (Join-Path $program $folder))){throw ('Missing module folder '+$folder)}}
 return $manifest.files.Count
}
function RunInstalled([string]$mode){
 $smoke=Join-Path $qa ('run-'+$mode)
 $app=Start-Process -FilePath (Join-Path $program 'AsterHub.Preview.exe') -ArgumentList @('--smoke',('"'+$smoke+'"'),'--data-root',('"'+$data+'"'),'--debug-port','19879') -WindowStyle Hidden -PassThru
 & node (Join-Path $installerRoot 'verify-installed.mjs') $qa $mode
 if($LASTEXITCODE-ne 0){throw ('Installed UI checks failed: '+$mode+'; owned process '+$app.Id)}
 if(!$app.WaitForExit(45000)){throw ('Installed preview did not exit: '+$app.Id)}
}
function DataProof {
 $files=@((Get-Item -LiteralPath (Join-Path $data 'devices.json')),(Get-Item -LiteralPath (Join-Path $data 'keep-settings.txt')))+@(Get-ChildItem -LiteralPath (Join-Path $data 'History') -Recurse -File)
 return @($files|ForEach-Object {@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
}
function CheckData($proof){foreach($file in $proof){if(!(Test-Path -LiteralPath $file.path)-or(Get-FileHash -LiteralPath $file.path).Hash-ne $file.sha256){throw ('User data modified or removed: '+$file.path)}}}
Install 'install.log'
$count=CheckPayload
$registration=Get-ItemProperty -LiteralPath $uninstallKey
if($registration.DisplayVersion-ne $build.version-or[IO.Path]::GetFullPath($registration.InstallLocation).TrimEnd('\')-ne $program.TrimEnd('\')){throw 'Wrong installed registration'}
if(!(Test-Path -LiteralPath $menu)){throw 'Start-menu shortcut missing'}
$shell=New-Object -ComObject WScript.Shell
$link=$shell.CreateShortcut($menu)
if($link.TargetPath-ne(Join-Path $program 'AsterHub.Preview.exe')){throw 'Start-menu shortcut target incorrect'}
$results.Add('INSTALL_PAYLOAD_'+$count+'_HASHES_REGISTRATION_AND_SHORTCUT=PASS')
Write-Output $results[$results.Count-1]
RunInstalled 'first'
$results.Add('ACTUAL_INSTALLED_STARTUP_AND_HISTORY=PASS')
$before=DataProof
[IO.File]::WriteAllText((Join-Path $program 'user-note.keep'),'user-added unrecognized file must survive')
Install 'reinstall.log'
$null=CheckPayload
CheckData $before
if(!(Test-Path -LiteralPath (Join-Path $program 'user-note.keep'))){throw 'Reinstall removed user-added file'}
$results.Add('REINSTALL_PRESERVES_CONFIGURATION_HISTORY_AND_USER_FILES=PASS')
Write-Output $results[$results.Count-1]
RunInstalled 'restart'
$results.Add('REINSTALLED_NATIVE_STARTUP_READS_PREVIOUS_SNAPSHOT=PASS')
# Exercise the exact parent-wait/install/restart protocol in the independent QA registration.
$signal=Join-Path $qa 'allow-parent-exit.txt'
$parentScript=Join-Path $qa 'owned-update-parent.ps1'
[IO.File]::WriteAllText($parentScript,("while(-not(Test-Path -LiteralPath '{0}')){{Start-Sleep -Milliseconds 100}}" -f $signal.Replace("'","''")))
$parent=Start-Process -FilePath 'powershell.exe' -ArgumentList @('-NoProfile','-File',('"'+$parentScript+'"')) -WindowStyle Hidden -PassThru
$updateLog=Join-Path $qa 'update.log'
$restartProof=Join-Path $qa 'auto-update-restart'
$updateArgs=@('/VERYSILENT','/SP-','/SUPPRESSMSGBOXES','/NORESTART','/UPDATE=1',('/AQPID='+$parent.Id),('/DIR="'+$program+'"'),('/GROUP="'+$group+'"'),('/LOG="'+$updateLog+'"'),('/AQRESTARTPROOF="'+$restartProof+'"'),('/AQRESTARTDATA="'+$data+'"'))
$update=Start-Process -FilePath $build.installer -ArgumentList $updateArgs -WindowStyle Hidden -PassThru
try {
 $waiting=$false
 for($attempt=0;$attempt-lt 100;$attempt++){if((Test-Path -LiteralPath $updateLog)-and((ReadSharedLog $updateLog).Contains('ASTER_UPDATE_WAITING_FOR_PARENT'))){$waiting=$true;break};if($update.HasExited){throw 'Update exited before waiting for parent'};Start-Sleep -Milliseconds 100}
 if(!$waiting){throw 'Update did not record parent wait'}
 if($parent.HasExited){throw 'Owned update parent exited prematurely'}
 [IO.File]::WriteAllText($signal,'exit owned fixture')
 if(!$parent.WaitForExit(10000)){throw 'Owned update parent did not exit'}
 if(!$update.WaitForExit(60000)-or$update.ExitCode-ne 0){throw 'Automatic update installation failed'}
 & node (Join-Path $installerRoot 'verify-installed.mjs') $qa 'auto-update'
 if($LASTEXITCODE-ne 0){throw 'Automatic update did not restart a working native host'}
 foreach($ownedApp in @(Get-Process -Name 'AsterHub.Preview' -ErrorAction SilentlyContinue|Where-Object {$_.Path-eq(Join-Path $program 'AsterHub.Preview.exe')})){if(!$ownedApp.WaitForExit(45000)){throw 'Automatically restarted QA host did not flush and exit'}}
 $null=CheckPayload
 $results.Add('UPDATE_WAITS_FOR_PARENT_INSTALLS_AND_AUTOMATICALLY_RESTARTS_NATIVE_APP=PASS')
 Write-Output $results[$results.Count-1]
} finally {if(!(Test-Path -LiteralPath $signal)){[IO.File]::WriteAllText($signal,'cleanup owned parent')}}
$beforeUninstall=DataProof
$uninstaller=Join-Path $program 'unins000.exe'
$u=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="'+(Join-Path $qa 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if($u.ExitCode-ne 0){throw ('Uninstaller exit '+$u.ExitCode)}
CheckData $beforeUninstall
foreach($file in @('AsterHub.Preview.exe','app/modules/AQServer.Core.dll','runtime/managed/Microsoft.Web.WebView2.Core.dll','resources/workbench/index.html')){if(Test-Path -LiteralPath (Join-Path $program $file)){throw ('Owned program file remains after uninstall: '+$file)}}
if((Test-Path -LiteralPath $uninstallKey)-or(Test-Path -LiteralPath $menu)){throw 'Uninstall entry or start-menu shortcut remains'}
if(!(Test-Path -LiteralPath (Join-Path $program 'user-note.keep'))){throw 'Uninstall removed unknown user file'}
$results.Add('UNINSTALL_REMOVES_PROGRAM_AND_SHORTCUT_PRESERVES_DATA_AND_USER_FILES=PASS')
Write-Output $results[$results.Count-1]
@{version=$build.version;installer=$build.installer;sha256=$build.sha256;testRoot=$qa;installedPayloadFiles=$count;results=$results.ToArray();runtimeDependency='existing WebView2; missing-runtime bootstrap path not executed';scope='real Windows install, silent repair reinstall, native WPF startup and uninstall; independent directory and data'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $qa 'results.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $qa 'results.json') -Destination (Join-Path $installerRoot 'verification-result.json')
Write-Output ('INSTALLER_VERIFIED='+$qa)
