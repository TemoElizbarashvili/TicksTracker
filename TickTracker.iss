; ====================================
; TickTracker Installer
; ====================================

#define AppVer GetFileVersion("publish\UI\TickTracker.exe")

[Setup]
AppName=TickTracker
AppVersion={#AppVer}
AppId={{D2E97107-8A5D-4F0A-9A48-2B1C7DFAE5A1}
DefaultDirName={pf}\TickTracker
DefaultGroupName=TickTracker
OutputBaseFilename=TickTrackerSetup
Compression=lzma
SolidCompression=yes
DisableDirPage=no
DisableProgramGroupPage=no

[Files]
; Background tracker (Service project)
Source: "{#SourcePath}\publish\Service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs

; UI (WPF viewer)
Source: "{#SourcePath}\publish\UI\*"; DestDir: "{app}\UI"; Flags: ignoreversion recursesubdirs

[Dirs]
; Ensure ProgramData folder exists for SQLite DB (your code also creates it)
; Grant normal users modify rights so the UI can write settings.
Name: "{commonappdata}\TickTracker"; Flags: uninsneveruninstall; Permissions: users-modify

[Icons]
; Start Menu shortcut for the UI
Name: "{group}\TickTracker"; Filename: "{app}\UI\TickTracker.exe"

; Optional desktop shortcut for UI
Name: "{userdesktop}\TickTracker"; Filename: "{app}\UI\TickTracker.exe"; Tasks: desktopicon

; Startup shortcut for tracker so it auto-runs on logon
Name: "{userstartup}\TickTracker Tracker"; Filename: "{app}\Service\TickTracker.Service.exe"

[Run]
Filename: "{app}\Service\TickTracker.Service.exe"; \
   Flags: nowait runhidden

[UninstallRun]
; Stop background tracker before deleting files
Filename: "taskkill.exe"; Parameters: "/IM TickTracker.Service.exe /F"; Flags: runhidden

   
[Tasks]
; Optional checkbox in installer for desktop icon
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked


