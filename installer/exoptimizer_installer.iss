; Exoptimizer v2.1.2 Installer Script
#define MyAppName "Exoptimizer - Gaming Optimization Tool"
#define MyAppVersion "2.1.2"
#define MyAppPublisher "mDev (Mobin Mardi)"
#define MyAppURL "https://mobinmardi.github.io/"
#define MyAppExeName "Exoptimizer.exe"
#define MyAppDisplayName "Exoptimizer.exe"
#define MyAppId "{{B2C3D4E5-F6G7-8901-BCDE-234567890123}"

[Setup]
; Basic App Info
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright © 2025 mDev (Mobin Mardi)

; Installation Settings
DefaultDirName={autopf}\Exoptimizer
DefaultGroupName=Exoptimizer
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
InfoBeforeFile=..\docs\README.txt
OutputDir=output
OutputBaseFilename=Exoptimizer-v2.1.2
SetupIconFile=..\assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression & Style
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; System Requirements
MinVersion=10.0.19041
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

; Uninstall Settings
UninstallDisplayName={#MyAppName} v{#MyAppVersion}
UninstallFilesDir={app}\uninstall

; Version Info
VersionInfoVersion=2.1.2
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright © 2025 mDev (Mobin Mardi)
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion=2.1.2

; Upgrade Settings
AppMutex=ExoptimizerAppMutex
CreateUninstallRegKey=yes
CloseApplications=yes
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; OnlyBelowVersion: 6.1
Name: "runonstartup"; Description: "Run Exoptimizer when Windows starts (Not Recommended)"; GroupDescription: "Startup Options"
Name: "createrestorepoint"; Description: "Create system restore point before installation"; GroupDescription: "Safety Options"

[Files]
; Main Application Files
Source: "..\src\bin\Release\net6.0-windows\win-x64\publish\Exoptimizer.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion replacesameversion
Source: "..\src\bin\Release\net6.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "Exoptimizer.exe"; Flags: ignoreversion replacesameversion recursesubdirs createallsubdirs

; Documentation
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion replacesameversion
Source: "..\docs\README.txt"; DestDir: "{app}"; Flags: ignoreversion replacesameversion

; Icon
Source: "..\assets\icon.ico"; DestDir: "{app}"; Flags: ignoreversion replacesameversion

[Icons]
; Start Menu
Name: "{group}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Comment: "Advanced Windows Gaming Optimizer"
Name: "{group}\{cm:UninstallProgram,Exoptimizer}"; Filename: "{uninstallexe}"; Comment: "Uninstall Exoptimizer"
Name: "{group}\User Guide"; Filename: "{app}\README.txt"; Comment: "Read the user guide"

; Desktop Icon (optional)
Name: "{autodesktop}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon; Comment: "Advanced Windows Gaming Optimizer"

; Quick Launch (optional)
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

; Startup (optional)
Name: "{userstartup}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: runonstartup

[Registry]
; Add to Windows Programs list
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName} v{#MyAppVersion}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "DisplayVersion"; ValueData: "2.1.2"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "Publisher"; ValueData: "{#MyAppPublisher}"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "UninstallString"; ValueData: "{uninstallexe}"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "InstallLocation"; ValueData: "{app}"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "DisplayIcon"; ValueData: "{app}\{#MyAppExeName}"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "HelpLink"; ValueData: "{#MyAppURL}"
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: dword; ValueName: "NoModify"; ValueData: 1
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: dword; ValueName: "NoRepair"; ValueData: 1
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppId}"; ValueType: string; ValueName: "URLInfoAbout"; ValueData: "{#MyAppURL}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Exoptimizer}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill any running instances before uninstall
Filename: "{cmd}"; Parameters: "/c taskkill /f /im ""Exoptimizer.exe"" >nul 2>&1"; Flags: runhidden

[UninstallDelete]
; Clean up any leftover files
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"
Type: files; Name: "{app}\exoptimizer_settings.json"; Check: not IsUpgrade
Type: dirifempty; Name: "{app}"

[Messages]
; Custom messages
WelcomeLabel2=This will install [name/ver] on your computer.%n%nThis application requires Administrator privileges and is designed for Windows gaming optimization.%n%nIt is recommended that you close all other applications before continuing.
FinishedLabelNoIcons=Setup has finished installing [name] on your computer.%n%nIMPORTANT: Always run Exoptimizer as Administrator for proper functionality.%n%nNEW in v2.1.2: Extreme Optimization mode for maximum FPS boost!

[Code]
var
  RestorePointCreated: Boolean;
  ResultCode: Integer;
  IsUpgradeInstall: Boolean;

function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  
  // Check if running as administrator
  if not IsAdminLoggedOn then begin
    MsgBox('This installer requires administrator privileges to install Exoptimizer.' + #13#10 + #13#10 +
           'Please right-click this installer and select "Run as administrator".',
            mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  // Check Windows version (Windows 10 build 19041+ or Windows 11)
  if (Version.Major < 10) or ((Version.Major = 10) and (Version.Build < 19041)) then begin
    MsgBox('Exoptimizer requires Windows 10 version 2004 (build 19041) or later.' + #13#10 +
            'Your current version is not supported.' + #13#10 + #13#10 +
           'Please update Windows and try again.',
            mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  // Check architecture
  if not Is64BitInstallMode then begin
    MsgBox('Exoptimizer requires a 64-bit version of Windows.' + #13#10 +
           'Your system appears to be 32-bit and is not supported.',
            mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  RestorePointDescription: String;
begin
  Result := True;
  
  // Create restore point if user selected the option
  if (CurPageID = wpSelectTasks) and IsTaskSelected('createrestorepoint') then begin
    if MsgBox('Do you want to create a system restore point now?' + #13#10 +
               'This is highly recommended before installing system optimization tools.' + #13#10 + #13#10 +
              'This may take a few minutes...',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES then begin
      
      // Show progress
      WizardForm.StatusLabel.Caption := 'Creating system restore point...';
      WizardForm.ProgressGauge.Style := npbstMarquee;
      
      try
        RestorePointDescription := 'Before Exoptimizer v2.1.2 Installation';
        
        // Create restore point using PowerShell
        if Exec('powershell.exe',
                 '-Command "Checkpoint-Computer -Description ''' + RestorePointDescription + ''' -RestorePointType ''INSTALL''"',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then begin
          RestorePointCreated := True;
          MsgBox('System restore point created successfully!', mbInformation, MB_OK);
        end else begin
          MsgBox('Failed to create restore point. Installation will continue.' + #13#10 +
                 'You can create one manually later from System Properties.',
                  mbInformation, MB_OK);
        end;
      except
        MsgBox('Error creating restore point. Installation will continue.', mbInformation, MB_OK);
      end;
      
      WizardForm.ProgressGauge.Style := npbstNormal;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    MsgBox('Exoptimizer v2.1.2 has been installed successfully!' + #13#10 + #13#10 +
           'IMPORTANT REMINDERS:' + #13#10 +
           '• Always run Exoptimizer as Administrator' + #13#10 +
           '• Create a restore point before optimization' + #13#10 +
           '• Try the new EXTREME OPTIMIZATION for maximum FPS' + #13#10 +
           '• Restart your computer after applying optimizations' + #13#10 +
           '• Use the "Undo Optimizations" feature if needed' + #13#10 + #13#10 +
           'Thank you for using Exoptimizer!',
            mbInformation, MB_OK);
  end;
end;

function InitializeUninstall(): Boolean;
var
  Response: Integer;
begin
  Response := MsgBox('Are you sure you want to completely remove Exoptimizer and all of its components?' + #13#10 + #13#10 +
                     'WARNING: This will not undo any system optimizations that were applied.' + #13#10 +
                     'Use the "Undo Optimizations" feature in the application first if needed.' + #13#10 + #13#10 +
                     'Your settings file will also be removed.' + #13#10 + #13#10 +
                     'Continue with uninstallation?',
                      mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
  Result := Response = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    if MsgBox('Exoptimizer has been uninstalled.' + #13#10 + #13#10 +
              'Would you like to create a system restore point to mark this change?' + #13#10 +
              '(This is optional but recommended)',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then begin
      
      Exec('powershell.exe',
            '-Command "Checkpoint-Computer -Description ''After Exoptimizer Uninstallation'' -RestorePointType ''APPLICATION_UNINSTALL''"',
            '', SW_HIDE, ewNoWait, ResultCode);
    end;
    
    MsgBox('Thank you for using Exoptimizer!' + #13#10 + #13#10 +
           'If you experienced any issues, you can restore your system' + #13#10 +
           'using System Restore from the Windows Control Panel.',
            mbInformation, MB_OK);
  end;
end;

function IsUpgrade(): Boolean;
begin
  Result := False;
end;
