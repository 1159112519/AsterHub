#ifndef PayloadDir
#error PayloadDir is required
#endif
#ifndef ProjectRoot
#error ProjectRoot is required
#endif
#ifndef Bootstrapper
#error Bootstrapper is required
#endif
#define PreviewVersion "0.3.0-preview.7"
[Setup]
#ifdef QAInstall
AppId={{9B812031-C2DF-49B2-BBCC-BAB37C63C42B}
#else
AppId={{2346218B-4E82-4C20-9A7C-9485C0A72696}
#endif
AppName=AsterHub Preview
AppVerName=AsterHub 0.3.0-preview.7（预览版）
AppVersion={#PreviewVersion}
AppPublisher=AsterHub
DefaultDirName={localappdata}\Programs\AsterHub Preview
DefaultGroupName={param:GROUP|AsterHub Preview}
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupMutex=Local\AsterHubPreviewSetup
OutputDir={#ProjectRoot}
#ifdef QAInstall
OutputBaseFilename=AsterHub-Setup-{#PreviewVersion}-QA
#else
OutputBaseFilename=AsterHub-Setup-{#PreviewVersion}
#endif
SetupIconFile={#ProjectRoot}\host\AsterHub.ico
UninstallDisplayIcon={app}\AsterHub.Preview.exe
UninstallDisplayName=AsterHub {#PreviewVersion}（预览版）
WizardStyle=modern
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
VersionInfoVersion=0.3.0.7
VersionInfoProductVersion=0.3.0.7
VersionInfoTextVersion={#PreviewVersion}
VersionInfoDescription=AsterHub Windows 预览版安装程序
InfoBeforeFile={#ProjectRoot}\installer\notice.txt
ChangesEnvironment=no
UninstallLogMode=append
[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Bootstrapper}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
[Icons]
Name: "{group}\AsterHub Preview"; Filename: "{app}\AsterHub.Preview.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\AsterHub Preview"; Filename: "{app}\AsterHub.Preview.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\AsterHub.Preview.exe"; Description: "启动 AsterHub"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent; Check: IsRegularInstall
Filename: "{app}\AsterHub.Preview.exe"; Parameters: "{code:UpdateRestartArguments}"; WorkingDir: "{app}"; Flags: nowait; Check: IsUpdateInstall
[Code]
function AQOpenProcess(DesiredAccess: Cardinal; InheritHandle: Boolean; ProcessId: Cardinal): THandle;
 external 'OpenProcess@kernel32.dll stdcall';
function AQWaitForSingleObject(Handle: THandle; Milliseconds: Cardinal): Cardinal;
 external 'WaitForSingleObject@kernel32.dll stdcall';
function AQCloseHandle(Handle: THandle): Boolean;
 external 'CloseHandle@kernel32.dll stdcall';
function AQGetLastError: Cardinal;
 external 'GetLastError@kernel32.dll stdcall';
function IsUpdateInstall: Boolean;
begin Result := ExpandConstant('{param:UPDATE|0}') = '1'; end;
function IsRegularInstall: Boolean;
begin Result := not IsUpdateInstall; end;
function UpdateRestartArguments(Param: String): String;
begin
 Result := '';
#ifdef QAInstall
 Result := '--smoke "' + ExpandConstant('{param:AQRESTARTPROOF|}') + '" --data-root "' + ExpandConstant('{param:AQRESTARTDATA|}') + '" --debug-port 19879';
#endif
end;
function InitializeSetup: Boolean;
var ParentPid: Integer; Handle: THandle; WaitResult: Cardinal;
begin
 Result := True;
 if not IsUpdateInstall then Exit;
 ParentPid := StrToIntDef(ExpandConstant('{param:AQPID|0}'), 0);
 if ParentPid <= 0 then begin Result := False; Exit; end;
 Handle := AQOpenProcess($00100000, False, ParentPid);
 if Handle <> 0 then begin
  Log('ASTER_UPDATE_WAITING_FOR_PARENT');
  WaitResult := AQWaitForSingleObject(Handle, 60000);
  AQCloseHandle(Handle);
  if WaitResult <> 0 then begin Result := False; Exit; end;
 end else if AQGetLastError <> 87 then begin Result := False; Exit; end;
 Log('ASTER_UPDATE_PARENT_EXITED');
end;

const EdgeKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
function ValidRuntime(RootKey: Integer): Boolean;
var Version: String;
begin
 Result := RegQueryStringValue(RootKey, EdgeKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;
function HasWebView: Boolean;
begin
 Result := ValidRuntime(HKCU32) or ValidRuntime(HKCU64) or ValidRuntime(HKLM32) or ValidRuntime(HKLM64);
end;
function HasFramework: Boolean;
var Release: Cardinal;
begin
 Result := (RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 461808)) or
           (RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 461808));
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
 Result := '';
 if not HasFramework then begin
   Result := 'AsterHub 需要 .NET Framework 4.7.2 或更新版本。请先通过 Windows 更新安装此组件，再重新运行安装程序。';
   Exit;
 end;
 if not HasWebView then begin
   ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
   if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
     Result := '无法启动 Microsoft WebView2 安装程序，请重试。'
   else if not HasWebView then
     Result := 'Microsoft WebView2 Runtime 尚未安装完成。请保持网络连接，安装完成后重试。';
 end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
 if CurUninstallStep = usUninstall then begin
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AsterHubPreview', Command) then
   if Command = '"' + ExpandConstant('{app}\AsterHub.Preview.exe') + '" --background' then
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AsterHubPreview');
 end;
end;
