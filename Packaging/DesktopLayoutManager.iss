#ifndef PayloadDir
  #define PayloadDir "..\artifacts\payload"
#endif
#ifndef ReleaseDir
  #define ReleaseDir "..\dist"
#endif
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{129B68CD-947C-42E1-B468-DF9AB4234D64}
AppName=Desktop Layout Manager
AppVersion={#AppVersion}
AppPublisher=Desktop Layout Manager
DefaultDirName={localappdata}\Programs\DesktopLayoutManager
DefaultGroupName=Desktop Layout Manager
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
WizardStyle=modern
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\DesktopLayoutManager.exe
SetupIconFile=..\Native\Assets\AppIcon.ico
OutputDir={#ReleaseDir}
OutputBaseFilename=DesktopLayoutManager-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Desktop Layout Manager
VersionInfoVersion={#AppVersion}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Desktop Layout Manager"; Filename: "{app}\DesktopLayoutManager.exe"; WorkingDir: "{app}"; IconFilename: "{app}\DesktopLayoutManager.exe"
Name: "{group}\Uninstall Desktop Layout Manager"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Desktop Layout Manager"; Filename: "{app}\DesktopLayoutManager.exe"; WorkingDir: "{app}"; IconFilename: "{app}\DesktopLayoutManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DesktopLayoutManager.exe"; Description: "Launch Desktop Layout Manager"; Flags: nowait postinstall skipifsilent
