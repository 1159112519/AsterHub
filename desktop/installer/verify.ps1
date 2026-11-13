param([string]$EvidenceRoot,[switch]$SkipNativeChecks)
$ErrorActionPreference='Stop'
$installerRoot=$PSScriptRoot
$projectRoot=Split-Path $installerRoot
$build=Get-Content -LiteralPath (Join-Path $installerRoot 'build-qa-result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(!$build.qa){throw 'Verification requires the independent QA installer; refusing to install a production package.'}
if((Get-FileHash -LiteralPath $build.installer).Hash-ne $build.sha256){throw 'Installer hash changed since build.'}
$uninstallKey='Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\{9B812031-C2DF-49B2-BBCC-BAB37C63C42B}_is1'
if(Test-Path -LiteralPath $uninstallKey){throw 'QA registration already exists; refusing to change an existing install.'}
$runId=[guid]::NewGuid().ToString('N')
$qa=if($EvidenceRoot){Join-Path ([IO.Path]::GetFullPath($EvidenceRoot)) ('run-'+$runId)}else{Join-Path $projectRoot ('evidence/installer-'+$runId)}
$program=Join-Path $qa 'program with spaces'
$data=Join-Path $qa 'user-data'
$group='AsterHub QA '+$runId
$fixture=Join-Path $qa 'shell-fixture'
$menu=Join-Path $fixture 'userprograms\AsterHub\AsterHub.lnk'
if(Test-Path -LiteralPath $menu){throw 'Existing fixture shortcut; refusing to overwrite it.'}
New-Item -ItemType Directory -Path $data -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $data 'keep-settings.txt'),'owned test sentinel; preserve data on uninstall')
$results=[Collections.Generic.List[string]]::new()
function ReadSharedLog([string]$path){
 $stream=[IO.File]::Open($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 try{$reader=[IO.StreamReader]::new($stream);try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}finally{$stream.Dispose()}
}
function Install([string]$log,[bool]$UseRegisteredDirectory=$false){
 $installArgs=@('/VERYSILENT','/SP-','/TASKS=desktopicon','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS',('/GROUP="'+$group+'"'),('/LOG="'+(Join-Path $qa $log)+'"'),('/AQICONDATA="'+$data+'"'))
 if(!$UseRegisteredDirectory){$installArgs+=('/DIR="'+$program+'"')}
 $p=Start-Process -FilePath $build.installer -ArgumentList $installArgs -WindowStyle Hidden -PassThru -Wait
 if($p.ExitCode-ne 0){throw ('Installer exit '+$p.ExitCode)}
}
function CheckPayload {
 $manifest=Get-Content -LiteralPath (Join-Path $program 'payload-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
 if($manifest.version-ne $build.version){throw 'Wrong installed version'}
 foreach($file in $manifest.files){$installed=Get-Item -LiteralPath (Join-Path $program $file.file);if($installed.Length-ne $file.bytes-or(Get-FileHash -LiteralPath $installed.FullName).Hash-ne $file.sha256){throw ('Installed payload differs: '+$file.file)}}
 foreach($folder in @('app/modules','runtime/managed','runtime/win-x64','resources/workbench','licenses','docs')){if(!(Test-Path -LiteralPath (Join-Path $program $folder))){throw ('Missing module folder '+$folder)}}
 return $manifest.files.Count
}
function RunInstalled([string]$mode){
 $smoke=Join-Path $qa ('run-'+$mode)
 $app=Start-Process -FilePath (Join-Path $program 'AsterHub.exe') -ArgumentList @('--smoke',('"'+$smoke+'"'),'--data-root',('"'+$data+'"'),'--debug-port','19879') -WindowStyle Hidden -PassThru
 & node (Join-Path $installerRoot 'verify-installed.mjs') $qa $mode
 if($LASTEXITCODE-ne 0){throw ('Installed UI checks failed: '+$mode+'; owned process '+$app.Id)}
 if(!$app.WaitForExit(45000)){throw ('Installed application did not exit: '+$app.Id)}
}
function DataProof {
 $files=@(Get-Item -LiteralPath (Join-Path $data 'keep-settings.txt'));if(Test-Path -LiteralPath (Join-Path $data 'devices.json')){$files+=Get-Item -LiteralPath (Join-Path $data 'devices.json')};if(Test-Path -LiteralPath (Join-Path $data 'History')){$files+=@(Get-ChildItem -LiteralPath (Join-Path $data 'History') -Recurse -File)}
 return @($files|ForEach-Object {@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
}
function CheckData($proof){foreach($file in $proof){if(!(Test-Path -LiteralPath $file.path)-or(Get-FileHash -LiteralPath $file.path).Hash-ne $file.sha256){throw ('User data modified or removed: '+$file.path)}}}
$shell=New-Object -ComObject WScript.Shell
function MakeFixtureShortcut([string]$relative,[string]$target){
 $linkPath=Join-Path $fixture $relative
 New-Item -ItemType Directory -Force -Path (Split-Path $linkPath) | Out-Null
 $link=$shell.CreateShortcut($linkPath);$link.TargetPath=$target;$link.WorkingDirectory=$qa;$link.Save()
}
$legacy=@('userdesktop\AsterHub Preview.lnk','commondesktop\AsterHub Preview.lnk','userprograms\AsterHub Preview\AsterHub Preview.lnk','commonprograms\AsterHub Preview\AsterHub Preview.lnk','userprograms\AsterHub Preview.lnk','commonprograms\AsterHub Preview.lnk')
foreach($relative in $legacy){MakeFixtureShortcut $relative (Join-Path $program 'AsterHub.exe')}
$foreignTarget=Join-Path $qa 'another-install.exe'
[IO.File]::WriteAllText($foreignTarget,'independent app sentinel')
$foreignFile=Join-Path $fixture 'userprograms\AsterHub Preview\personal.keep'
[IO.File]::WriteAllText($foreignFile,'unrelated user file in legacy folder')
Install 'install.log'
$count=CheckPayload
$registration=Get-ItemProperty -LiteralPath $uninstallKey
if($registration.DisplayVersion-ne $build.version-or[IO.Path]::GetFullPath($registration.InstallLocation).TrimEnd('\')-ne $program.TrimEnd('\')){throw 'Wrong installed registration'}
if(!(Test-Path -LiteralPath $menu)){throw 'Start-menu shortcut missing'}
$shell=New-Object -ComObject WScript.Shell
$link=$shell.CreateShortcut($menu)
if($link.TargetPath-ne(Join-Path $program 'AsterHub.exe')){throw 'Start-menu shortcut target incorrect'}
foreach($relative in $legacy){if(Test-Path -LiteralPath (Join-Path $fixture $relative)){throw ('Owned legacy shortcut remains: '+$relative)}}
foreach($relative in @('userdesktop\AsterHub.lnk','commondesktop\AsterHub.lnk','userprograms\AsterHub\AsterHub.lnk','commonprograms\AsterHub\AsterHub.lnk')){if(!(Test-Path -LiteralPath (Join-Path $fixture $relative))){throw ('Formal shortcut missing: '+$relative)}}
if(!(Test-Path -LiteralPath $foreignFile)){throw 'Legacy-folder user file was deleted'}
if($registration.DisplayName-ne ('AsterHub '+$build.version)){throw 'Uninstall display name is not formal AsterHub'}
$results.Add('SIX_OWNED_LEGACY_LINKS_MIGRATED_TO_FOUR_FORMAL_LINKS_USER_FILES_PRESERVED=PASS')
$results.Add('INSTALL_PAYLOAD_'+$count+'_HASHES_REGISTRATION_AND_SHORTCUT=PASS')
Write-Output $results[$results.Count-1]
if(!$SkipNativeChecks){RunInstalled 'first';$results.Add('ACTUAL_INSTALLED_STARTUP_AND_HISTORY=PASS')}
if((ReadSharedLog (Join-Path $qa 'install.log')) -match 'ASTER_SETUP_ICON_FAMILY='){throw 'Setup used a custom icon before any preference was saved'}
$prefFile=Join-Path $data 'preferences.json'
$pref=if(Test-Path -LiteralPath $prefFile){Get-Content -LiteralPath $prefFile -Raw | ConvertFrom-Json -AsHashtable}else{@{}}
$pref['iconThemeId']='06'; $pref | ConvertTo-Json -Depth 20 -Compress | Set-Content -LiteralPath $prefFile -Encoding utf8
$before=DataProof
[IO.File]::WriteAllText((Join-Path $program 'user-note.keep'),'user-added unrecognized file must survive')
# A same-named link belonging to another installation must remain unchanged.
MakeFixtureShortcut 'commonprograms\AsterHub Preview.lnk' $foreignTarget
MakeFixtureShortcut 'commonprograms\AsterHub\AsterHub.lnk' $foreignTarget
MakeFixtureShortcut 'commonprograms\AsterHub Preview\AsterHub Preview.lnk' (Join-Path $program 'AsterHub.exe')
Install 'reinstall.log'
if((ReadSharedLog (Join-Path $qa 'reinstall.log')) -notmatch 'ASTER_SETUP_ICON_FAMILY=06'){throw 'Upgrade wizard did not load both sizes of saved icon 06'}
$results.Add('SETUP_DEFAULT_FIRST_AND_SAVED_ICON_ON_UPGRADE=PASS')
$null=CheckPayload
CheckData $before
if(!(Test-Path -LiteralPath (Join-Path $program 'user-note.keep'))){throw 'Reinstall removed user-added file'}
$results.Add('REINSTALL_PRESERVES_CONFIGURATION_HISTORY_AND_USER_FILES=PASS')
Write-Output $results[$results.Count-1]
if($shell.CreateShortcut((Join-Path $fixture 'commonprograms\AsterHub Preview.lnk')).TargetPath-ne $foreignTarget){throw 'Foreign legacy shortcut changed'}
if($shell.CreateShortcut((Join-Path $fixture 'commonprograms\AsterHub\AsterHub.lnk')).TargetPath-ne $foreignTarget){throw 'Conflicting foreign destination changed'}
if(!(Test-Path -LiteralPath (Join-Path $fixture 'commonprograms\AsterHub Preview\AsterHub Preview.lnk'))){throw 'Conflicting owned shortcut was removed without a usable replacement'}
$results.Add('IDEMPOTENT_REINSTALL_PRESERVES_FOREIGN_LINKS_AND_DESTINATION_CONFLICTS=PASS')
Install 'upgrade-directory-reuse.log' $true
$null=CheckPayload
if([IO.Path]::GetFullPath((Get-ItemProperty -LiteralPath $uninstallKey).InstallLocation).TrimEnd('\')-ne $program.TrimEnd('\')){throw 'Upgrade did not reuse the registered installation directory'}
$results.Add('UPGRADE_WITHOUT_DIR_ARGUMENT_REUSES_REGISTERED_INSTALL_DIRECTORY=PASS')
if(!$SkipNativeChecks){RunInstalled 'restart';$results.Add('REINSTALLED_NATIVE_STARTUP_READS_PREVIOUS_SNAPSHOT=PASS')}
if(!$SkipNativeChecks){
# Exercise the exact parent-wait/install/restart protocol in the independent QA registration.
$signal=Join-Path $qa 'allow-parent-exit.txt'
$parentSource=Join-Path $qa 'OwnedUpdateParent.cs'
$parentExe=Join-Path $qa 'OwnedUpdateParent.exe'
[IO.File]::WriteAllText($parentSource,'using System;using System.IO;using System.Threading;class OwnedUpdateParent{static void Main(string[] args){while(!File.Exists(args[0]))Thread.Sleep(100);}}')
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe ('/out:'+$parentExe) $parentSource
if($LASTEXITCODE-ne 0){throw 'Owned parent fixture compilation failed'}
$parent=Start-Process -FilePath $parentExe -ArgumentList ('"'+$signal+'"') -WindowStyle Hidden -PassThru
$updateLog=Join-Path $qa 'update.log'
$restartProof=Join-Path $qa 'auto-update-restart'
$updateArgs=@('/VERYSILENT','/SP-','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS','/UPDATE=1',('/AQPID='+$parent.Id),('/DIR="'+$program+'"'),('/GROUP="'+$group+'"'),('/LOG="'+$updateLog+'"'),('/AQRESTARTPROOF="'+$restartProof+'"'),('/AQRESTARTDATA="'+$data+'"'))
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
 foreach($ownedApp in @(Get-Process -Name 'AsterHub' -ErrorAction SilentlyContinue|Where-Object {$_.Path-eq(Join-Path $program 'AsterHub.exe')})){if(!$ownedApp.WaitForExit(45000)){throw 'Automatically restarted QA host did not flush and exit'}}
 $null=CheckPayload
 $results.Add('UPDATE_WAITS_FOR_PARENT_INSTALLS_AND_AUTOMATICALLY_RESTARTS_NATIVE_APP=PASS')
 Write-Output $results[$results.Count-1]
} finally {if(!(Test-Path -LiteralPath $signal)){[IO.File]::WriteAllText($signal,'cleanup owned parent')}}
}
$beforeUninstall=DataProof
$uninstaller=Join-Path $program 'unins000.exe'
$u=Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/NOCLOSEAPPLICATIONS',('/LOG="'+(Join-Path $qa 'uninstall.log')+'"')) -WindowStyle Hidden -PassThru -Wait
if($u.ExitCode-ne 0){throw ('Uninstaller exit '+$u.ExitCode)}
CheckData $beforeUninstall
foreach($file in @('AsterHub.exe','app/modules/AsterHub.Core.dll','runtime/managed/Microsoft.Web.WebView2.Core.dll','resources/workbench/index.html')){if(Test-Path -LiteralPath (Join-Path $program $file)){throw ('Owned program file remains after uninstall: '+$file)}}
if((Test-Path -LiteralPath $uninstallKey)-or(Test-Path -LiteralPath $menu)){throw 'Uninstall entry or start-menu shortcut remains'}
if(!(Test-Path -LiteralPath (Join-Path $program 'user-note.keep'))){throw 'Uninstall removed unknown user file'}
foreach($relative in @('commondesktop\AsterHub.lnk','userdesktop\AsterHub.lnk','commonprograms\AsterHub Preview\AsterHub Preview.lnk')){if(Test-Path -LiteralPath (Join-Path $fixture $relative)){throw ('Owned migrated link remains after uninstall: '+$relative)}}
if(!(Test-Path -LiteralPath $foreignFile)){throw 'Uninstall deleted a legacy-folder user file'}
if($shell.CreateShortcut((Join-Path $fixture 'commonprograms\AsterHub\AsterHub.lnk')).TargetPath-ne $foreignTarget){throw 'Uninstall modified foreign destination shortcut'}
if($shell.CreateShortcut((Join-Path $fixture 'commonprograms\AsterHub Preview.lnk')).TargetPath-ne $foreignTarget){throw 'Uninstall modified foreign legacy shortcut'}
$results.Add('UNINSTALL_REMOVES_PROGRAM_AND_OWNED_SHORTCUTS_PRESERVES_DATA_AND_FOREIGN_FILES=PASS')
Write-Output $results[$results.Count-1]
@{version=$build.version;installer=$build.installer;sha256=$build.sha256;testRoot=$qa;installedPayloadFiles=$count;results=$results.ToArray();runtimeDependency='existing WebView2; missing-runtime bootstrap path not executed';nativeChecks=(-not $SkipNativeChecks);scope='Windows install, repair, shortcut migration and uninstall; all desktop/start-menu scopes redirected into isolated fixture; native startup/update included unless skipped'} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $qa 'results.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $qa 'results.json') -Destination (Join-Path $installerRoot 'verification-result.json')
Write-Output ('INSTALLER_VERIFIED='+$qa)
