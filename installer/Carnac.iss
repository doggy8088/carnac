; Inno Setup script for the per-user Carnac installer published to winget
; as doggy8088.Carnac.
;
; Build Release first, then compile with:
;   iscc /DAppVersion=2.4.0 installer\Carnac.iss
; The installer is written to deploy\Carnac-<version>-Setup.exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef BuildDir
  #define BuildDir "..\src\Carnac\bin\Release"
#endif
#ifndef OutputDir
  #define OutputDir "..\deploy"
#endif

[Setup]
; Keep AppId stable across releases: winget and upgrades rely on it.
AppId={{DB5C24A2-1558-4272-8C3C-676D68A1E405}
AppName=Carnac
AppVersion={#AppVersion}
AppVerName=Carnac {#AppVersion}
AppPublisher=doggy8088
AppPublisherURL=https://carnac.gh.miniasp.com/
AppSupportURL=https://github.com/doggy8088/carnac/issues
AppUpdatesURL=https://github.com/doggy8088/carnac/releases
AppCopyright=Copyright (c) Carnac contributors
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Carnac
VersionInfoDescription=Carnac Setup
; Per-user install into %LocalAppData%\Programs\Carnac, no elevation needed.
PrivilegesRequired=lowest
DefaultDirName={autopf}\Carnac
DisableProgramGroupPage=yes
UninstallDisplayName=Carnac
UninstallDisplayIcon={app}\Carnac.exe
SetupIconFile=..\src\Carnac\icon.ico
; Carnac runs in the tray, so close it before replacing files during upgrades.
CloseApplications=force
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=Carnac-{#AppVersion}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start Carnac when I sign in to Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; Costura embeds all dependencies into Carnac.exe, so this is the whole app.
; installer\New-PortableZip.ps1 packs the portable zip from these lines: keep them to plain
; Source/DestDir/Flags entries below {app}, or teach that script (and Test-PortableZip.ps1) first.
Source: "{#BuildDir}\Carnac.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\Carnac.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\Keymaps\*.yml"; DestDir: "{app}\Keymaps"; Flags: ignoreversion
Source: "..\LICENSE.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Carnac"; Filename: "{app}\Carnac.exe"
Name: "{autodesktop}\Carnac"; Filename: "{app}\Carnac.exe"; Tasks: desktopicon
Name: "{userstartup}\Carnac"; Filename: "{app}\Carnac.exe"; Tasks: startup

[Run]
Filename: "{app}\Carnac.exe"; Description: "{cm:LaunchProgram,Carnac}"; Flags: nowait postinstall skipifsilent

[Code]
// .NET Framework 4.5.2 reports Release 379893 or later.
function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := True;
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release)
    or (Release < 379893) then
  begin
    SuppressibleMsgBox('Carnac needs .NET Framework 4.5.2 or later. Install it from ' +
      'https://dotnet.microsoft.com/download/dotnet-framework and then run Setup again.',
      mbCriticalError, MB_OK, IDOK);
    Result := False;
  end;
end;

// The uninstaller doesn't use Restart Manager, so stop a running Carnac
// ourselves. Only the copy in {app} is stopped, never other installs.
procedure StopRunningCarnac();
var
  Locator, Service, Processes: Variant;
  ExePath: String;
  I: Integer;
begin
  ExePath := ExpandConstant('{app}\Carnac.exe');
  StringChangeEx(ExePath, '\', '\\', True);
  StringChangeEx(ExePath, '''', '\''', True);
  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');
    Service := Locator.ConnectServer('.', 'root\CIMV2');
    Processes := Service.ExecQuery('SELECT * FROM Win32_Process WHERE ExecutablePath = ''' + ExePath + '''');
    for I := 0 to Processes.Count - 1 do
      Processes.ItemIndex(I).Terminate();
    if Processes.Count > 0 then
      Sleep(1000);
  except
    Log('Could not stop a running Carnac: ' + GetExceptionMessage);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopRunningCarnac();
end;
