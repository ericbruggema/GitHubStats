; GitHub Stats installer (Inno Setup 6)
;
; Safe update: the installer ONLY writes GitHubStats.exe (and its uninstaller).
; config.json, data\ (stats.json, ui.json, log.txt ...) and share\ are never shipped,
; so existing settings and statistics are never overwritten. The program itself
; creates any missing files on first start.
;
; Existing installation is detected via the "Start with Windows" registry value
; (HKCU\...\Run\GitHubStats) or the portable Desktop\GitHubStats\dist folder,
; and is used as the default install folder so the update lands next to your data.

#define AppName "GitHub Stats"
#define AppVer  "2.3.1"
#define AppExe  "GitHubStats.exe"

[Setup]
AppId={{6E0B4C1A-5F2D-4B8E-9C3A-7D1E2F4A9B60}
AppName={#AppName}
AppVersion={#AppVer}
AppVerName={#AppName} {#AppVer}
AppPublisher=Eric Bruggema
DefaultDirName={code:GetDefaultDir}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer
OutputBaseFilename=GitHubStats-{#AppVer}-Setup
SetupIconFile=UI\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
DirExistsWarning=no

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
; Only the program. No config/data files -> nothing of yours gets overwritten.
Source: "dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName}"; Flags: nowait postinstall skipifsilent

; [UninstallDelete] intentionally empty: uninstalling removes the program but keeps config.json, data\ and share\.

[Code]
function ExistingDir(): String;
var
  V: String;
  P: Integer;
begin
  Result := '';
  { 1. "Start with Windows" value written by the program itself }
  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'GitHubStats', V) then
  begin
    if (Length(V) > 0) and (V[1] = '"') then
    begin
      Delete(V, 1, 1);
      P := Pos('"', V);
      if P > 0 then V := Copy(V, 1, P - 1);
    end;
    V := ExtractFileDir(V);
    if (V <> '') and FileExists(AddBackslash(V) + '{#AppExe}') then
    begin
      Result := V;
      Exit;
    end;
  end;
  { 2. Portable build folder on the desktop }
  V := ExpandConstant('{userdesktop}\GitHubStats\dist');
  if FileExists(AddBackslash(V) + '{#AppExe}') then Result := V;
end;

function GetDefaultDir(Param: String): String;
begin
  Result := ExistingDir();
  if Result = '' then Result := ExpandConstant('{localappdata}\Programs\GitHubStats');
end;
