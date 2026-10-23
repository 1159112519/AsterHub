param([Parameter(Mandatory=$true)][string]$InstallRoot,[Parameter(Mandatory=$true)][string]$SettingsFile)
$ErrorActionPreference='Stop'
$before=(Get-FileHash -LiteralPath $SettingsFile).Hash
$core=[Reflection.Assembly]::LoadFrom((Join-Path $InstallRoot 'app/modules/AQServer.Core.dll'))
$storage=[Reflection.Assembly]::LoadFrom((Join-Path $InstallRoot 'app/modules/AQServer.Storage.dll'))
$type=$storage.GetType('Storage',$true)
$type.GetField('PathName').SetValue($null,$SettingsFile)
$value=$type.GetMethod('Load').Invoke($null,@())
if($value.Interval-ne 7-or$value.Servers.Count-ne 2-or$value.Servers[0].Name-ne'aq-server'-or$value.Servers[0].Port-ne 22){throw 'Legacy settings values changed'}
if((Get-FileHash -LiteralPath $SettingsFile).Hash-ne$before){throw 'Loading legacy settings modified the file'}
Write-Output 'LEGACY_SETTINGS_COMPATIBLE=True'
