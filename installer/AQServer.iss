#ifndef AppVersion
#define AppVersion "0.2.1"
#endif
[Setup]
#ifdef QAInstall
AppId=AQServer-Isolated-Installer-QA
AppName=AQ Server Installer QA
AppVerName=AQ Server Installer QA {#AppVersion}
OutputBaseFilename=AQServer-Setup-QA-{#AppVersion}
AppMutex=Local\AQServerMonitor-Installer-QA
#else
AppId={{85F9C0AA-14B7-46C6-9DBE-97366452CB58}
AppName=AQ Server
AppVerName=AQ Server {#AppVersion}
OutputBaseFilename=AQServer-Setup-{#AppVersion}
AppMutex=Local\AQServerMonitor
#endif
AppVersion={#AppVersion}
AppPublisher=AQ Server
AppPublisherURL=http://8.140.49.4/aqserver/
AppUpdatesURL=http://8.140.49.4/aqserver/
DefaultDirName={localappdata}\Programs\AQ Server
DefaultGroupName=AQ Server
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
MinVersion=10.0.17763
SetupMutex=Local\AQServerSetup
OutputDir={#ReleaseDir}
SetupIconFile={#RepoRoot}\desktop\assets\AQServer.ico
UninstallDisplayIcon={app}\AQServer.exe
WizardStyle=modern
DisableProgramGroupPage=yes
AllowNoIcons=yes
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoDescription=AQ Server Installer
[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\installer\redist\MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
#ifndef QAInstall
[Icons]
Name: "{autoprograms}\AQ Server"; Filename: "{app}\AQServer.exe"
Name: "{autodesktop}\AQ Server"; Filename: "{app}\AQServer.exe"; Tasks: desktopicon
#endif
[InstallDelete]
; Remove only program-owned files from the previous flat layout. User data is outside {app}.
Type: files; Name: "{app}\AQServer.App.exe"; Check: LegacyFlatLayout
Type: files; Name: "{app}\Renci.SshNet.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\BouncyCastle.Cryptography.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\Microsoft.Web.WebView2.Core.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\Microsoft.Web.WebView2.Wpf.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\WebView2Loader.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Buffers.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Memory.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Runtime.CompilerServices.Unsafe.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Numerics.Vectors.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Formats.Asn1.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\System.Threading.Tasks.Extensions.dll"; Check: LegacyFlatLayout
Type: files; Name: "{app}\Microsoft.Bcl.AsyncInterfaces.dll"; Check: LegacyFlatLayout
Type: filesandordirs; Name: "{app}\TerminalAssets"; Check: LegacyFlatLayout
Type: filesandordirs; Name: "{app}\UiAssets"; Check: LegacyFlatLayout
[Run]
Filename: "{app}\AQServer.exe"; Description: "启动 AQ Server"; Flags: nowait postinstall skipifsilent; Check: IsRegularInstall
Filename: "{app}\AQServer.exe"; Parameters: "{code:UpdateRestartArguments}"; Flags: nowait; Check: IsUpdateInstall
[Code]
const RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
const EdgeKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
var LegacyLayoutAtStart: Boolean;
function AQOpenProcess(DesiredAccess: Cardinal; InheritHandle: Boolean; ProcessId: Cardinal): THandle;
  external 'OpenProcess@kernel32.dll stdcall';
function AQWaitForSingleObject(Handle: THandle; Milliseconds: Cardinal): Cardinal;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function AQCloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function IsUpdateInstall: Boolean;
begin
  Result := ExpandConstant('{param:UPDATE|0}') = '1';
end;
function IsRegularInstall: Boolean;
begin
  Result := not IsUpdateInstall;
end;
function UpdateRestartArguments(Param: String): String;
var Proof: String;
begin
  Result := '';
#ifdef QAInstall
  Proof := ExpandConstant('{param:AQRESTARTPROOF|}');
  if Proof <> '' then Result := '--update-restart-smoke "' + Proof + '"';
#endif
end;
function InitializeSetup: Boolean;
var ParentPid, Attempt: Integer; Handle: THandle; WaitResult: Cardinal;
begin
  Result := True;
  if not IsUpdateInstall then Exit;
  ParentPid := StrToIntDef(ExpandConstant('{param:AQPID|0}'), 0);
  if ParentPid <= 0 then begin
    { Previous clients supplied UPDATE=1 without AQPID. Wait for their own app mutex. }
    for Attempt := 1 to 300 do begin
#ifdef QAInstall
      if not CheckForMutexes('Local\AQServerMonitor-Installer-QA') then begin
#else
      if not CheckForMutexes('Local\AQServerMonitor') then begin
#endif
        Log('AQ_UPDATE_LEGACY_CALLER_EXITED');
        Exit;
      end;
      Sleep(200);
    end;
    MsgBox('当前程序尚未正常退出，已取消更新。请保存工作后重试。', mbError, MB_OK);
    Result := False;
    Exit;
  end;
  Handle := AQOpenProcess($00100000, False, ParentPid);
  if Handle <> 0 then begin
    Log('AQ_UPDATE_WAITING_FOR_PARENT');
    WaitResult := AQWaitForSingleObject(Handle, 60000);
    AQCloseHandle(Handle);
    if WaitResult <> 0 then begin
      MsgBox('当前程序尚未正常退出，已取消更新。请保存工作后重试。', mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;
  Log('AQ_UPDATE_PARENT_EXITED');
end;
function LegacyFlatLayout: Boolean;
begin
  Result := LegacyLayoutAtStart;
end;
function ValidRuntime(RootKey: Integer): Boolean;
var Version: String;
begin
  Result := RegQueryStringValue(RootKey, EdgeKey, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0');
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
var ExitCode: Integer;
begin
  LegacyLayoutAtStart := FileExists(ExpandConstant('{app}\AQServer.exe')) and DirExists(ExpandConstant('{app}\TerminalAssets'));
  Result := '';
#ifndef QAInstall
  if not (ValidRuntime(HKCU32) or ValidRuntime(HKCU64) or ValidRuntime(HKLM32) or ValidRuntime(HKLM64)) then begin
    ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
    if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Result := '无法启动 Microsoft WebView2 安装程序，请稍后重试。'
    else if (ExitCode <> 0) and not (ValidRuntime(HKCU32) or ValidRuntime(HKCU64) or ValidRuntime(HKLM32) or ValidRuntime(HKLM64)) then
      Result := '终端需要 Microsoft WebView2。请保持网络连接并重试，或先安装 WebView2 Evergreen Runtime。';
  end;
#endif
end;
procedure CurStepChanged(CurStep: TSetupStep);
var Command: String;
begin
#ifndef QAInstall
  if (CurStep = ssPostInstall) and RegQueryStringValue(HKCU, RunKey, 'AQServer', Command) then
    RegWriteStringValue(HKCU, RunKey, 'AQServer', '"' + ExpandConstant('{app}\AQServer.exe') + '" --tray');
#endif
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
#ifndef QAInstall
  if (CurUninstallStep = usPostUninstall) and RegQueryStringValue(HKCU, RunKey, 'AQServer', Command) then
    if Pos(Lowercase(ExpandConstant('{app}\AQServer.exe')), Lowercase(Command)) > 0 then
      RegDeleteValue(HKCU, RunKey, 'AQServer');
#endif
end;
