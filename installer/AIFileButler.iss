; Inno Setup script for AI File Butler
; Build:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" AIFileButler.iss
; Produces: Output\AIFileButler-Setup.exe (a single distributable installer)

#define AppName "AI File Butler"
; AppVersion can be overridden from the command line: ISCC /DAppVersion=1.2.3
#ifndef AppVersion
  #define AppVersion "0.0.1"
#endif
#define AppPublisher "AI File Butler"
#define ExeName "AIFileButler.exe"

[Setup]
AppId={{8F3C2A41-9D2E-4B7A-9E2C-AIFILEBUTLER01}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
; Per-user install -> no admin prompt, lands in LocalAppData.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableReadyPage=no
LicenseFile=..\LICENSE.txt
UninstallDisplayName={#AppName}
OutputDir=Output
OutputBaseFilename=AIFileButler-Setup
SetupIconFile=..\app.ico
; Version metadata for the Setup.exe itself (legitimacy / fewer AV flags).
VersionInfoVersion=1.0.0.0
VersionInfoCompany=ForgeLabsSoft
VersionInfoProductName=AI File Butler
VersionInfoDescription=AI File Butler Setup
VersionInfoCopyright=Copyright © 2026 ForgeLabsSoft
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Start {#AppName} automatically when Windows starts"

[Files]
Source: "..\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish\{#ExeName}"; \
    DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Registry]
; Start-with-Windows (per-user Run key); removed on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "AIFileButler"; ValueData: """{app}\{#ExeName}"""; \
    Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#ExeName}"; Description: "Launch {#AppName} now"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Make sure the tray app isn't running so its files can be removed.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#ExeName} /F"; \
    Flags: runhidden; RunOnceId: "KillButler"
