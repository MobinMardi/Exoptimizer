; Exoptimizer v3.2.0 Installer Script
#define MyAppName "Exoptimizer - Gaming Optimization Tool"
#define MyAppVersion "3.2.0"
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
AppCopyright=Copyright © 2026 mDev (Mobin Mardi)

; Installation Settings
DefaultDirName={autopf}\Exoptimizer
DefaultGroupName=Exoptimizer
AllowNoIcons=yes
LicenseFile=..\LICENSE.txt
InfoBeforeFile=..\docs\README.txt
OutputDir=output
OutputBaseFilename=Exoptimizer-v3.2.0
SetupIconFile=..\assets\icon-new.ico
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
VersionInfoVersion=3.2.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoCopyright=Copyright © 2026 mDev (Mobin Mardi)
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion=3.2.0

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
Source: "..\src\bin\Release\net10.0-windows\win-x64\publish\Exoptimizer.exe"; DestDir: "{app}"; DestName: "{#MyAppExeName}"; Flags: ignoreversion
Source: "..\src\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "Exoptimizer.exe"; Flags: ignoreversion recursesubdirs createallsubdirs

; Documentation
Source: "..\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\README.txt"; DestDir: "{app}"; Flags: ignoreversion

; Icon
Source: "..\assets\icon-new.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Start Menu
Name: "{group}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon-new.ico"; Comment: "Advanced Windows Gaming Optimizer"
Name: "{group}\{cm:UninstallProgram,Exoptimizer}"; Filename: "{uninstallexe}"; Comment: "Uninstall Exoptimizer"
Name: "{group}\User Guide"; Filename: "{app}\README.txt"; Comment: "Read the user guide"

; Desktop Icon (optional)
Name: "{autodesktop}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon-new.ico"; Tasks: desktopicon; Comment: "Advanced Windows Gaming Optimizer"

; Quick Launch (optional)
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

; Startup (optional)
Name: "{userstartup}\Exoptimizer"; Filename: "{app}\{#MyAppExeName}"; Tasks: runonstartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Exoptimizer}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill any running instances before uninstall
Filename: "{cmd}"; Parameters: "/c taskkill /f /im ""Exoptimizer.exe"" >nul 2>&1"; Flags: runhidden

[UninstallDelete]
; Clean up any leftover files
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"
Type: files; Name: "{app}\exoptimizer_settings.json"
Type: dirifempty; Name: "{app}"

[Messages]
; Custom messages
WelcomeLabel2=This will install [name/ver] on your computer.%n%nThis application requires Administrator privileges and is designed for Windows gaming optimization.%n%nIt is recommended that you close all other applications before continuing.
FinishedLabelNoIcons=Setup has finished installing [name] on your computer.%n%nIMPORTANT: Always run Exoptimizer as Administrator for proper functionality.%n%nSince v3.0.0: System Restore, your network, and your firewall are permanently protected from every optimization mode - see the changelog for details.

[Code]
const
  LegacyUninstallKey = 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{B2C3D4E5-F6G7-8901-BCDE-234567890123}';
  // Base64 (UTF-16LE) PowerShell scripts that create a restore point and then CONFIRM it exists (exit code 0 only if it does).
  InstallRestorePointScript = 'JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQA9ACcAUwB0AG8AcAAnADsAIAAkAGQAZQBzAGMAPQAnAEIAZQBmAG8AcgBlACAARQB4AG8AcAB0AGkAbQBpAHoAZQByACAAdgAzAC4AMgAuADAAIABJAG4AcwB0AGEAbABsAGEAdABpAG8AbgAnADsAIAAkAGIAZQBmAG8AcgBlAD0AQAAoAEcAZQB0AC0AQwBvAG0AcAB1AHQAZQByAFIAZQBzAHQAbwByAGUAUABvAGkAbgB0ACAALQBFAHIAcgBvAHIAQQBjAHQAaQBvAG4AIABTAGkAbABlAG4AdABsAHkAQwBvAG4AdABpAG4AdQBlACAAfAAgAEYAbwByAEUAYQBjAGgALQBPAGIAagBlAGMAdAAgAHsAIABbAHMAdAByAGkAbgBnAF0AJABfAC4AUwBlAHEAdQBlAG4AYwBlAE4AdQBtAGIAZQByACAAfQApADsAIABDAGgAZQBjAGsAcABvAGkAbgB0AC0AQwBvAG0AcAB1AHQAZQByACAALQBEAGUAcwBjAHIAaQBwAHQAaQBvAG4AIAAkAGQAZQBzAGMAIAAtAFIAZQBzAHQAbwByAGUAUABvAGkAbgB0AFQAeQBwAGUAIABBAFAAUABMAEkAQwBBAFQASQBPAE4AXwBJAE4AUwBUAEEATABMADsAIABTAHQAYQByAHQALQBTAGwAZQBlAHAAIAAtAFMAZQBjAG8AbgBkAHMAIAAyADsAIAAkAGEAZgB0AGUAcgA9AEAAKABHAGUAdAAtAEMAbwBtAHAAdQB0AGUAcgBSAGUAcwB0AG8AcgBlAFAAbwBpAG4AdAAgAHwAIABXAGgAZQByAGUALQBPAGIAagBlAGMAdAAgAHsAIAAkAF8ALgBEAGUAcwBjAHIAaQBwAHQAaQBvAG4AIAAtAGUAcQAgACQAZABlAHMAYwAgAC0AYQBuAGQAIAAoACQAYgBlAGYAbwByAGUAIAAtAG4AbwB0AGMAbwBuAHQAYQBpAG4AcwAgAFsAcwB0AHIAaQBuAGcAXQAkAF8ALgBTAGUAcQB1AGUAbgBjAGUATgB1AG0AYgBlAHIAKQAgAH0AKQA7ACAAaQBmACAAKAAkAGEAZgB0AGUAcgAuAEMAbwB1AG4AdAAgAC0AZwB0ACAAMAApACAAewAgAGUAeABpAHQAIAAwACAAfQAgAGUAbABzAGUAIAB7ACAAZQB4AGkAdAAgADIAIAB9AA==';
  UninstallRestorePointScript = 'JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQA9ACcAUwB0AG8AcAAnADsAIAAkAGQAZQBzAGMAPQAnAEEAZgB0AGUAcgAgAEUAeABvAHAAdABpAG0AaQB6AGUAcgAgAFUAbgBpAG4AcwB0AGEAbABsAGEAdABpAG8AbgAnADsAIAAkAGIAZQBmAG8AcgBlAD0AQAAoAEcAZQB0AC0AQwBvAG0AcAB1AHQAZQByAFIAZQBzAHQAbwByAGUAUABvAGkAbgB0ACAALQBFAHIAcgBvAHIAQQBjAHQAaQBvAG4AIABTAGkAbABlAG4AdABsAHkAQwBvAG4AdABpAG4AdQBlACAAfAAgAEYAbwByAEUAYQBjAGgALQBPAGIAagBlAGMAdAAgAHsAIABbAHMAdAByAGkAbgBnAF0AJABfAC4AUwBlAHEAdQBlAG4AYwBlAE4AdQBtAGIAZQByACAAfQApADsAIABDAGgAZQBjAGsAcABvAGkAbgB0AC0AQwBvAG0AcAB1AHQAZQByACAALQBEAGUAcwBjAHIAaQBwAHQAaQBvAG4AIAAkAGQAZQBzAGMAIAAtAFIAZQBzAHQAbwByAGUAUABvAGkAbgB0AFQAeQBwAGUAIABBAFAAUABMAEkAQwBBAFQASQBPAE4AXwBVAE4ASQBOAFMAVABBAEwATAA7ACAAUwB0AGEAcgB0AC0AUwBsAGUAZQBwACAALQBTAGUAYwBvAG4AZABzACAAMgA7ACAAJABhAGYAdABlAHIAPQBAACgARwBlAHQALQBDAG8AbQBwAHUAdABlAHIAUgBlAHMAdABvAHIAZQBQAG8AaQBuAHQAIAB8ACAAVwBoAGUAcgBlAC0ATwBiAGoAZQBjAHQAIAB7ACAAJABfAC4ARABlAHMAYwByAGkAcAB0AGkAbwBuACAALQBlAHEAIAAkAGQAZQBzAGMAIAAtAGEAbgBkACAAKAAkAGIAZQBmAG8AcgBlACAALQBuAG8AdABjAG8AbgB0AGEAaQBuAHMAIABbAHMAdAByAGkAbgBnAF0AJABfAC4AUwBlAHEAdQBlAG4AYwBlAE4AdQBtAGIAZQByACkAIAB9ACkAOwAgAGkAZgAgACgAJABhAGYAdABlAHIALgBDAG8AdQBuAHQAIAAtAGcAdAAgADAAKQAgAHsAIABlAHgAaQB0ACAAMAAgAH0AIABlAGwAcwBlACAAewAgAGUAeABpAHQAIAAyACAAfQA=';

var
  ResultCode: Integer;

function RecoveryDataFolder(): String;
begin
  Result := ExpandConstant('{commonappdata}\Exoptimizer');
end;

function HasRecoveryData(): Boolean;
begin
  Result := FileExists(RecoveryDataFolder() + '\journal.json');
end;

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
begin
  Result := True;

  // Create restore point if user selected the option
  if (CurPageID = wpSelectTasks) and IsTaskSelected('createrestorepoint') then begin
    if MsgBox('Do you want to create a system restore point now?' + #13#10 +
               'This is highly recommended before installing system optimization tools.' + #13#10 + #13#10 +
              'This may take a few minutes...',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES then begin

      WizardForm.StatusLabel.Caption := 'Creating system restore point...';
      WizardForm.ProgressGauge.Style := npbstMarquee;

      try
        // The script exits with 0 only if Windows reports the new restore point, so the result is checked - not assumed.
        if Exec('powershell.exe',
                 '-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + InstallRestorePointScript,
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then begin
          MsgBox('System restore point created and confirmed.', mbInformation, MB_OK);
        end else begin
          MsgBox('Windows did not create (or could not confirm) a restore point.' + #13#10 +
                 'System Protection may be turned off, or Windows may limit restore points to one every 24 hours.' + #13#10 + #13#10 +
                 'Installation will continue. You can create one manually from System Properties.',
                  mbInformation, MB_OK);
        end;
      except
        MsgBox('There was an error while creating the restore point. Installation will continue.', mbInformation, MB_OK);
      end;

      WizardForm.ProgressGauge.Style := npbstNormal;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin
    // Older versions also wrote a second, hand-made uninstall entry that showed up as a duplicate in "Installed apps".
    RegDeleteKeyIncludingSubkeys(HKLM, LegacyUninstallKey);
  end;

  if CurStep = ssPostInstall then begin
    MsgBox('Exoptimizer v3.2.0 has been installed successfully!' + #13#10 + #13#10 +
           'IMPORTANT REMINDERS:' + #13#10 +
           '• Always run Exoptimizer as Administrator' + #13#10 +
           '• Exoptimizer creates and verifies a restore point before optimizing' + #13#10 +
           '• Every change is recorded, so "Undo Optimizations" restores exactly what was there' + #13#10 +
           '• Restart your computer after applying optimizations' + #13#10 + #13#10 +
           'Thank you for using Exoptimizer!',
            mbInformation, MB_OK);
  end;
end;

function InitializeUninstall(): Boolean;
var
  Question: String;
begin
  Question := 'Are you sure you want to remove Exoptimizer?' + #13#10 + #13#10 +
              'Uninstalling does NOT undo optimizations that were applied. Use "Undo Optimizations" in the application first if you want to revert them.' + #13#10 + #13#10;

  if HasRecoveryData() then
    Question := Question +
                'Exoptimizer has recorded changes on this PC. That recovery data is kept in ' + RecoveryDataFolder() +
                ' so that Undo still works if you reinstall - you will be asked about it at the end.' + #13#10 + #13#10;

  Question := Question + 'Your settings file will also be removed.' + #13#10 + #13#10 + 'Continue with uninstallation?';

  Result := MsgBox(Question, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    if HasRecoveryData() then begin
      if MsgBox('Keep Exoptimizer''s recovery data?' + #13#10 + #13#10 +
                'It records the original value of everything Exoptimizer changed. Keeping it means "Undo Optimizations" can still work if you reinstall Exoptimizer.' + #13#10 + #13#10 +
                'Choose No only if you are sure you will never need to undo.',
                 mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDNO then begin
        DelTree(RecoveryDataFolder(), True, True, True);
      end;
    end;

    if MsgBox('Exoptimizer has been uninstalled.' + #13#10 + #13#10 +
              'Would you like to create a system restore point to mark this change?' + #13#10 +
              '(This is optional but recommended)',
               mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then begin

      if not (Exec('powershell.exe',
                   '-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand ' + UninstallRestorePointScript,
                   '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0)) then begin
        MsgBox('Windows did not create (or could not confirm) a restore point.', mbInformation, MB_OK);
      end;
    end;

    MsgBox('Thank you for using Exoptimizer!' + #13#10 + #13#10 +
           'If you experienced any issues, you can restore your system' + #13#10 +
           'using System Restore from the Windows Control Panel.',
            mbInformation, MB_OK);
  end;
end;
