[Setup]
AppName=Ojaswat
AppVersion=42.0.0
AppPublisher=Rushi Dave
DefaultDirName={autopf}\Ojaswat
DefaultGroupName=Ojaswat
OutputDir=.\Installer
OutputBaseFilename=ojaswat_V_42_0_0
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
RestartApplications=yes
UsePreviousAppDir=yes
SetupIconFile=.\Installer\src\logo.ico
WizardSmallImageFile=.\Installer\src\small.bmp
WizardImageFile=.\Installer\src\side.bmp

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\Ojaswat";       Filename: "{app}\Ojaswat.exe"
Name: "{commondesktop}\Ojaswat"; Filename: "{app}\Ojaswat.exe"

[Run]
Filename: "{app}\Ojaswat.exe"; Description: "Launch Ojaswat"; Flags: nowait postinstall skipifsilent















