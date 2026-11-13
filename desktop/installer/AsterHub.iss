#ifndef PayloadDir
#error PayloadDir is required
#endif
#ifndef ProjectRoot
#error ProjectRoot is required
#endif
#ifndef Bootstrapper
#error Bootstrapper is required
#endif
#if VER < EncodeVer(7, 0, 0)
#error AsterHub requires Inno Setup 7 or newer for the Windows 11 wizard.
#endif
#ifndef ReleaseVersion
#error ReleaseVersion is required
#endif
#ifndef InstallerOutputDir
#define InstallerOutputDir ProjectRoot
#endif
[Setup]
#ifdef QAInstall
AppId={{9B812031-C2DF-49B2-BBCC-BAB37C63C42B}
#else
AppId={{2346218B-4E82-4C20-9A7C-9485C0A72696}
#endif
AppName=AsterHub
AppVerName=AsterHub {#ReleaseVersion}
AppVersion={#ReleaseVersion}
AppPublisher=AsterHub
DefaultDirName={localappdata}\Programs\AsterHub
DefaultGroupName={param:GROUP|AsterHub}
PrivilegesRequired=lowest
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupMutex=Local\AsterHubPreviewSetup
OutputDir={#InstallerOutputDir}
#ifdef QAInstall
OutputBaseFilename=AsterHub-Setup-{#ReleaseVersion}-QA
#else
OutputBaseFilename=AsterHub-Setup-{#ReleaseVersion}
#endif
SetupIconFile={#ProjectRoot}\host\brand-icons\01\window.ico
UninstallDisplayIcon={app}\AsterHub.exe
UninstallDisplayName=AsterHub {#ReleaseVersion}
WizardStyle=modern dynamic windows11 hidebevels
WizardSizePercent=100,100
WizardImageFile=
WizardImageFileDynamicDark=
WizardSmallImageFile={#ProjectRoot}\installer\assets\wizard-mark.png
WizardSmallImageFileDynamicDark={#ProjectRoot}\installer\assets\wizard-mark.png
DisableWelcomePage=yes
ShowLanguageDialog=no
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=no
VersionInfoVersion={#ReleaseVersion}
VersionInfoProductVersion={#ReleaseVersion}
VersionInfoTextVersion={#ReleaseVersion}
VersionInfoDescription=AsterHub Windows 正式版安装程序
; Usage notes are installed in README.txt; the welcome stays focused on setup.
ChangesEnvironment=no
UninstallLogMode=append
[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
[LangOptions]
DialogFontName=Microsoft YaHei
DialogFontSize=10
[Messages]
chinesesimplified.SetupWindowTitle=AsterHub 安装
chinesesimplified.FinishedHeadingLabel=AsterHub 已准备就绪
chinesesimplified.FinishedLabel=安装已完成。
chinesesimplified.SelectDirLabel3=选择 AsterHub 的安装位置。升级时将沿用原安装目录。
chinesesimplified.ReadyLabel1=已准备好安装 AsterHub。
chinesesimplified.InstallingLabel=正在安装 AsterHub，请稍候…
english.SetupWindowTitle=AsterHub Setup
english.FinishedHeadingLabel=AsterHub is ready
english.FinishedLabel=Installation is complete.
english.SelectDirLabel3=Choose where to install AsterHub. Upgrades reuse your existing installation folder.
english.ReadyLabel1=Setup is ready to install AsterHub.
english.InstallingLabel=Installing AsterHub. Please wait…
[CustomMessages]
chinesesimplified.DesktopShortcut=创建桌面快捷方式
chinesesimplified.ShortcutGroup=快捷方式
chinesesimplified.LaunchApplication=启动 AsterHub
english.DesktopShortcut=Create a desktop shortcut
english.ShortcutGroup=Shortcuts
english.LaunchApplication=Open AsterHub
[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:ShortcutGroup}"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#Bootstrapper}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
[Icons]
Name: "{code:ProgramShortcutFolder}\AsterHub"; Filename: "{app}\AsterHub.exe"; WorkingDir: "{app}"
Name: "{code:DesktopShortcutFolder}\AsterHub"; Filename: "{app}\AsterHub.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\AsterHub.exe"; Description: "{cm:LaunchApplication}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent; Check: IsRegularInstall
Filename: "{app}\AsterHub.exe"; Parameters: "{code:UpdateRestartArguments}"; WorkingDir: "{app}"; Flags: nowait; Check: IsUpdateInstall
[Code]
const
#ifdef QAInstall
 InstallRegKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{9B812031-C2DF-49B2-BBCC-BAB37C63C42B}_is1';
#else
 InstallRegKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{2346218B-4E82-4C20-9A7C-9485C0A72696}_is1';
#endif
 RunRegKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
var LegacyDir, FormalDir: String;
    LegacyMoved, InstallCompleted: Boolean;
    SetupIconSmall, SetupIconLarge: THandle;

function AQFileAttributes(Name: String): Cardinal;
 external 'GetFileAttributesW@kernel32.dll stdcall';

function LegacyInstallAt(Dir: String): Boolean;
begin
 Result := FileExists(Dir + '\AsterHub.Preview.exe') and FileExists(Dir + '\unins000.exe') and
   ((AQFileAttributes(Dir) and $400) = 0);
end;

function OwnedExecutable(Target: String): Boolean;
var Folder, FileName: String;
begin
 Result := False;
 if Target = '' then Exit;
 Target := ExpandFileName(Target);
 Folder := ExtractFileDir(Target); FileName := ExtractFileName(Target);
 if CompareText(Folder, ExpandConstant('{app}')) = 0 then
  Result := (CompareText(FileName, 'AsterHub.exe') = 0) or (CompareText(FileName, 'AsterHub.Preview.exe') = 0)
 else if (LegacyDir <> '') and (CompareText(Folder, LegacyDir) = 0) then
  Result := (CompareText(FileName, 'AsterHub.exe') = 0) or (CompareText(FileName, 'AsterHub.Preview.exe') = 0);
end;
// QA builds redirect every shortcut scope into a sibling fixture directory.
// Production builds keep normal Windows locations and preserve the stable AppId.
function ShortcutFolder(Kind: String): String;
begin
#ifdef QAInstall
 Result := ExtractFileDir(ExpandConstant('{app}')) + '\shell-fixture\' + Kind;
#else
 Result := ExpandConstant('{' + Kind + '}');
#endif
end;
function ProgramShortcutFolder(Param: String): String;
begin
#ifdef QAInstall
 Result := ShortcutFolder('userprograms') + '\AsterHub';
#else
 Result := ExpandConstant('{group}');
#endif
end;
function DesktopShortcutFolder(Param: String): String;
begin
#ifdef QAInstall
 Result := ShortcutFolder('userdesktop');
#else
 Result := ExpandConstant('{autodesktop}');
#endif
end;
function OwnedShortcut(LinkPath: String): Boolean;
var Shell, Link: Variant; Target: String;
begin
 Result := False;
 if not FileExists(LinkPath) then Exit;
 try
  Shell := CreateOleObject('WScript.Shell');
  Link := Shell.CreateShortcut(LinkPath);
  Target := Link.TargetPath;
  Result := OwnedExecutable(Target);
 except
  Log('ASTER_SHORTCUT_INSPECT_FAILED: ' + LinkPath);
 end;
end;
procedure RemoveOwnedShortcut(LinkPath: String);
begin
 if not OwnedShortcut(LinkPath) then Exit;
 if DeleteFile(LinkPath) then begin
  Log('ASTER_SHORTCUT_REMOVED: ' + LinkPath);
  // Remove only a now-empty known program group; never recurse.
  if (CompareText(ExtractFileName(ExtractFileDir(LinkPath)), 'AsterHub') = 0) or
     (CompareText(ExtractFileName(ExtractFileDir(LinkPath)), 'AsterHub Preview') = 0) then
   RemoveDir(ExtractFileDir(LinkPath));
 end else Log('ASTER_SHORTCUT_REMOVE_FAILED: ' + LinkPath);
end;
procedure MigrateShortcut(OldPath, NewPath: String);
var Created: String;
begin
 if not OwnedShortcut(OldPath) then Exit;
 if FileExists(NewPath) and not OwnedShortcut(NewPath) then begin
  Log('ASTER_SHORTCUT_CONFLICT_PRESERVED: ' + NewPath); Exit;
 end;
 try
  if not ForceDirectories(ExtractFileDir(NewPath)) then Exit;
  Created := CreateShellLink(NewPath, 'AsterHub', ExpandConstant('{app}\AsterHub.exe'), '', ExpandConstant('{app}'), ExpandConstant('{app}\AsterHub.exe'), 0, SW_SHOWNORMAL);
  if (Created <> '') and OwnedShortcut(NewPath) then RemoveOwnedShortcut(OldPath);
 except
  // A per-user installer may not be able to write a protected common desktop.
  // Preserve that shortcut and log the failure rather than fail an update.
  Log('ASTER_SHORTCUT_MIGRATION_FAILED: ' + OldPath);
 end;
end;
procedure MigrateLegacyShortcuts;
var Base: String; I: Integer;
begin
 for I := 0 to 1 do begin
  if I = 0 then Base := ShortcutFolder('userdesktop') else Base := ShortcutFolder('commondesktop');
  MigrateShortcut(Base + '\AsterHub Preview.lnk', Base + '\AsterHub.lnk');
  if I = 0 then Base := ShortcutFolder('userprograms') else Base := ShortcutFolder('commonprograms');
  MigrateShortcut(Base + '\AsterHub Preview\AsterHub Preview.lnk', Base + '\AsterHub\AsterHub.lnk');
  MigrateShortcut(Base + '\AsterHub Preview.lnk', Base + '\AsterHub\AsterHub.lnk');
 end;
 MigrateShortcut(ProgramShortcutFolder('') + '\AsterHub Preview.lnk', ProgramShortcutFolder('') + '\AsterHub.lnk');
end;
procedure CleanupMigratedShortcuts;
var Base: String; I: Integer;
begin
 for I := 0 to 1 do begin
  if I = 0 then Base := ShortcutFolder('userdesktop') else Base := ShortcutFolder('commondesktop');
  RemoveOwnedShortcut(Base + '\AsterHub.lnk');
  RemoveOwnedShortcut(Base + '\AsterHub Preview.lnk');
  if I = 0 then Base := ShortcutFolder('userprograms') else Base := ShortcutFolder('commonprograms');
  RemoveOwnedShortcut(Base + '\AsterHub\AsterHub.lnk');
  RemoveOwnedShortcut(Base + '\AsterHub Preview\AsterHub Preview.lnk');
  RemoveOwnedShortcut(Base + '\AsterHub Preview.lnk');
 end;
end;
function LoadSetupIcon(Instance: THandle; Name: String; Kind, Width, Height, Flags: Cardinal): THandle;
 external 'LoadImageW@user32.dll stdcall';
function SetSetupIcon(Window: HWND; MessageId, IconType: Cardinal; Icon: THandle): THandle;
 external 'SendMessageW@user32.dll stdcall';
function SetupIconMetric(Index: Integer): Integer;
 external 'GetSystemMetrics@user32.dll stdcall';
function FreeSetupIcon(Icon: THandle): Boolean;
 external 'DestroyIcon@user32.dll stdcall';

function SetupIconFamily(Preferences: String): String;
var Bytes: AnsiString; Text, Token: String; I, Start, J, Depth: Integer; Size: Int64;
begin
 Result := '01';
 if not FileSize64(Preferences, Size) or (Size > 1048576) or not LoadStringFromFile(Preferences, Bytes) then Exit;
 Text := String(Bytes); I := 1; Depth := 0;
 while I <= Length(Text) do begin
  case Text[I] of
   '{','[': Depth := Depth + 1;
   '}',']': Depth := Depth - 1;
   '"': begin
    I := I + 1; Start := I;
    while (I <= Length(Text)) and (Text[I] <> '"') do begin
     if Text[I] = '\' then I := I + 1;
     I := I + 1;
    end;
    if (Depth = 1) and (Copy(Text, Start, I - Start) = 'iconThemeId') then begin
     J := I + 1;
     while (J <= Length(Text)) and (Text[J] <= ' ') do J := J + 1;
     if (J <= Length(Text)) and (Text[J] = ':') then begin
      J := J + 1;
      while (J <= Length(Text)) and (Text[J] <= ' ') do J := J + 1;
      if (J <= Length(Text)) and (Text[J] = '"') then begin
       J := J + 1; Start := J;
       while (J <= Length(Text)) and (Text[J] <> '"') do J := J + 1;
       Token := Copy(Text, Start, J - Start);
       if (Length(Token) = 2) and (Token[1] = '0') and (Token[2] >= '1') and (Token[2] <= '9') then Result := Token
       else if (Token = '10') or (Token = '11') or (Token = '12') then Result := Token
       else if Token = 'pearl' then Result := '02'
       else if Token = 'zones' then Result := '03'
       else if Token = 'ember' then Result := '04'
       else if Token = 'mint' then Result := '05'
       else if Token = 'glacier' then Result := '06';
      end;
      Exit;
     end;
    end;
   end;
  end;
  I := I + 1;
 end;
end;

procedure ApplyExistingSetupIcon(RegisteredDir: String);
var DataDir, Family, FileName: String;
begin
 if RegisteredDir = '' then Exit;
 if not FileExists(RegisteredDir + '\AsterHub.exe') and not LegacyInstallAt(RegisteredDir) then Exit;
#ifdef QAInstall
 DataDir := ExpandConstant('{param:AQICONDATA|}');
 if DataDir = '' then Exit;
#else
 DataDir := ExpandConstant('{localappdata}\AsterHub\Preview1');
#endif
 Family := SetupIconFamily(DataDir + '\preferences.json');
 if Family = '01' then Exit;
 FileName := RegisteredDir + '\resources\brand-icons\' + Family + '\window.ico';
 if not FileExists(FileName) then FileName := RegisteredDir + '\resources\brand-icons\' + Family + '\app.ico';
 if not FileExists(FileName) then Exit;
 SetupIconSmall := LoadSetupIcon(0, FileName, 1, SetupIconMetric(49), SetupIconMetric(50), $10);
 SetupIconLarge := LoadSetupIcon(0, FileName, 1, SetupIconMetric(11), SetupIconMetric(12), $10);
 if SetupIconSmall <> 0 then SetSetupIcon(WizardForm.Handle, $80, 0, SetupIconSmall);
 if SetupIconLarge <> 0 then SetSetupIcon(WizardForm.Handle, $80, 1, SetupIconLarge);
 if (SetupIconSmall <> 0) and (SetupIconLarge <> 0) then Log('ASTER_SETUP_ICON_FAMILY=' + Family);
end;

procedure InitializeWizard;
var RegisteredDir, ChosenDir: String;
begin
 ChosenDir := RemoveBackslashUnlessRoot(ExpandFileName(WizardDirValue));
 if RegQueryStringValue(HKCU, InstallRegKey, 'InstallLocation', RegisteredDir) and (RegisteredDir <> '') then begin
  RegisteredDir := RemoveBackslashUnlessRoot(ExpandFileName(RegisteredDir));
  ApplyExistingSetupIcon(RegisteredDir);
  if (CompareText(ChosenDir, RegisteredDir) = 0) and LegacyInstallAt(RegisteredDir) then begin
   LegacyDir := RegisteredDir;
   if CompareText(ExtractFileName(LegacyDir), 'AsterHub Preview') = 0 then begin
    FormalDir := ExtractFileDir(LegacyDir) + '\AsterHub';
    WizardForm.DirEdit.Text := FormalDir;
   end;
  end;
 end;
end;
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
 if Result <> '' then Exit;
 if (FormalDir <> '') and (CompareText(RemoveBackslashUnlessRoot(WizardDirValue), FormalDir) = 0) and not LegacyMoved then begin
  if DirExists(FormalDir) or FileExists(FormalDir) then begin
   Result := '正式安装目录已存在，未移动原有文件。请选择其他安装目录，或先整理同名目录。'; Exit;
  end;
  if not LegacyInstallAt(LegacyDir) or not RenameFile(LegacyDir, FormalDir) then begin
   Result := '无法迁移原安装目录。请关闭正在使用该目录的 AsterHub 窗口后重试。'; Exit;
  end;
  LegacyMoved := True;
  Log('ASTER_FORMAL_DIRECTORY_MIGRATED');
 end;
end;

procedure DeinitializeSetup;
begin
 if SetupIconSmall <> 0 then FreeSetupIcon(SetupIconSmall);
 if SetupIconLarge <> 0 then FreeSetupIcon(SetupIconLarge);
 if LegacyMoved and not InstallCompleted and not DirExists(LegacyDir) then
  if not RenameFile(FormalDir, LegacyDir) then Log('ASTER_FORMAL_DIRECTORY_ROLLBACK_FAILED');
end;


procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
 if CurUninstallStep = usUninstall then begin
  CleanupMigratedShortcuts;
  if RegQueryStringValue(HKCU, RunRegKey, 'AsterHub', Command) then
   if Command = '"' + ExpandConstant('{app}\AsterHub.exe') + '" --background' then
    RegDeleteValue(HKCU, RunRegKey, 'AsterHub');
 end;
end;

procedure RefreshExplorerIcon(EventId, Flags: Cardinal; Item: String; Reserved: Integer);
 external 'SHChangeNotify@shell32.dll stdcall';

procedure RetargetFormalShortcut(LinkPath: String);
var Shell, Link: Variant; Target: String;
begin
 if not OwnedShortcut(LinkPath) then Exit;
 try
  Shell := CreateOleObject('WScript.Shell'); Link := Shell.CreateShortcut(LinkPath);
  Target := ExpandConstant('{app}\\AsterHub.exe');
  if CompareText(Link.TargetPath, Target) <> 0 then begin
   Link.TargetPath := Target; Link.WorkingDirectory := ExpandConstant('{app}');
   Link.IconLocation := Target + ',0'; Link.Save;
  end;
 except
  Log('ASTER_FORMAL_SHORTCUT_RETARGET_FAILED: ' + LinkPath);
 end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var AppPath, Command, Folder: String; Item: TFindRec;
begin
 if CurStep <> ssPostInstall then Exit;
 MigrateLegacyShortcuts;
 AppPath := ExpandConstant('{app}');
 if (LegacyDir <> '') and (LegacyMoved or (CompareText(AppPath, LegacyDir) = 0)) then begin
  if FileExists(AppPath + '\AsterHub.Preview.exe') then DeleteFile(AppPath + '\AsterHub.Preview.exe');
  if FileExists(AppPath + '\AsterHub.Preview.exe.config') then DeleteFile(AppPath + '\AsterHub.Preview.exe.config');
  if FileExists(AppPath + '\README-预览版.txt') then DeleteFile(AppPath + '\README-预览版.txt');
 end;
 RetargetFormalShortcut(ShortcutFolder('userdesktop') + '\AsterHub.lnk');
 RetargetFormalShortcut(ShortcutFolder('commondesktop') + '\AsterHub.lnk');
 RetargetFormalShortcut(ShortcutFolder('userprograms') + '\AsterHub\AsterHub.lnk');
 RetargetFormalShortcut(ShortcutFolder('commonprograms') + '\AsterHub\AsterHub.lnk');
#ifndef QAInstall
 Folder := ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar');
 if FindFirst(Folder + '\*.lnk', Item) then begin
  try repeat RetargetFormalShortcut(Folder + '\' + Item.Name); until not FindNext(Item);
  finally FindClose(Item); end;
 end;
 if (LegacyDir <> '') and RegQueryStringValue(HKCU, RunRegKey, 'AsterHubPreview', Command) then
  if CompareText(Command, '"' + LegacyDir + '\AsterHub.Preview.exe" --background') = 0 then
   if RegWriteStringValue(HKCU, RunRegKey, 'AsterHub', '"' + AppPath + '\AsterHub.exe" --background') then
    RegDeleteValue(HKCU, RunRegKey, 'AsterHubPreview');
#endif
 InstallCompleted := True;
 RefreshExplorerIcon($00002000, $2005, AppPath + '\AsterHub.exe', 0);
 RefreshExplorerIcon($00002000, $2005, ExpandConstant('{uninstallexe}'), 0);
 RefreshExplorerIcon($00001000, $2005, AppPath, 0);
end;
