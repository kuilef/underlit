; Inno Setup 6 script for Underlit.
; Build the app first:
;   dotnet publish src/Underlit/Underlit.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
; Then run Inno Setup Compiler on this .iss.

#define MyAppName "Underlit"
; MyAppVersion is injected by CI on the command line: /DMyAppVersion=x.y.z
; If compiling locally (without /D), fall back to this default.
#ifndef MyAppVersion
  #define MyAppVersion "0.7.3"
#endif
#ifndef MyAppSourceDir
  #define MyAppSourceDir "..\publish"
#endif
#define MyAppPublisher "Underlit"
#define MyAppExeName "Underlit.exe"

[Setup]
AppId={{A4F9E6D2-0001-4F40-8F2B-3EA7D9E8F111}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/kuilef/underlit
AppSupportURL=https://github.com/kuilef/underlit/issues
AppUpdatesURL=https://github.com/kuilef/underlit/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=UnderlitSetup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter=Underlit.exe
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startwithwindows"; Description: "Start {#MyAppName} when I sign in"; GroupDescription: "Auto-start:"; Flags: unchecked
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; PublishSingleFile + native-library bundling produces one complete executable.
; Do not include sibling experimental builds or old outputs from publish/.
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; {autodesktop} = user's desktop on per-user installs, all-users desktop on admin installs.
; Using {commondesktop} here caused "Access is denied" (0x80070005) on per-user installs
; because writing to C:\Users\Public\Desktop needs admin.
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Underlit"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startwithwindows

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; AutoStart.EnsureValid also registers this task after the application is run.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""\Underlit\Autostart"" /F"; Flags: runhidden waituntilterminated; RunOnceId: "RemoveUnderlitAutostartTask"

[UninstallDelete]
; Older versions also used this application-owned Startup shortcut.
Type: files; Name: "{userstartup}\Underlit.lnk"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    { The app can enable startup after installation, outside the install log. }
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Underlit');
end;
