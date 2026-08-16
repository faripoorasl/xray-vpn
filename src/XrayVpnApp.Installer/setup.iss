; Xray VPN - Inno Setup Installer Script
; Build with: iscc setup.iss
; Requires Inno Setup 6.x (https://jrsoftware.org/isdl.php)

#define MyAppName "Xray VPN"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "XrayVpn"
#define MyAppURL "https://github.com/yourname/xray-vpn"
#define MyAppExeName "XrayVpn.exe"

[Setup]
AppId={{B7F2A1C3-9D45-4E8F-A6B2-1C3D5E7F8A9B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE.txt
OutputDir=..\..\build\installer
OutputBaseFilename=XrayVpn-{#MyAppVersion}-setup
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
RestartIfNeededByRun=no
CloseApplications=no

[Languages]
Name: "fa"; MessagesFile: "compiler:Languages\Farsi.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Run on Windows startup / اجرا با ویندوز"; GroupDescription: "Other:"

[Files]
Source: "..\..\build\Release\publish\XrayVpn.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\build\Release\publish\xray.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\build\Release\publish\wintun.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\build\Release\publish\geoip.dat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\build\Release\publish\geosite.dat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\build\Release\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon
Name: "{commonprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM XrayVpn.exe /F /T"; Flags: runhidden; RunOnceId "KillApp"
Filename: "{cmd}"; Parameters: "/C taskkill /IM xray.exe /F /T"; Flags: runhidden; RunOnceId "KillXray"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\XrayVpn"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    // Optionally auto-start after install
  end;
end;
