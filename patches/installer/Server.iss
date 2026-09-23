[Setup]
AppId={{4F48A2E6-EC23-4B54-B1E2-90F7C174E858}
AppName=Vitakora Servidor
AppVersion=0.4.1
AppPublisher=Viteka
DefaultDirName={autopf}\Vitakora
DefaultGroupName=Vitakora
OutputDir=..\out
OutputBaseFilename=Vitakora-Servidor-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Dirs]
Name: "{commonappdata}\Vitakora"; Permissions: users-modify

[Files]
Source: "..\out\server\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\out\client\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\Vitakora"; Filename: "{app}\Client\Vitakora.Client.exe"
Name: "{group}\Vitakora"; Filename: "{app}\Client\Vitakora.Client.exe"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "VitakoraServer"; ValueData: """{app}\Server\Vitakora.Server.exe"""; Flags: uninsdeletevalue

[Run]
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall delete rule name=""Vitakora Server"" >nul 2>&1 & netsh advfirewall firewall add rule name=""Vitakora Server"" dir=in action=allow protocol=TCP localport=5000 profile=private"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C netsh advfirewall firewall delete rule name=""Vitakora Discovery"" >nul 2>&1 & netsh advfirewall firewall add rule name=""Vitakora Discovery"" dir=in action=allow protocol=UDP localport=50505 profile=private"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C schtasks /Delete /TN ""Vitakora Server"" /F >nul 2>&1"; Flags: runhidden waituntilterminated
Filename: "{cmd}"; Parameters: "/C schtasks /Create /TN ""Vitakora Server"" /TR """"""{app}\Server\Vitakora.Server.exe"""""" /SC ONSTART /RU SYSTEM /RL HIGHEST /F"; Flags: runhidden waituntilterminated
Filename: "{app}\Server\Vitakora.Server.exe"; Flags: nowait runhidden
Filename: "{cmd}"; Parameters: "/C timeout /T 4 /NOBREAK >nul"; Flags: runhidden waituntilterminated
Filename: "{app}\Client\Vitakora.Client.exe"; Description: "Abrir Vitakora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /F /IM Vitakora.Server.exe >nul 2>&1 & schtasks /End /TN ""Vitakora Server"" >nul 2>&1 & schtasks /Delete /TN ""Vitakora Server"" /F >nul 2>&1 & netsh advfirewall firewall delete rule name=""Vitakora Server"" >nul 2>&1 & netsh advfirewall firewall delete rule name=""Vitakora Discovery"" >nul 2>&1"; Flags: runhidden waituntilterminated
