[Setup]
AppId={{9259584C-6413-44CD-806C-7132E59BB1B6}
AppName=Vitakora Cliente
AppVersion=0.4.0
AppPublisher=Viteka
DefaultDirName={autopf}\Vitakora Cliente
DefaultGroupName=Vitakora
OutputDir=..\out
OutputBaseFilename=Vitakora-Cliente-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Dirs]
Name: "{commonappdata}\Vitakora"; Permissions: users-modify

[Files]
Source: "..\out\client\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Vitakora"; Filename: "{app}\Vitakora.Client.exe"
Name: "{group}\Vitakora"; Filename: "{app}\Vitakora.Client.exe"

[Run]
Filename: "{app}\Vitakora.Client.exe"; Description: "Abrir Vitakora"; Flags: nowait postinstall skipifsilent
