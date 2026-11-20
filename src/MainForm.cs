using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Security.Principal;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Linq;
using System.Collections.Generic;

namespace Exoptimizer
{
    public partial class MainForm : Form
    {
        public const string AppVersion = "3.2.0";

        private bool isOptimized = false;
        private bool valorantPriorityActive = false;
        private bool systemTrayEnabled = false;
        private bool allowVisible = true;
        private bool isExtremeOptimized = false;
        private bool optimizationPartial = false;
        private bool extremePartial = false;

        // Security options (off by default; kept while navigating between pages)
        private bool optDisableRealtime = false;
        private bool optAddExclusions = false;

        // Control references
        private Button? optimizeButton;
        private CheckBox? defenderCheckBox;
        private Label? statusLabel;
        private Button? valorantEnablePriority;
        private Button? valorantDisablePriority;
        private Label? valorantStatusLabel;

        // Gaming Network Mode controls
        private Button? networkModeEnableButton;
        private Button? networkModeDisableButton;
        private Label? networkModeStatusLabel;
        private bool gamingNetworkModeActive = false;

        // Navigation
        private Panel? contentPanel;
        private Panel? navigationPanel;
        private string currentSection = "optimization";

        // Update checking
        private Label? updateStatusLabel;
        private UpdateCheckResult? latestUpdateResult;

        // System Monitor controls
        private System.Windows.Forms.Timer? monitorTimer;

        // System Tray
        private NotifyIcon? notifyIcon;

        // Settings
        private readonly string settingsFilePath;

        // Dark mode support
        private bool isDarkMode = false;

        public MainForm()
        {
            settingsFilePath = Path.Combine(Application.StartupPath, "exoptimizer_settings.json");
            LoadSettings();
            ApplyTheme(); // Apply theme before initializing components
            InitializeComponent();
            CheckAdminRights();
            SetupMonitorTimer();
            SetupSystemTray();
            ApplyLoadedSettings();
            
            // Load default content after everything is initialized
            this.Load += (s, e) => LoadOptimizationContent();
            this.Load += async (s, e) => await CheckForUpdatesOnStartupAsync();
        }

        /// <summary>
        /// Checks GitHub for a newer release once, shortly after the window
        /// appears. Runs in the background (never blocks the UI) and never
        /// shows a popup on its own - a silent failure here (no internet,
        /// GitHub unreachable, etc.) should be invisible to the user. The
        /// "Check for Updates" button in Settings surfaces the same check
        /// with visible feedback either way.
        /// </summary>
        private async Task CheckForUpdatesOnStartupAsync()
        {
            var result = await UpdateChecker.CheckForUpdateAsync();

            if (this.IsDisposed || !this.IsHandleCreated) return;

            latestUpdateResult = result;

            if (result.Success && result.UpdateAvailable)
            {
                SetUpdateBadge(true);
            }
        }

        private string BuildUpdateStatusText()
        {
            if (latestUpdateResult == null) return "Click \"Check for Updates\" to look for a newer release.";
            if (!latestUpdateResult.Success) return $"Last check failed: {latestUpdateResult.ErrorMessage}";
            if (latestUpdateResult.UpdateAvailable) return $"🔔 A new version is available: v{latestUpdateResult.LatestVersion} (you have v{AppVersion})";
            return $"✓ You're up to date (v{AppVersion}).";
        }

        private async void CheckForUpdatesButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            string originalText = button?.Text ?? "Check for Updates";

            if (button != null)
            {
                button.Enabled = false;
                button.Text = "Checking...";
            }

            var result = await UpdateChecker.CheckForUpdateAsync();
            latestUpdateResult = result;

            // The user may have navigated away from Settings while this was
            // in flight, which disposes the old controls - guard before
            // touching them again.
            if (button != null && !button.IsDisposed)
            {
                button.Enabled = true;
                button.Text = originalText;
            }

            SetUpdateBadge(result.Success && result.UpdateAvailable);

            if (updateStatusLabel != null)
            {
                updateStatusLabel.Text = BuildUpdateStatusText();
                updateStatusLabel.ForeColor = (result.Success && result.UpdateAvailable) ? SuccessColor : TextSecondary;
            }

            if (!result.Success)
            {
                AppDialog.Show(
                    $"Couldn't check for updates: {result.ErrorMessage}" + Environment.NewLine + Environment.NewLine +
                    "Make sure you're connected to the internet.",
                    "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (result.UpdateAvailable)
            {
                var openResult = AppDialog.Show(
                    $"A new version is available: v{result.LatestVersion}" + Environment.NewLine +
                    $"You're currently running v{AppVersion}." + Environment.NewLine + Environment.NewLine +
                    "Open the release page now?",
                    "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (openResult == DialogResult.Yes && !string.IsNullOrWhiteSpace(result.ReleaseUrl))
                {
                    try { Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true }); }
                    catch { }
                }
            }
            else
            {
                AppDialog.Show($"You're up to date! (v{AppVersion})", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Method to check VALORANT priority status
        private void CheckValorantPriorityStatus()
        {
            try
            {
                var valorantProcesses = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                
                if (valorantProcesses.Length > 0)
                {
                    var process = valorantProcesses[0];
                    var priority = process.PriorityClass;
                    
                    if (priority == ProcessPriorityClass.High || priority == ProcessPriorityClass.RealTime)
                    {
                        valorantPriorityActive = true;
                        if (valorantStatusLabel != null)
                        {
                            valorantStatusLabel.Text = $"Priority: Active ({priority})";
                            valorantStatusLabel.ForeColor = SuccessColor;
                        }
                        if (valorantEnablePriority != null) valorantEnablePriority.Enabled = false;
                        if (valorantDisablePriority != null) valorantDisablePriority.Enabled = true;
                    }
                    else
                    {
                        valorantPriorityActive = false;
                        if (valorantStatusLabel != null)
                        {
                            valorantStatusLabel.Text = $"Priority: Normal ({priority})";
                            valorantStatusLabel.ForeColor = TextSecondary;
                        }
                        if (valorantEnablePriority != null) valorantEnablePriority.Enabled = true;
                        if (valorantDisablePriority != null) valorantDisablePriority.Enabled = false;
                    }
                }
                else
                {
                    valorantPriorityActive = false;
                    if (valorantStatusLabel != null)
                    {
                        valorantStatusLabel.Text = "Priority: VALORANT not running";
                        valorantStatusLabel.ForeColor = TextSecondary;
                    }
                    if (valorantEnablePriority != null) valorantEnablePriority.Enabled = false;
                    if (valorantDisablePriority != null) valorantDisablePriority.Enabled = false;
                }
            }
            catch
            {
                valorantPriorityActive = false;
                if (valorantStatusLabel != null)
                {
                    valorantStatusLabel.Text = "Priority: Unable to check";
                    valorantStatusLabel.ForeColor = TextSecondary;
                }
                if (valorantEnablePriority != null) valorantEnablePriority.Enabled = true;
                if (valorantDisablePriority != null) valorantDisablePriority.Enabled = false;
            }
        }

        // Settings and system tray methods
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string jsonString = File.ReadAllText(settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(jsonString);
                    
                    if (settings != null)
                    {
                        systemTrayEnabled = settings.SystemTrayEnabled;
                        isDarkMode = settings.DarkModeEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                AppDialog.Show($"Failed to load settings: {ex.Message}" + Environment.NewLine + "Using default settings.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                systemTrayEnabled = false;
                isDarkMode = false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new AppSettings
                {
                    SystemTrayEnabled = systemTrayEnabled,
                    DarkModeEnabled = isDarkMode
                };

                string jsonString = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFilePath, jsonString);
            }
            catch (Exception ex)
            {
                AppDialog.Show($"Failed to save settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyLoadedSettings()
        {
            if (notifyIcon != null)
            {
                notifyIcon.Visible = systemTrayEnabled;
            }
            
            if (systemTrayEnabled)
            {
                allowVisible = false;
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
            else
            {
                allowVisible = true;
                this.ShowInTaskbar = true;
            }
        }

        private void SetupMonitorTimer()
        {
            monitorTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000
            };
            monitorTimer.Tick += UpdateMonitorData;
            monitorTimer.Start();
        }

        private void SetupSystemTray()
        {
            notifyIcon = new NotifyIcon
            {
                Text = "Exoptimizer",
                Visible = systemTrayEnabled
            };

            try
            {
                // Use the main application icon for system tray
                string iconPath = Path.Combine(Application.StartupPath, "icon-new.ico");
                if (File.Exists(iconPath))
                {
                    notifyIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    // Try alternative path
                    iconPath = Path.Combine(Application.StartupPath, "..", "assets", "icon-new.ico");
                    if (File.Exists(iconPath))
                    {
                        notifyIcon.Icon = new Icon(iconPath);
                    }
                    else
                    {
                        notifyIcon.Icon = SystemIcons.Application;
                    }
                }
            }
            catch
            {
                // Fallback to system icon
                notifyIcon.Icon = SystemIcons.Application;
            }

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Show", null, (s, e) => 
            {
                allowVisible = true;
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
                this.Activate();
            });
            contextMenu.Items.Add("Exit", null, (s, e) => 
            {
                if (OperationGate.IsBusy)
                {
                    AppDialog.Show($"Exoptimizer is busy ({OperationGate.Current}). Please wait for it to finish before exiting, so your system isn't left half-changed.",
                        "Operation in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    monitorTimer?.Stop();
                    monitorTimer?.Dispose();
                    
                    if (notifyIcon != null)
                    {
                        notifyIcon.Visible = false;
                        notifyIcon.Dispose();
                    }
                    
                    Application.ExitThread();
                    Environment.Exit(0);
                }
                catch
                {
                    Environment.Exit(0);
                }
            });
            notifyIcon.ContextMenuStrip = contextMenu;

            notifyIcon.DoubleClick += (s, e) => 
            {
                allowVisible = true;
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.BringToFront();
                this.Activate();
            };
        }

        protected override void SetVisibleCore(bool value)
        {
            if (systemTrayEnabled && !allowVisible && !this.IsHandleCreated)
            {
                base.SetVisibleCore(false);
                return;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (OperationGate.IsBusy && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                AppDialog.Show($"Exoptimizer is busy ({OperationGate.Current}). Please wait for it to finish before closing, so your system isn't left half-changed.",
                    "Operation in progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (systemTrayEnabled)
            {
                e.Cancel = true;
                allowVisible = false;
                this.Hide();
                this.ShowInTaskbar = false;
                if (notifyIcon != null)
                {
                    notifyIcon.ShowBalloonTip(2000, "Exoptimizer", "Application minimized to tray", ToolTipIcon.Info);
                }
            }
            else
            {
                try
                {
                    monitorTimer?.Stop();
                    monitorTimer?.Dispose();
                    
                    if (notifyIcon != null)
                    {
                        notifyIcon.Visible = false;
                        notifyIcon.Dispose();
                    }
                }
                catch { }
                
                base.OnFormClosing(e);
            }
        }

        private static double GetCpuUsage()
        {
            try
            {
                using var pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                pc.NextValue();
                System.Threading.Thread.Sleep(100);
                return pc.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                monitorTimer?.Stop();
                monitorTimer?.Dispose();
                
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = false;
                    notifyIcon.Dispose();
                }
            }
            catch { }
            
            base.OnFormClosed(e);
        }

        // Event handlers and optimization methods
        private void CheckAdminRights()
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    AppDialog.Show(
                        "Exoptimizer requires administrator privileges." + Environment.NewLine + Environment.NewLine + "Please restart as administrator.",
                        "Administrator Rights Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                AppDialog.Show($"Failed to check administrator rights: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
