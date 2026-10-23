#ifndef AppVersion
#define AppVersion "0.1.2"
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
Filename: "{app}\AQServer.exe"; Description: "启动 AQ Server"; Flags: nowait postinstall skipifsilent
[Code]
const RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
const EdgeKey = 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
var LegacyLayoutAtStart: Boolean;
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
