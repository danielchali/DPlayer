; DPlayer Inno Setup Installer Script
; Requires Inno Setup 6: https://jrsoftware.org/isinfo.php

#define MyAppName "DPlayer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DPlayer"
#define MyAppURL "https://github.com/DPlayer/DPlayer"
#define MyAppExeName "DPlayer.exe"

[Setup]
AppId={{F8A2B3C4-D5E6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\output
OutputBaseFilename=DPlayer-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "Associate DPlayer with media files"; GroupDescription: "File associations:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Video file associations
Root: HKCU; Subkey: "Software\Classes\.mp4"; ValueType: string; ValueData: "DPlayer.mp4"; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\DPlayer.mp4"; ValueType: string; ValueData: "MP4 Video"; Tasks: fileassoc; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\DPlayer.mp4\DefaultIcon"; ValueType: string; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKCU; Subkey: "Software\Classes\DPlayer.mp4\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

Root: HKCU; Subkey: "Software\Classes\.mkv"; ValueType: string; ValueData: "DPlayer.mkv"; Tasks: fileassoc; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\DPlayer.mkv\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
