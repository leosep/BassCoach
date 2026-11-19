; Inno Setup script. Build with:  ISCC.exe /DAppVersion=1.0.0 installer\BassCoach.iss
; Expects the self-contained publish output in ..\publish
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{B7C1E2A4-5D93-4F0A-9E61-2A8F4C7D1B30}
AppName=Bass Coach
AppVersion={#AppVersion}
AppPublisher=Bass Coach
DefaultDirName={autopf}\Bass Coach
DefaultGroupName=Bass Coach
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=Install-BassCoach-{#AppVersion}
SetupIconFile=..\src\BassCoach.Desktop\Assets\bass.ico
UninstallDisplayIcon={app}\BassCoach.exe
UninstallDisplayName=Bass Coach
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Bass Coach"; Filename: "{app}\BassCoach.exe"
Name: "{autodesktop}\Bass Coach"; Filename: "{app}\BassCoach.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\BassCoach.exe"; Description: "Open Bass Coach now"; Flags: nowait postinstall skipifsilent
