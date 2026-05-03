#define AppVersion "9.0.0"
#define BuildDir "out\build\x64-clang-release"

[Setup]
AppName=ImPlay
AppVersion={#AppVersion}
DefaultDirName={localappdata}\ImPlay
DefaultGroupName=ImPlay
UninstallDisplayIcon={app}\ImPlay.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Installer
OutputBaseFilename=ImPlay_9_Setup
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64
CloseApplications=yes
AppPublisher=tsl0922

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Package the build output produced by Clang in the out directory.
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{userprograms}\ImPlay"; Filename: "{app}\ImPlay.exe"; WorkingDir: "{app}"
Name: "{userdesktop}\ImPlay"; Filename: "{app}\ImPlay.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\ImPlay.exe"; Description: "Launch ImPlay"; Flags: nowait postinstall skipifsilent
