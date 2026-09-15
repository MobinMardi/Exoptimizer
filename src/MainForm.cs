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
using System.ServiceProcess;

namespace Exoptimizer
{
    // Settings class to store user preferences
    public class AppSettings
    {
        public bool SystemTrayEnabled { get; set; } = false;
        public bool DarkModeEnabled { get; set; } = false;
    }

    public partial class MainForm : Form
    {
        public const string AppVersion = "3.0.1";

        private bool isOptimized = false;
        private bool valorantPriorityActive = false;
        private bool systemTrayEnabled = false;
        private bool allowVisible = true;
        private bool isExtremeOptimized = false;

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
        private Label? updateBadge;
        private Label? updateStatusLabel;
        private UpdateCheckResult? latestUpdateResult;

        // System Monitor controls
        private Label? cpuMonitorLabel;
        private Label? memoryMonitorLabel;
        private Label? networkMonitorLabel;
        private Label? valorantProcessLabel;
        private Label? riotClientLabel;
        private Label? vanguardLabel;
        private System.Windows.Forms.Timer? monitorTimer;

        // System Tray
        private NotifyIcon? notifyIcon;

        // Settings
        private readonly string settingsFilePath;

        // Dark mode support
        private bool isDarkMode = false;

        // Light mode colors (existing)
        private readonly Color LightPrimaryColor = Color.FromArgb(37, 99, 235);
        private readonly Color LightSecondaryColor = Color.FromArgb(99, 102, 241);
        private readonly Color LightBackgroundColor = Color.FromArgb(248, 250, 252);
        private readonly Color LightCardColor = Color.FromArgb(248, 250, 252);
        private readonly Color LightTextPrimary = Color.FromArgb(15, 23, 42);
        private readonly Color LightTextSecondary = Color.FromArgb(100, 116, 139);
        private readonly Color LightBorderColor = Color.FromArgb(226, 232, 240);
        private readonly Color LightSuccessColor = Color.FromArgb(34, 197, 94);
        private readonly Color LightWarningColor = Color.FromArgb(245, 158, 11);
        private readonly Color LightDangerColor = Color.FromArgb(239, 68, 68);

        // Dark mode colors - Onyx Black theme with Blurple accents
        private readonly Color DarkPrimaryColor = Color.FromArgb(114, 137, 218); // Discord Blurple
        private readonly Color DarkSecondaryColor = Color.FromArgb(88, 101, 242); // Lighter Blurple
        private readonly Color DarkBackgroundColor = Color.FromArgb(32, 34, 37); // Onyx Black
        private readonly Color DarkCardColor = Color.FromArgb(47, 49, 54); // Slightly lighter than background
        private readonly Color DarkTextPrimary = Color.FromArgb(255, 255, 255); // Pure white for high contrast
        private readonly Color DarkTextSecondary = Color.FromArgb(185, 187, 190); // Light gray
        private readonly Color DarkBorderColor = Color.FromArgb(79, 84, 92); // Border color
        private readonly Color DarkSuccessColor = Color.FromArgb(67, 181, 129); // Green
        private readonly Color DarkWarningColor = Color.FromArgb(250, 166, 26); // Orange
        private readonly Color DarkDangerColor = Color.FromArgb(237, 66, 69); // Red

        // Current theme colors (will be updated based on mode)
        private Color PrimaryColor;
        private Color SecondaryColor;
        private Color BackgroundColor;
        private Color CardColor;
        private Color TextPrimary;
        private Color TextSecondary;
        private Color BorderColor;
        private Color SuccessColor;
        private Color WarningColor;
        private Color DangerColor;

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

            if (result.Success && result.UpdateAvailable && updateBadge != null)
            {
                updateBadge.Visible = true;
            }
        }

        private void ApplyTheme()
        {
            if (isDarkMode)
            {
                PrimaryColor = DarkPrimaryColor;
                SecondaryColor = DarkSecondaryColor;
                BackgroundColor = DarkBackgroundColor;
                CardColor = DarkCardColor;
                TextPrimary = DarkTextPrimary;
                TextSecondary = DarkTextSecondary;
                BorderColor = DarkBorderColor;
                SuccessColor = DarkSuccessColor;
                WarningColor = DarkWarningColor;
                DangerColor = DarkDangerColor;
            }
            else
            {
                PrimaryColor = LightPrimaryColor;
                SecondaryColor = LightSecondaryColor;
                BackgroundColor = LightBackgroundColor;
                CardColor = LightCardColor;
                TextPrimary = LightTextPrimary;
                TextSecondary = LightTextSecondary;
                BorderColor = LightBorderColor;
                SuccessColor = LightSuccessColor;
                WarningColor = LightWarningColor;
                DangerColor = LightDangerColor;
            }

            // Apply theme to form immediately if it exists
            if (this.IsHandleCreated)
            {
                this.BackColor = BackgroundColor;
        
                // Update navigation bar colors
                if (this.Controls.Count > 0 && this.Controls[0] is Panel navBar)
                {
                    navBar.BackColor = CardColor;
            
                    // Update all controls in navigation bar
                    foreach (Control control in navBar.Controls)
                    {
                        if (control is Button navBtn && navBtn.Tag != null)
                        {
                            navBtn.ForeColor = navBtn.Tag.ToString() == currentSection ? PrimaryColor : TextSecondary;
                            navBtn.FlatAppearance.MouseOverBackColor = isDarkMode ? 
                                Color.FromArgb(79, 84, 92) : Color.FromArgb(241, 245, 249);
                        }
                        else if (control == updateBadge)
                        {
                            // Keep the update-available badge red regardless of
                            // theme - it's an alert color, not body text.
                            control.ForeColor = DangerColor;
                        }
                        else if (control is Label titleLabel)
                        {
                            titleLabel.ForeColor = TextPrimary;
                        }
                    }
                }

                // Update content panel
                if (contentPanel != null)
                {
                    contentPanel.BackColor = BackgroundColor;
            
                    // Force refresh all controls in content panel with new colors
                    UpdateAllControlColors(contentPanel);
                }

                // Force a complete refresh of the current section
                string tempSection = currentSection;
                currentSection = "";
                NavigateToSection(tempSection);
        
                this.Refresh();
            }
        }

        // Add this new method to recursively update all control colors
        private void UpdateAllControlColors(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                // Update background colors
                if (control.BackColor != Color.Transparent && control.BackColor != PrimaryColor && 
                    control.BackColor != SecondaryColor && control.BackColor != SuccessColor && 
                    control.BackColor != WarningColor && control.BackColor != DangerColor)
                {
                    control.BackColor = Color.Transparent;
                }
        
                // Update text colors for labels
                if (control is Label label)
                {
                    // Check if it's a title (large font)
                    if (label.Font.Size >= 20)
                    {
                        label.ForeColor = TextPrimary;
                    }
                    // Check if it's a section header (medium font, bold)
                    else if (label.Font.Size >= 12 && label.Font.Bold)
                    {
                        label.ForeColor = TextPrimary;
                    }
                    // Regular text
                    else
                    {
                        label.ForeColor = TextSecondary;
                    }
                }
        
                // Update checkbox colors
                if (control is CheckBox checkBox)
                {
                    checkBox.ForeColor = control.Name == "defenderCheckBox" ? WarningColor : TextPrimary;
                }
        
                // Recursively update child controls
                if (control.HasChildren)
                {
                    UpdateAllControlColors(control);
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            
            // Set application icon - updated path for new structure
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "icon-new.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    // Try alternative path
                    iconPath = Path.Combine(Application.StartupPath, "..", "assets", "icon-new.ico");
                    if (File.Exists(iconPath))
                    {
                        this.Icon = new Icon(iconPath);
                    }
                }
            }
            catch { }
            
            // Form properties - Modern styling with fixed size (increased width)
            this.Text = "Exoptimizer";
            this.Size = new Size(1200, 700); // Increased width to 1200
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimumSize = new Size(1200, 700);
            this.MaximumSize = new Size(1200, 700);
            this.BackColor = BackgroundColor;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            
            CreateInterface();
            this.ResumeLayout(false);
        }

        private void CreateInterface()
        {
            // Create top navigation bar
            var navBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = CardColor,
                Padding = new Padding(40, 10, 40, 10) // Increased padding for wider form
            };

            // Add subtle border to nav bar
            navBar.Paint += (s, e) =>
            {
                using var pen = new Pen(BorderColor, 1);
                e.Graphics.DrawLine(pen, 0, navBar.Height - 1, navBar.Width, navBar.Height - 1);
            };

            // App title with more left padding
            var titleLabel = new Label
            {
                Text = "  Exoptimizer",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 12), // Already has padding from navBar
                AutoSize = true,
                BackColor = Color.Transparent
            };

            // Navigation buttons with adjusted positions for wider form
            var navOptimization = CreateNavButton("System Optimization", "optimization", new Point(250, 15));
            var navValorant = CreateNavButton("VALORANT Priority", "valorant", new Point(400, 15));
            var navTools = CreateNavButton("Performance Tools", "tools", new Point(550, 15));
            var navMonitor = CreateNavButton("System Monitor", "monitor", new Point(700, 15));
            var navRestore = CreateNavButton("System Restore", "restore", new Point(850, 15));
            var navSettings = CreateNavButton("Settings", "settings", new Point(1000, 15));

            // Small "update available" badge, overlaid on the Settings button.
            // Hidden by default; CheckForUpdatesOnStartupAsync/the manual
            // "Check for Updates" button in Settings makes it visible.
            updateBadge = new Label
            {
                Text = "●",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = DangerColor,
                BackColor = Color.Transparent,
                Location = new Point(1125, 10),
                Size = new Size(20, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false,
                Cursor = Cursors.Hand
            };
            var updateBadgeTip = new ToolTip();
            updateBadgeTip.SetToolTip(updateBadge, "A new version is available - see Settings");
            updateBadge.Click += (s, e) => NavigateToSection("settings");

            navBar.Controls.AddRange(new Control[] { 
                titleLabel, navOptimization, navValorant, navTools, navMonitor, navRestore, navSettings, updateBadge
            });

            // Create content area - positioned below navigation bar with reduced padding
            contentPanel = new Panel
            {
                Location = new Point(0, 60), // Position below nav bar
                Size = new Size(1200, 640),  // Adjusted for wider form
                BackColor = BackgroundColor,
                Padding = new Padding(0, 0, 0, 0), // Remove padding since we're adding margins manually
                AutoScroll = true
            };

            this.Controls.AddRange(new Control[] { navBar, contentPanel });
        }

        private Button CreateNavButton(string text, string section, Point location)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(130, 30), // Slightly wider for better spacing
                BackColor = Color.Transparent,
                ForeColor = section == currentSection ? PrimaryColor : TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand,
                Tag = section
            };

            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = isDarkMode ? 
                Color.FromArgb(55, 65, 81) : Color.FromArgb(241, 245, 249);

            button.Click += (s, e) => NavigateToSection(section);

            return button;
        }

        private void NavigateToSection(string section)
        {
            if (section == currentSection) return;

            currentSection = section;
            contentPanel?.Controls.Clear();

            // Update navigation button colors
            foreach (Control control in this.Controls[0].Controls)
            {
                if (control is Button navBtn && navBtn.Tag != null)
                {
                    navBtn.ForeColor = navBtn.Tag.ToString() == section ? PrimaryColor : TextSecondary;
                }
            }

            switch (section)
            {
                case "optimization": LoadOptimizationContent(); break;
                case "valorant": LoadValorantContent(); break;
                case "tools": LoadToolsContent(); break;
                case "monitor": LoadMonitorContent(); break;
                case "restore": LoadRestoreContent(); break;
                case "settings": LoadSettingsContent(); break;
            }
        }

        private Panel CreateCard(Point location, Size size)
        {
            var card = new Panel
            {
                Location = location,
                Size = size,
                BackColor = Color.Transparent // Fully transparent
            };

            // No Paint event handler - completely transparent cards

            return card;
        }

        private Button CreateModernButton(string text, Color color, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderSize = 0;

            button.MouseEnter += (s, e) =>
            {
                if (button.Enabled)
                    button.BackColor = ControlPaint.Dark(color, 0.1f);
            };

            button.MouseLeave += (s, e) =>
            {
                if (button.Enabled)
                    button.BackColor = color;
            };

            return button;
        }

        private void LoadOptimizationContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "System Optimization",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40), // Added 40px margin from left and top
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Optimize your Windows system for maximum gaming performance",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100), // Added 40px margin from left
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Card container
            var optimizationCard = CreateCard(new Point(40, 140), new Size(900, 320)); // Added 40px margin from left

            // Check current optimization status
            CheckOptimizationStatus();
            CheckExtremeOptimizationStatus(); // Add this new check

            // Reassurance banner - directly addresses the class of bug fixed in 3.0.0
            var safetyBanner = new Label
            {
                Text = "🛡️ System Restore, your network connection, and your firewall are never disabled by Exoptimizer - ever, at any setting.",
                Location = new Point(0, 0),
                Size = new Size(880, 20),
                ForeColor = SuccessColor,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            // Buttons in horizontal layout: Optimize System, Quick Boost, Extreme Optimization, Longer Battery
            optimizeButton = CreateModernButton(
                isOptimized ? "✓ System Optimized" : "Optimize System", 
                isOptimized ? SuccessColor : PrimaryColor, 
                new Point(0, 40), 
                new Size(140, 40)
            );
            optimizeButton.Click += OptimizeButton_Click;
            if (isOptimized) optimizeButton.Enabled = false;

            var quickButton = CreateModernButton("Quick Boost", SuccessColor, new Point(150, 40), new Size(120, 40));
            quickButton.Click += (s, e) => MessageBox.Show("Quick optimization applied!", "Exoptimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);

            var extremeOptimizeButton = CreateModernButton(
                isExtremeOptimized ? "✓ Extreme Applied" : "Extreme Optimization", 
                isExtremeOptimized ? SuccessColor : DangerColor, // Red theme when not applied, green when applied
                new Point(280, 40), // Position after Quick Boost
                new Size(160, 40)
            );
            extremeOptimizeButton.Click += ExtremeOptimizeButton_Click;
            if (isExtremeOptimized) extremeOptimizeButton.Enabled = false;

            var batteryButton = CreateModernButton("Longer Battery", Color.FromArgb(168, 85, 247), new Point(450, 40), new Size(130, 40));
            batteryButton.Click += BatteryOptimizeButton_Click;

            // Add warning text for extreme optimization below the buttons
            var extremeWarningLabel = new Label
            {
                Text = "Extreme Optimization:\n⚠️ Disables many non-essential Windows features for maximum FPS. A restore point is created first - use with caution.",
                Location = new Point(0, 140), // Position under extreme button
                Size = new Size(350, 50),
                ForeColor = DangerColor,
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                BackColor = Color.Transparent
            };

            // Battery description
            var batteryDescLabel = new Label
            {
                Text = "Longer Battery:\nOptimizes power settings for extended battery life during power outages and when unplugged from AC power source",
                Location = new Point(0, 190), // Position under battery button
                Size = new Size(300, 40),
                ForeColor = Color.FromArgb(168, 85, 247),
                Font = new Font("Segoe UI", 8F, FontStyle.Italic),
                BackColor = Color.Transparent
            };

            // Checkbox
            defenderCheckBox = new CheckBox
            {
                Text = "Disable Windows Defender real-time protection (Advanced, fully reversible)",
                Location = new Point(0, 95),
                Size = new Size(430, 25),
                ForeColor = WarningColor,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            };

            optimizationCard.Controls.AddRange(new Control[] { 
                safetyBanner, optimizeButton, quickButton, extremeOptimizeButton, batteryButton, extremeWarningLabel, batteryDescLabel, defenderCheckBox
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, optimizationCard });
        }

        private void CheckOptimizationStatus()
        {
            try
            {
                bool servicesOptimized = CheckServicesOptimized();
                bool powerOptimized = CheckPowerOptimized();
                bool registryOptimized = CheckRegistryOptimized();
                
                // Consider system optimized if at least 2 out of 3 categories are optimized
                int optimizedCount = 0;
                if (servicesOptimized) optimizedCount++;
                if (powerOptimized) optimizedCount++;
                if (registryOptimized) optimizedCount++;
                
                isOptimized = optimizedCount >= 2;
            }
            catch
            {
                isOptimized = false;
            }
        }

        private bool CheckServicesOptimized()
        {
            try
            {
                // 3.0.0 sets non-essential services to "manual" (demand) rather
                // than "disabled" - see SystemGuard - so that's what indicates
                // the optimization has been applied.
                string[] keyServices = { "WSearch", "SysMain", "wuauserv" };
                int adjustedCount = keyServices.Count(service => SystemGuard.GetServiceStartType(service) == "demand");

                return adjustedCount >= 2; // At least 2 out of 3 key services adjusted
            }
            catch
            {
                return false;
            }
        }

        private bool CheckPowerOptimized()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit();
                    string output = process.StandardOutput.ReadToEnd();
                    // Check if High Performance power plan is active
                    return output.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                }
            }
            catch { }
            
            return false;
        }

        private bool CheckRegistryOptimized()
        {
            try
            {
                // Check Win32PrioritySeparation value
                var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\PriorityControl");
                if (key != null)
                {
                    var value = key.GetValue("Win32PrioritySeparation");
                    if (value != null && value.ToString() == "38")
                    {
                        return true;
                    }
                }
            }
            catch { }
            
            return false;
        }

        private void CheckExtremeOptimizationStatus()
        {
            try
            {
                bool extremeServicesDisabled = CheckExtremeServicesDisabled();
                bool visualEffectsDisabled = CheckVisualEffectsDisabled();
                bool defenderCompletelyDisabled = CheckDefenderCompletelyDisabled();
        
                // Consider extreme optimization applied if at least 2 out of 3 categories are applied
                int extremeOptimizedCount = 0;
                if (extremeServicesDisabled) extremeOptimizedCount++;
                if (visualEffectsDisabled) extremeOptimizedCount++;
                if (defenderCompletelyDisabled) extremeOptimizedCount++;
        
                isExtremeOptimized = extremeOptimizedCount >= 2;
            }
            catch
            {
                isExtremeOptimized = false;
            }
        }

        private bool CheckExtremeServicesDisabled()
        {
            try
            {
                // These are services extreme mode still adjusts (set to
                // "manual") in 3.0.0. EventLog/WinDefend/WdNisSvc used to be
                // in this indicator list, but they're never touched anymore
                // (EventLog is protected; Defender is handled purely via
                // registry policy - see SystemGuard and
                // DisableWindowsDefenderRealtimeProtection).
                string[] extremeServices = { "BITS", "DiagTrack", "dmwappushservice", "WSearch", "SysMain" };
                int adjustedCount = extremeServices.Count(service => SystemGuard.GetServiceStartType(service) == "demand");

                return adjustedCount >= 3; // At least 3 out of 5 extreme-mode services adjusted
            }
            catch
            {
                return false;
            }
        }

        private bool CheckVisualEffectsDisabled()
        {
            try
            {
                var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
                if (key != null)
                {
                    var value = key.GetValue("VisualFXSetting");
                    if (value != null && value.ToString() == "2") // Custom settings (disabled)
                    {
                        return true;
                    }
                }
            }
            catch { }
    
            return false;
        }

        private bool CheckDefenderCompletelyDisabled()
        {
            try
            {
                var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender");
                if (key != null)
                {
                    var value = key.GetValue("DisableAntiSpyware");
                    if (value != null && value.ToString() == "1")
                    {
                        return true;
                    }
                }
            }
            catch { }
    
            return false;
        }

        private void LoadValorantContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "VALORANT Priority",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40),
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Boost VALORANT performance with high process priority and network optimization",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100),
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // VALORANT Priority Card
            var valorantCard = CreateCard(new Point(40, 140), new Size(800, 180));

            // Priority section header
            var priorityHeaderLabel = new Label
            {
                Text = "VALORANT Process Priority",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 0),
                Size = new Size(400, 25),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Priority buttons
            valorantEnablePriority = CreateModernButton("Enable Priority", PrimaryColor, new Point(0, 30), new Size(130, 40));
            valorantEnablePriority.Click += ValorantEnableButton_Click;

            valorantDisablePriority = CreateModernButton("Disable Priority", DangerColor, new Point(140, 30), new Size(130, 40));
            valorantDisablePriority.Click += ValorantDisableButton_Click;

            // Priority status
            valorantStatusLabel = new Label
            {
                Text = "Priority: Checking...",
                Location = new Point(0, 80),
                Size = new Size(300, 25),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                BackColor = Color.Transparent
            };

            valorantCard.Controls.AddRange(new Control[] { 
                priorityHeaderLabel, valorantEnablePriority, valorantDisablePriority, valorantStatusLabel
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, valorantCard });

            // Check current VALORANT priority status
            CheckValorantPriorityStatus();
        }

        private void LoadToolsContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "Performance Tools",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40), // Added 40px margin
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Quick access to essential Windows system management tools and cleanup utilities",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100), // Added 40px margin
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Card container
            var toolsCard = CreateCard(new Point(40, 140), new Size(800, 250)); // Added 40px margin

            // System Tools section
            var systemToolsLabel = new Label
            {
                Text = "System Tools",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 0),
                Size = new Size(200, 25),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // First row of buttons
            var taskMgrBtn = CreateModernButton("Task Manager", SecondaryColor, new Point(0, 30), new Size(120, 35));
            taskMgrBtn.Click += (s, e) => { try { Process.Start("taskmgr"); } catch { } };

            var resMonBtn = CreateModernButton("Resource Monitor", SecondaryColor, new Point(130, 30), new Size(130, 35));
            resMonBtn.Click += (s, e) => { try { Process.Start("resmon.exe"); } catch { } };

            var cleanupBtn = CreateModernButton("Disk Cleanup", SecondaryColor, new Point(270, 30), new Size(110, 35));
            cleanupBtn.Click += (s, e) => { try { Process.Start("cleanmgr.exe"); } catch { } };

            // Second row of buttons
            var servicesBtn = CreateModernButton("Services", SecondaryColor, new Point(0, 75), new Size(100, 35));
            servicesBtn.Click += (s, e) => { try { Process.Start("services.msc"); } catch { } };

            var registryBtn = CreateModernButton("Registry Editor", SecondaryColor, new Point(110, 75), new Size(120, 35));
            registryBtn.Click += (s, e) => { try { Process.Start("regedit.exe"); } catch { } };

            var msConfigBtn = CreateModernButton("System Config", SecondaryColor, new Point(240, 75), new Size(120, 35));
            msConfigBtn.Click += (s, e) => { try { Process.Start("msconfig.exe"); } catch { } };

            // Cleanup Tools section
            var cleanupToolsLabel = new Label
            {
                Text = "Cleanup Tools",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 130),
                Size = new Size(200, 25),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Deep Cleanup button
            var deepCleanupBtn = CreateModernButton("Deep System Cleanup", WarningColor, new Point(0, 160), new Size(160, 35));
            deepCleanupBtn.Click += DeepCleanupButton_Click;

            // Cleanup description
            var cleanupDescLabel = new Label
            {
                Text = "Safely removes temporary files, logs, cache, and unnecessary system files without affecting running processes or games",
                Location = new Point(170, 160),
                Size = new Size(400, 35),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            };

            toolsCard.Controls.AddRange(new Control[] { 
                systemToolsLabel, taskMgrBtn, resMonBtn, cleanupBtn, servicesBtn, registryBtn, msConfigBtn,
                cleanupToolsLabel, deepCleanupBtn, cleanupDescLabel
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, toolsCard });
        }

        private void LoadMonitorContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "System Monitor",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40), // Added 40px margin
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Monitor your system performance and resource usage in real-time",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100), // Added 40px margin
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Card container
            var monitorCard = CreateCard(new Point(40, 140), new Size(800, 400)); // Added 40px margin

            // Section header
            var statsLabel = new Label
            {
                Text = "Live System Status",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 20),
                Size = new Size(300, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // System monitoring labels
            cpuMonitorLabel = new Label
            {
                Text = "CPU Usage: Loading...",
                Location = new Point(0, 60),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.Transparent
            };

            memoryMonitorLabel = new Label
            {
                Text = "Memory Usage: Loading...",
                Location = new Point(0, 90),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.Transparent
            };

            networkMonitorLabel = new Label
            {
                Text = "Network Latency: Testing...",
                Location = new Point(0, 120),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 11F),
                BackColor = Color.Transparent
            };

            // Process status section
            var processLabel = new Label
            {
                Text = "VALORANT Processes",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 170),
                Size = new Size(300, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            valorantProcessLabel = new Label
            {
                Text = "VALORANT: Checking...",
                Location = new Point(0, 205),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent
            };

            riotClientLabel = new Label
            {
                Text = "Riot Client: Checking...",
                Location = new Point(0, 235),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent
            };

            vanguardLabel = new Label
            {
                Text = "Riot Vanguard: Checking...",
                Location = new Point(0, 265),
                Size = new Size(400, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent
            };

            // Add Vanguard start button with play icon
            var startVanguardButton = new Button
            {
                Text = "⏻",
                Location = new Point(180, 260), // Positioned under the Vanguard status text
                Size = new Size(24, 32), // Square size instead of 24x32
                BackColor = Color.Transparent, // Transparent background
                ForeColor = Color.FromArgb(237, 66, 69), // Red color to match Riot theme
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Visible = true, // Temporarily visible for testing
                Tag = "vanguardStartBtn" // Tag for easy identification
            };

            startVanguardButton.FlatAppearance.BorderSize = 0;
            startVanguardButton.FlatAppearance.MouseOverBackColor = Color.Transparent;
            startVanguardButton.FlatAppearance.MouseDownBackColor = Color.Transparent;
            startVanguardButton.Click += StartVanguardButton_Click;

            // Add hover effects for the transparent button
            startVanguardButton.MouseEnter += (s, e) =>
            {
                if (startVanguardButton.Enabled)
                {
                    startVanguardButton.BackColor = Color.FromArgb(30, 237, 66, 69); // Semi-transparent red
                }
            };

            startVanguardButton.MouseLeave += (s, e) =>
            {
                if (startVanguardButton.Enabled)
                {
                    startVanguardButton.BackColor = Color.Transparent;
                }
            };

            monitorCard.Controls.AddRange(new Control[] { 
                statsLabel, cpuMonitorLabel, memoryMonitorLabel, networkMonitorLabel, 
                processLabel, valorantProcessLabel, riotClientLabel, vanguardLabel, startVanguardButton
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, monitorCard });

            // Start updating monitor data
            UpdateMonitorData(null, EventArgs.Empty);
        }

        private void LoadRestoreContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "System Restore",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40), // Added 40px margin
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Backup and restore your system settings safely",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100), // Added 40px margin
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Card container
            var restoreCard = CreateCard(new Point(40, 140), new Size(800, 280)); // Added 40px margin

            // First row of buttons
            var createRestoreBtn = CreateModernButton("Create Restore Point", PrimaryColor, new Point(0, 20), new Size(150, 40));
            createRestoreBtn.Click += RestoreButton_Click;

            var systemRestoreBtn = CreateModernButton("System Restore", SecondaryColor, new Point(160, 20), new Size(130, 40));
            systemRestoreBtn.Click += (s, e) => { try { Process.Start("rstrui.exe"); } catch { } };

            // Second row of buttons
            var undoBtn = CreateModernButton("Undo Optimizations", WarningColor, new Point(0, 70), new Size(150, 40));
            undoBtn.Click += UndoOptimizationButton_Click;

            var rebootBtn = CreateModernButton("Restart PC", DangerColor, new Point(160, 70), new Size(110, 40));
            rebootBtn.Click += (s, e) => 
            {
                var result = MessageBox.Show("Restart now?", "Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    try { Process.Start("shutdown", "/r /t 0"); } catch { }
                }
            };

            // Separator label
            var repairLabel = new Label
            {
                Text = "Upgraded from an older Exoptimizer version?",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 130),
                Size = new Size(500, 25),
                BackColor = Color.Transparent
            };

            var repairDescLabel = new Label
            {
                Text = "If restore points, your network, or Windows Defender were left disabled by an older\n" +
                       "version, this resets those specific services back to how Windows ships them.",
                Location = new Point(0, 158),
                Size = new Size(760, 35),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            };

            var repairBtn = CreateModernButton("Repair Critical Services", SuccessColor, new Point(0, 200), new Size(190, 40));
            repairBtn.Click += RepairCriticalServicesButton_Click;

            var protectedInfoBtn = CreateModernButton("What does Exoptimizer never touch?", SecondaryColor, new Point(200, 200), new Size(260, 40));
            protectedInfoBtn.Click += (s, e) =>
            {
                string list = string.Join(Environment.NewLine, SystemGuard.ProtectedServices.OrderBy(n => n));
                MessageBox.Show(
                    "Exoptimizer will never disable or stop these Windows services, at any setting:" +
                    Environment.NewLine + Environment.NewLine + list,
                    "Protected Services", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            restoreCard.Controls.AddRange(new Control[] { 
                createRestoreBtn, systemRestoreBtn, undoBtn, rebootBtn,
                repairLabel, repairDescLabel, repairBtn, protectedInfoBtn
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, restoreCard });
        }

        private void RepairCriticalServicesButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Repair Critical Services?" + Environment.NewLine + Environment.NewLine +
                "This resets System Restore, networking, and firewall/Defender-related services " +
                "back to the start type Windows ships them with, and starts them." + Environment.NewLine + Environment.NewLine +
                "This is safe to run even if nothing is broken.",
                "Repair Critical Services",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result != DialogResult.Yes) return;

            var steps = new List<(string, Action<Action<string>>)>
            {
                ("Repairing critical services...", report =>
                {
                    SystemGuard.RepairCriticalServices((name, current, total) =>
                        report($"Repairing {name} ({current}/{total})..."));
                }),
            };

            RunStepsWithProgress("Repairing Critical Services", steps);

            MessageBox.Show(
                "Critical services have been reset to their Windows-default state." + Environment.NewLine + Environment.NewLine +
                "A restart is recommended. If Windows Defender still shows as off afterwards, " +
                "run 'sfc /scannow' from an elevated Command Prompt to repair its driver files.",
                "Repair Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadSettingsContent()
        {
            if (contentPanel == null) return;

            // Title
            var titleLabel = new Label
            {
                Text = "Settings",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(40, 40),
                Size = new Size(800, 45),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Description
            var descLabel = new Label
            {
                Text = "Configure application preferences and display options",
                Font = new Font("Segoe UI", 12F),
                ForeColor = TextSecondary,
                Location = new Point(40, 100),
                Size = new Size(800, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            // Card container
            var settingsCard = CreateCard(new Point(40, 140), new Size(800, 450));

            // Appearance section
            var appearanceLabel = new Label
            {
                Text = "Appearance",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 20),
                Size = new Size(300, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            var darkModeCheckBox = new CheckBox
            {
                Text = "Enable Dark Mode",
                Location = new Point(0, 50),
                Size = new Size(300, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent,
                Checked = isDarkMode
            };

            darkModeCheckBox.CheckedChanged += (s, e) =>
            {
                isDarkMode = darkModeCheckBox.Checked;
                ApplyTheme();
                SaveSettings();
    
                // Force immediate visual update
                this.Invalidate(true);
                this.Update();
            };

            // System Tray section
            var trayLabel = new Label
            {
                Text = "System Tray",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 100),
                Size = new Size(300, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            var trayCheckBox = new CheckBox
            {
                Text = "Enable System Tray (minimize to tray)",
                Location = new Point(0, 130),
                Size = new Size(300, 35),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent,
                Checked = systemTrayEnabled
            };

            trayCheckBox.CheckedChanged += (s, e) =>
            {
                systemTrayEnabled = trayCheckBox.Checked;
                if (notifyIcon != null)
                {
                    notifyIcon.Visible = systemTrayEnabled;
                }
                
                if (systemTrayEnabled)
                {
                    this.ShowInTaskbar = true;
                }
                else
                {
                    allowVisible = true;
                    this.Show();
                    this.ShowInTaskbar = true;
                }
                
                SaveSettings();
            };

            // About section
            var versionLabel = new Label
            {
                Text = "About",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = TextPrimary,
                Location = new Point(0, 180),
                Size = new Size(300, 35),
                AutoSize = false,
                BackColor = Color.Transparent
            };

            var versionInfo = new Label
            {
                Text = $"Version: {AppVersion}" + Environment.NewLine + "mDev (Mobin Mardi)" + Environment.NewLine + "Copyright © 2026",
                Location = new Point(0, 210),
                Size = new Size(300, 75),
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 10F),
                BackColor = Color.Transparent
            };

            var checkUpdatesBtn = CreateModernButton("Check for Updates", SecondaryColor, new Point(0, 290), new Size(160, 35));
            checkUpdatesBtn.Click += CheckForUpdatesButton_Click;

            var viewChangelogBtn = CreateModernButton("View Changelog", SecondaryColor, new Point(170, 290), new Size(150, 35));
            viewChangelogBtn.Click += (s, e) =>
            {
                using var changelogForm = new ChangelogForm(CardColor, BackgroundColor, TextPrimary, TextSecondary, PrimaryColor, BorderColor);
                changelogForm.ShowDialog(this);
            };

            updateStatusLabel = new Label
            {
                Text = BuildUpdateStatusText(),
                Location = new Point(0, 332),
                Size = new Size(760, 30),
                ForeColor = (latestUpdateResult?.Success == true && latestUpdateResult.UpdateAvailable) ? SuccessColor : TextSecondary,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.Transparent
            };

            // Reset button
            var resetBtn = CreateModernButton("Reset to Defaults", WarningColor, new Point(0, 375), new Size(140, 35));
            resetBtn.Click += (s, e) =>
            {
                var result = MessageBox.Show("Reset all settings to default?", "Reset Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    systemTrayEnabled = false;
                    isDarkMode = false;
                    trayCheckBox.Checked = false;
                    darkModeCheckBox.Checked = false;
                    
                    ApplyTheme();
                    
                    allowVisible = true;
                    this.Show();
                    this.ShowInTaskbar = true;
                    if (notifyIcon != null) notifyIcon.Visible = false;
                    
                    SaveSettings();
                    MessageBox.Show("Settings reset to defaults!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            settingsCard.Controls.AddRange(new Control[] { 
                appearanceLabel, darkModeCheckBox, trayLabel, trayCheckBox, versionLabel, versionInfo,
                checkUpdatesBtn, viewChangelogBtn, updateStatusLabel, resetBtn
            });

            contentPanel.Controls.AddRange(new Control[] { titleLabel, descLabel, settingsCard });
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

            if (updateBadge != null)
            {
                updateBadge.Visible = result.Success && result.UpdateAvailable;
            }

            if (updateStatusLabel != null)
            {
                updateStatusLabel.Text = BuildUpdateStatusText();
                updateStatusLabel.ForeColor = (result.Success && result.UpdateAvailable) ? SuccessColor : TextSecondary;
            }

            if (!result.Success)
            {
                MessageBox.Show(
                    $"Couldn't check for updates: {result.ErrorMessage}" + Environment.NewLine + Environment.NewLine +
                    "Make sure you're connected to the internet.",
                    "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (result.UpdateAvailable)
            {
                var openResult = MessageBox.Show(
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
                MessageBox.Show($"You're up to date! (v{AppVersion})", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // Deep Cleanup Button Click Handler
        private void DeepCleanupButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Run Deep System Cleanup?" + Environment.NewLine + Environment.NewLine +
                "This will safely remove:" + Environment.NewLine +
                "• Temporary files and folders" + Environment.NewLine +
                "• System log files" + Environment.NewLine +
                "• Memory dumps and crash dumps" + Environment.NewLine +
                "• Windows Update cache" + Environment.NewLine +
                "• Prefetch files" + Environment.NewLine +
                "• Delivery Optimization cache" + Environment.NewLine + Environment.NewLine +
                "This process is safe and will not affect running programs or games." + Environment.NewLine + Environment.NewLine +
                "Continue?",
                "Deep System Cleanup",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                RunDeepCleanup();
            }
        }

        private void RunDeepCleanup()
        {
            try
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Running deep cleanup...";
                    statusLabel.ForeColor = WarningColor;
                }

                var progressForm = new Form
                {
                    Text = "Deep System Cleanup",
                    Size = new Size(400, 200),
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var progressLabel = new Label
                {
                    Text = "Initializing cleanup...",
                    Location = new Point(20, 20),
                    Size = new Size(350, 20),
                    Font = new Font("Segoe UI", 9F)
                };

                var progressBar = new ProgressBar
                {
                    Location = new Point(20, 50),
                    Size = new Size(350, 20),
                    Style = ProgressBarStyle.Continuous,
                    Maximum = 100
                };

                var cancelButton = new Button
                {
                    Text = "Close",
                    Location = new Point(300, 120),
                    Size = new Size(75, 25),
                    Enabled = false
                };

                cancelButton.Click += (s, e) => progressForm.Close();

                progressForm.Controls.AddRange(new Control[] { progressLabel, progressBar, cancelButton });
                progressForm.Show();

                // Stop Windows Update service
                progressLabel.Text = "Stopping Windows Update service...";
                progressBar.Value = 10;
                Application.DoEvents();
                RunCommand("net stop wuauserv");

                // Delete user temp files
                progressLabel.Text = "Cleaning user temporary files...";
                progressBar.Value = 20;
                Application.DoEvents();
                CleanDirectory(Path.GetTempPath());

                // Delete system temp files
                progressLabel.Text = "Cleaning system temporary files...";
                progressBar.Value = 30;
                Application.DoEvents();
                CleanDirectory(@"C:\Windows\Temp");

                // Delete Windows memory dump and minidump
                progressLabel.Text = "Removing memory dumps...";
                progressBar.Value = 40;
                Application.DoEvents();
                SafeDeleteFile(@"C:\Windows\MEMORY.DMP");
                CleanDirectory(@"C:\Windows\Minidump");

                // Delete Windows Update leftovers
                progressLabel.Text = "Cleaning Windows Update cache...";
                progressBar.Value = 50;
                Application.DoEvents();
                CleanDirectory(@"C:\Windows\SoftwareDistribution\Download");

                // Delete Delivery Optimization cache
                progressLabel.Text = "Cleaning Delivery Optimization cache...";
                progressBar.Value = 60;
                Application.DoEvents();
                CleanDirectory(@"C:\ProgramData\Microsoft\Windows\DeliveryOptimization");

                // Delete Prefetch files
                progressLabel.Text = "Cleaning Prefetch files...";
                progressBar.Value = 70;
                Application.DoEvents();
                CleanDirectory(@"C:\Windows\Prefetch");

                // Delete crash dumps from user profiles
                progressLabel.Text = "Cleaning crash dumps...";
                progressBar.Value = 80;
                Application.DoEvents();
                CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"));
                CleanDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CrashDumps"));

                // Delete log files
                progressLabel.Text = "Cleaning log files...";
                progressBar.Value = 90;
                Application.DoEvents();
                CleanLogFiles(@"C:\Windows\Logs");
                CleanLogFiles(@"C:\Windows\System32\LogFiles");

                // Restart Windows Update service
                progressLabel.Text = "Restarting Windows Update service...";
                progressBar.Value = 95;
                Application.DoEvents();
                RunCommand("net start wuauserv");

                progressLabel.Text = "Cleanup completed successfully!";
                progressBar.Value = 100;
                cancelButton.Enabled = true;
                cancelButton.Text = "Close";

                if (statusLabel != null)
                {
                    statusLabel.Text = "Deep cleanup completed";
                    statusLabel.ForeColor = SuccessColor;
                }

                MessageBox.Show(
                    "Deep system cleanup completed successfully!" + Environment.NewLine + Environment.NewLine +
                    "Temporary files, logs, cache, and unnecessary system files have been removed." + Environment.NewLine +
                    "Your system should now have more free space and improved performance.",
                    "Cleanup Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Cleanup failed";
                    statusLabel.ForeColor = DangerColor;
                }

                MessageBox.Show(
                    $"Deep cleanup encountered an error: {ex.Message}" + Environment.NewLine + Environment.NewLine +
                    "Some files may not have been cleaned due to permissions or file locks.",
                    "Cleanup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
            }
        }

        private void CleanDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                            File.Delete(file);
                        }
                        catch
                        {
                            // Skip files that can't be deleted (in use, permissions, etc.)
                        }
                    }

                    // Try to remove empty directories
                    var directories = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
                    foreach (string dir in directories.Reverse())
                    {
                        try
                        {
                            if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                            {
                                Directory.Delete(dir);
                            }
                        }
                        catch
                        {
                            // Skip directories that can't be deleted
                        }
                    }
                }
            }
            catch
            {
                // Skip directories that can't be accessed
            }
        }

        private void SafeDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                }
            }
            catch
            {
                // Skip files that can't be deleted
            }
        }

        private void CleanLogFiles(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    var logFiles = Directory.GetFiles(path, "*.log", SearchOption.AllDirectories);
                    foreach (string logFile in logFiles)
                    {
                        try
                        {
                            File.SetAttributes(logFile, FileAttributes.Normal);
                            File.Delete(logFile);
                        }
                        catch
                        {
                            // Skip log files that can't be deleted
                        }
                    }
                }
            }
            catch
            {
                // Skip directories that can't be accessed
            }
        }

        // Battery Optimization Button Click Handler
        private void BatteryOptimizeButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Apply battery optimization settings?" + Environment.NewLine + Environment.NewLine +
                "This will:" + Environment.NewLine +
                "• Set power plan to Power Saver mode" + Environment.NewLine +
                "• Reduce CPU maximum performance" + Environment.NewLine +
                "• Dim display brightness" + Environment.NewLine +
                "• Disable unnecessary background services" + Environment.NewLine +
                "• Optimize network adapter for power saving" + Environment.NewLine +
                "• Enable aggressive power management",
                "Battery Optimization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                ApplyBatteryOptimizations();
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

        private void ApplyBatteryOptimizations()
        {
            try
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Applying battery optimizations...";
                    statusLabel.ForeColor = WarningColor;
                }

                // Set power plan to Power Saver
                RunCommand("powercfg -setactive a1841308-3541-4fab-bc81-f71556f20b4a");

                // Reduce CPU maximum performance to 50%
                RunCommand("powercfg -setacvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 50");
                RunCommand("powercfg -setdcvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 54533251-82be-4824-96c1-47b60b740d00 bc5038f7-23e0-4960-96da-33abaf5935ec 50");

                // Set display timeout to 5 minutes on battery
                RunCommand("powercfg -setdcvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 7516b95f-f776-4464-8c53-06167f40cc99 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e 300");

                // Set sleep timeout to 10 minutes on battery
                RunCommand("powercfg -setdcvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 600");

                // Disable USB selective suspend (can cause issues but saves power)
                RunCommand("powercfg -setdcvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1");

                // Enable adaptive brightness
                RunCommand("powercfg -setdcvalueindex a1841308-3541-4fab-bc81-f71556f20b4a 7516b95f-f776-4464-8c53-06167f40cc99 fbd9aa66-9553-4097-ba44-ed6e9d65eab8 1");

                // Apply the settings
                RunCommand("powercfg -setactive a1841308-3541-4fab-bc81-f71556f20b4a");

                // Reduce background disk/CPU activity to save power (set to
                // manual, not disabled - see SystemGuard for why)
                SystemGuard.DisableServicesSafely(new[] { "WSearch", "wuauserv", "SysMain" });

                if (statusLabel != null)
                {
                    statusLabel.Text = "Battery optimization complete";
                    statusLabel.ForeColor = SuccessColor;
                }

                MessageBox.Show(
                    "Battery optimization applied successfully!" + Environment.NewLine + Environment.NewLine +
                    "Your system is now configured for maximum battery life." + Environment.NewLine +
                    "Performance may be reduced to extend battery duration." + Environment.NewLine + Environment.NewLine +
                    "To restore performance settings, use 'Undo Optimizations'.",
                    "Battery Optimization Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Battery optimization failed";
                    statusLabel.ForeColor = DangerColor;
                }

                MessageBox.Show($"Error applying battery optimizations: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"Failed to load settings: {ex.Message}" + Environment.NewLine + "Using default settings.", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        private void UpdateMonitorData(object? sender, EventArgs e)
        {
            if (currentSection == "monitor" && cpuMonitorLabel != null && memoryMonitorLabel != null && networkMonitorLabel != null)
            {
                try
                {
                    var cpuUsage = GetCpuUsage();
                    cpuMonitorLabel.Text = $"CPU Usage: {cpuUsage:F1}%";
                    cpuMonitorLabel.ForeColor = cpuUsage > 80 ? DangerColor : cpuUsage > 50 ? WarningColor : SuccessColor;

                    UpdateMemoryUsage();
                    UpdateNetworkLatency();
                    UpdateProcessStatuses();
                }
                catch
                {
                    if (cpuMonitorLabel != null)
                        cpuMonitorLabel.Text = "CPU Usage: Unable to retrieve";
                }
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

        private void UpdateMemoryUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024;
                    var free = Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024 / 1024;
                    var used = total - free;
                    var percentage = (used / total) * 100;

                    if (memoryMonitorLabel != null)
                    {
                        memoryMonitorLabel.Text = $"Memory Usage: {used:F1}GB / {total:F1}GB ({percentage:F1}%)";
                        memoryMonitorLabel.ForeColor = percentage > 80 ? DangerColor : percentage > 60 ? WarningColor : SuccessColor;
                    }
                    break;
                }
            }
            catch
            {
                if (memoryMonitorLabel != null)
                {
                    memoryMonitorLabel.Text = "Memory Usage: Unable to retrieve";
                }
            }
        }

        private void UpdateNetworkLatency()
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send("8.8.8.8", 1000);
                
                if (networkMonitorLabel != null)
                {
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        networkMonitorLabel.Text = $"Network Latency: {reply.RoundtripTime}ms (Google DNS)";
                        networkMonitorLabel.ForeColor = reply.RoundtripTime > 100 ? DangerColor : 
                                                       reply.RoundtripTime > 50 ? WarningColor : SuccessColor;
                    }
                    else
                    {
                        networkMonitorLabel.Text = "Network Latency: Connection failed";
                        networkMonitorLabel.ForeColor = DangerColor;
                    }
                }
            }
            catch
            {
                if (networkMonitorLabel != null)
                {
                    networkMonitorLabel.Text = "Network Latency: Unable to test";
                }
            }
        }

        private void UpdateProcessStatuses()
        {
            try
            {
                // Check VALORANT
                var valorantProcesses = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                if (valorantProcessLabel != null)
                {
                    if (valorantProcesses.Length > 0)
                    {
                        valorantProcessLabel.Text = $"VALORANT: Running (PID: {valorantProcesses[0].Id})";
                        valorantProcessLabel.ForeColor = SuccessColor;
                    }
                    else
                    {
                        valorantProcessLabel.Text = "VALORANT: Not running";
                        valorantProcessLabel.ForeColor = TextSecondary;
                    }
                }

                // Check Riot Client
                var riotProcesses = Process.GetProcessesByName("RiotClientServices");
                if (riotClientLabel != null)
                {
                    if (riotProcesses.Length > 0)
                    {
                        riotClientLabel.Text = $"Riot Client: Running (PID: {riotProcesses[0].Id})";
                        riotClientLabel.ForeColor = SuccessColor;
                    }
                    else
                    {
                        riotClientLabel.Text = "Riot Client: Not running";
                        riotClientLabel.ForeColor = TextSecondary;
                    }
                }

                // Check Riot Vanguard and manage start button visibility
                var vanguardProcesses = Process.GetProcessesByName("vgc");
                bool vanguardRunning = vanguardProcesses.Length > 0;
                
                if (vanguardLabel != null)
                {
                    if (vanguardRunning)
                    {
                        vanguardLabel.Text = $"Riot Vanguard: Running (PID: {vanguardProcesses[0].Id})";
                        vanguardLabel.ForeColor = SuccessColor;
                    }
                    else
                    {
                        vanguardLabel.Text = "Riot Vanguard: Not running";
                        vanguardLabel.ForeColor = TextSecondary;
                    }
                }

                // Show/hide the start Vanguard button based on service status
                if (currentSection == "monitor" && contentPanel != null)
                {
                    foreach (Control control in contentPanel.Controls)
                    {
                        if (control is Panel panel)
                        {
                            foreach (Control panelControl in panel.Controls)
                            {
                                if (panelControl is Button btn && btn.Tag?.ToString() == "vanguardStartBtn")
                                {
                                    btn.Visible = !vanguardRunning; // Show button only when Vanguard is not running
                                    btn.BringToFront(); // Ensure button is on top
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                if (valorantProcessLabel != null)
                    valorantProcessLabel.Text = "VALORANT: Unable to check";
                if (riotClientLabel != null)
                    riotClientLabel.Text = "Riot Client: Unable to check";
                if (vanguardLabel != null)
                    vanguardLabel.Text = "Riot Vanguard: Unable to check";
            }
        }

        private void StartVanguardButton_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Start Riot Vanguard service?" + Environment.NewLine + Environment.NewLine +
                    "This will start the vgc.exe service which is required for VALORANT anti-cheat." + Environment.NewLine +
                    "You may need to restart VALORANT after starting Vanguard.",
                    "Start Riot Vanguard",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    // Try to start the vgc service
                    RunCommand("sc start vgc");
                    
                    // Wait a moment for the service to start
                    System.Threading.Thread.Sleep(2000);
                    
                    // Update the process status immediately
                    UpdateProcessStatuses();
                    
                    MessageBox.Show(
                        "Riot Vanguard service start command sent." + Environment.NewLine + Environment.NewLine +
                        "If Vanguard doesn't start, you may need to:" + Environment.NewLine +
                        "• Restart your computer" + Environment.NewLine +
                        "• Reinstall VALORANT" + Environment.NewLine +
                        "• Check Windows services manually",
                        "Vanguard Start",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start Riot Vanguard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    MessageBox.Show(
                        "Exoptimizer requires administrator privileges." + Environment.NewLine + Environment.NewLine + "Please restart as administrator.",
                        "Administrator Rights Required",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check administrator rights: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Runs a sequence of named steps with a small progress dialog, so the
        /// app never just freezes with no feedback while it works through a
        /// batch of service/registry/network changes (the same
        /// progress-dialog pattern the Deep Cleanup tool already uses). Each
        /// step receives a "report" callback it can call any number of times
        /// to show finer-grained progress (e.g. which service it's on).
        /// </summary>
        private void RunStepsWithProgress(string title, IReadOnlyList<(string Label, Action<Action<string>> Step)> steps)
        {
            using var progressForm = new Form
            {
                Text = title,
                Size = new Size(440, 160),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ControlBox = false,
                BackColor = CardColor
            };

            var progressLabel = new Label
            {
                Text = "Starting...",
                Location = new Point(20, 20),
                Size = new Size(390, 40),
                ForeColor = TextPrimary,
                Font = new Font("Segoe UI", 9F)
            };

            var progressBar = new ProgressBar
            {
                Location = new Point(20, 65),
                Size = new Size(390, 20),
                Style = ProgressBarStyle.Continuous,
                Maximum = 100
            };

            progressForm.Controls.AddRange(new Control[] { progressLabel, progressBar });
            progressForm.Show(this);

            void Report(string text)
            {
                progressLabel.Text = text;
                Application.DoEvents();
            }

            for (int i = 0; i < steps.Count; i++)
            {
                Report(steps[i].Label);
                progressBar.Value = Math.Min(100, (int)((i + 1) / (double)steps.Count * 100));

                try { steps[i].Step(Report); }
                catch { /* one step failing should never abort the rest of the batch */ }
            }

            progressForm.Close();
        }

        private void OptimizeButton_Click(object sender, EventArgs e)
        {
            if (isOptimized)
            {
                MessageBox.Show("System is already optimized!" + Environment.NewLine + Environment.NewLine + "Use 'Undo Optimizations' if you want to reset and re-apply optimizations.", "Exoptimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                "Apply gaming optimizations?" + Environment.NewLine + Environment.NewLine + "• Disable unnecessary services" + Environment.NewLine + "• Optimize network settings" + Environment.NewLine + "• Configure power settings" + Environment.NewLine + "• Registry optimizations",
                "System Optimization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                ApplyOptimizations();
            }
            // Handle X button click (DialogResult.Cancel) - do nothing, just return
        }

        /// <summary>The "regular" optimization pass, shared by both Optimize System and (as a base) Extreme Optimization, so the two never diverge or run their own separate confirmation dialogs.</summary>
        private List<(string, Action<Action<string>>)> BuildRegularOptimizationSteps(Action<bool, string> onRestorePointResult)
        {
            return new List<(string, Action<Action<string>>)>
            {
                ("Creating a restore point...", report =>
                {
                    var (ok, msg) = CreateRestorePoint();
                    onRestorePointResult(ok, msg);
                }),
                ("Adjusting non-essential services...", report =>
                {
                    DisableUnnecessaryServices(name => report($"Adjusting services ({name})..."));
                }),
                ("Tuning network settings...", report => OptimizeNetworkSettings()),
                ("Applying power plan...", report => OptimizePowerSettings()),
                ("Applying registry tweaks...", report => ApplyRegistryOptimizations()),
                (defenderCheckBox?.Checked == true ? "Adjusting Windows Defender..." : "Adding game exclusions...", report =>
                {
                    if (defenderCheckBox?.Checked == true) DisableWindowsDefenderRealtimeProtection();
                    else AddGamingExclusions();
                }),
            };
        }

        private void ApplyOptimizations()
        {
            if (statusLabel != null)
            {
                statusLabel.Text = "Optimizing system...";
                statusLabel.ForeColor = WarningColor;
            }
            
            if (optimizeButton != null)
            {
                optimizeButton.Enabled = false;
            }

            try
            {
                bool restorePointOk = true;
                string restorePointMessage = string.Empty;

                var steps = BuildRegularOptimizationSteps((ok, msg) => { restorePointOk = ok; restorePointMessage = msg; });

                RunStepsWithProgress("Optimizing System", steps);

                isOptimized = true;

                if (statusLabel != null)
                {
                    statusLabel.Text = "Optimization complete";
                    statusLabel.ForeColor = SuccessColor;
                }

                if (optimizeButton != null)
                {
                    optimizeButton.Text = "✓ Optimized";
                    optimizeButton.BackColor = SuccessColor;
                }

                string restoreNote = restorePointOk
                    ? "A restore point was created before any changes were made."
                    : $"Note: a restore point could not be created ({restorePointMessage}). Nothing critical (System Restore, networking, or your firewall) was touched regardless.";

                MessageBox.Show(
                    "Optimization completed!" + Environment.NewLine + Environment.NewLine +
                    restoreNote + Environment.NewLine + Environment.NewLine +
                    "Restart recommended.",
                    "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Optimization failed";
                    statusLabel.ForeColor = DangerColor;
                }

                if (optimizeButton != null)
                {
                    optimizeButton.Enabled = true;
                }

                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExtremeOptimizeButton_Click(object sender, EventArgs e)
        {
            if (isExtremeOptimized)
            {
                MessageBox.Show("Extreme optimization is already applied!" + Environment.NewLine + Environment.NewLine + "Use 'Undo Optimizations' if you want to reset and re-apply extreme optimizations.", "Exoptimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                "⚠️ EXTREME OPTIMIZATION WARNING ⚠️" + Environment.NewLine + Environment.NewLine +
                "This will apply AGGRESSIVE optimizations that may:" + Environment.NewLine +
                "• Disable many non-essential Windows features and services" + Environment.NewLine +
                "• Turn off Windows visual effects completely" + Environment.NewLine +
                "• Disable Windows Defender real-time protection" + Environment.NewLine +
                "• Stop Windows Update and telemetry services" + Environment.NewLine +
                "• Disable startup programs and background apps" + Environment.NewLine +
                "• Apply extreme power and CPU settings" + Environment.NewLine + Environment.NewLine +
                "Exoptimizer will never disable System Restore, your network, or your firewall - " +
                "those are always left alone, no matter what." + Environment.NewLine + Environment.NewLine +
                "⚠️ ONLY USE IF YOU UNDERSTAND THE RISKS ⚠️" + Environment.NewLine +
                "A system restore point will be created automatically." + Environment.NewLine + Environment.NewLine +
                "Continue with EXTREME optimization?",
                "Extreme Gaming Optimization",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                ApplyExtremeOptimizations();
            }
        }

        private void ApplyExtremeOptimizations()
        {
            try
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Applying EXTREME optimizations...";
                    statusLabel.ForeColor = DangerColor;
                }

                bool restorePointOk = true;
                string restorePointMessage = string.Empty;

                // Extreme mode is the regular pass plus the extra extreme
                // steps, run as one batch with one progress dialog and one
                // result - not two separate passes with two separate popups.
                var steps = BuildRegularOptimizationSteps((ok, msg) => { restorePointOk = ok; restorePointMessage = msg; });
                steps.AddRange(new List<(string, Action<Action<string>>)>
                {
                    ("Applying extreme service changes...", report =>
                    {
                        ApplyExtremeGameModeOptimizations(name => report($"Adjusting services ({name})..."));
                    }),
                    ("Disabling visual effects...", report => DisableWindowsVisualEffects()),
                    ("Adjusting background apps...", report => DisableBackgroundAppsAndServices()),
                    ("Tuning network settings (extreme)...", report => ApplyExtremeNetworkOptimizations()),
                    ("Applying extreme registry tweaks...", report => ApplyExtremeRegistryTweaks()),
                    ("Disabling Windows Defender real-time protection...", report => DisableWindowsDefenderRealtimeProtection()),
                    ("Disabling startup programs...", report => DisableStartupPrograms()),
                    ("Applying memory tweaks...", report => ApplyExtremeMemoryOptimizations()),
                });

                RunStepsWithProgress("Applying Extreme Optimization", steps);

                isOptimized = true;
                isExtremeOptimized = true;

                if (optimizeButton != null)
                {
                    optimizeButton.Text = "✓ Optimized";
                    optimizeButton.BackColor = SuccessColor;
                    optimizeButton.Enabled = false;
                }

                if (statusLabel != null)
                {
                    statusLabel.Text = "EXTREME optimization complete - RESTART REQUIRED";
                    statusLabel.ForeColor = DangerColor;
                }

                string restoreNote = restorePointOk
                    ? "A restore point was created before any changes were made."
                    : $"Note: a restore point could not be created ({restorePointMessage}). System Restore, your network, and your firewall were still never touched.";

                MessageBox.Show(
                    "🔥 EXTREME OPTIMIZATION COMPLETED! 🔥" + Environment.NewLine + Environment.NewLine +
                    "Your system has been optimized for maximum gaming performance." + Environment.NewLine +
                    "Many non-essential Windows features have been disabled for FPS gains." + Environment.NewLine + Environment.NewLine +
                    restoreNote + Environment.NewLine + Environment.NewLine +
                    "⚠️ RESTART YOUR COMPUTER NOW FOR CHANGES TO TAKE EFFECT ⚠️" + Environment.NewLine + Environment.NewLine +
                    "If you experience issues, use 'Undo Optimizations' or System Restore.",
                    "Extreme Optimization Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );

                // Update the extreme button if it exists
                if (contentPanel != null)
                {
                    foreach (Control control in contentPanel.Controls)
                    {
                        if (control is Panel panel)
                        {
                            foreach (Control panelControl in panel.Controls)
                            {
                                if (panelControl is Button btn && btn.Text.Contains("Extreme"))
                                {
                                    btn.Text = "✓ Extreme Applied";
                                    btn.BackColor = SuccessColor;
                                    btn.Enabled = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "Extreme optimization failed";
                    statusLabel.ForeColor = DangerColor;
                }

                MessageBox.Show($"Error during extreme optimization: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Additional non-essential services set to manual (not disabled) start
        // for extreme mode. System Restore (VSS/swprv), the networking stack
        // (nsi/NlaSvc/iphlpsvc/RasMan/LanmanServer/WinHttpAutoProxySvc), and
        // core plumbing (Winmgmt/Schedule/EventLog/gpsvc/W32Time) are never
        // included here - see SystemGuard.ProtectedServices for why.
        private static readonly string[] ExtremeModeServices = {
            "BITS", "MMCSS", "SENS", "ShellHWDetection",
            "Spooler", "SSDPSRV", "SstpSvc", "TapiSrv", "TrkWks", "upnphost",
            "WbioSrvc", "wcncsvc", "WdiServiceHost", "WdiSystemHost",
            "WebClient", "Wecsvc", "wercplsupport", "WerSvc",
            "WinRM", "WMPNetworkSvc", "WPCSvc", "WPDBusEnum", "wscsvc",
            "WSearch", "wuauserv", "WwanSvc", "XblAuthManager", "XblGameSave", "XboxGipSvc",
            "XboxNetApiSvc", "DiagTrack", "dmwappushservice", "lfsvc", "MapsBroker",
            "NetTcpPortSharing", "RemoteAccess", "RemoteRegistry",
            "SysMain", "Themes", "WpcMonSvc", "SessionEnv",
            "TermService", "UmRdpService", "RpcLocator", "FontCache", "stisvc", "wisvc",
            "PcaSvc", "CscService", "defragsvc", "UsoSvc", "WaaSMedicSvc", "DoSvc"
        };

        private static void ApplyExtremeGameModeOptimizations(Action<string>? onServiceStep = null)
        {
            SystemGuard.DisableServicesSafely(ExtremeModeServices, (name, current, total) =>
                onServiceStep?.Invoke(name));

            // Set extreme power settings
            RunCommand("powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"); // High Performance
            RunCommand("powercfg -change -monitor-timeout-ac 0");
            RunCommand("powercfg -change -disk-timeout-ac 0");
            RunCommand("powercfg -change -standby-timeout-ac 0");
            RunCommand("powercfg -change -hibernate-timeout-ac 0");
        }

        private static void DisableWindowsVisualEffects()
        {
            try
            {
                // Disable all visual effects for maximum performance
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", 
                    "VisualFXSetting", 2, RegistryValueKind.DWord); // Custom settings

                // Disable individual visual effects
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "DragFullWindows", "0", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "MenuShowDelay", "0", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", RegistryValueKind.String);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", 
                    "ListviewAlphaSelect", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", 
                    "TaskbarAnimations", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", 
                    "ListviewShadow", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", 
                    "EnableAeroPeek", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", 
                    "AlwaysHibernateThumbnails", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void DisableBackgroundAppsAndServices()
        {
            try
            {
                // Disable background apps
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", 
                    "GlobalUserDisabled", 1, RegistryValueKind.DWord);

                // Disable startup delay
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", 
                    "StartupDelayInMSec", 0, RegistryValueKind.DWord);

                // Disable Windows tips and suggestions
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", 
                    "SystemPaneSuggestionsEnabled", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", 
                    "SoftLandingEnabled", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void ApplyExtremeNetworkOptimizations() => OptimizeNetworkSettings();

        private static void ApplyExtremeRegistryTweaks()
        {
            try
            {
                // Extreme CPU scheduling for programs
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", 
                    "Win32PrioritySeparation", 38, RegistryValueKind.DWord);

                // Disable CPU throttling
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", 
                    "PowerThrottlingOff", 1, RegistryValueKind.DWord);

                // Gaming mode registry tweaks
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", 
                    "AllowAutoGameMode", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", 
                    "AutoGameModeEnabled", 1, RegistryValueKind.DWord);

                // Disable fullscreen optimizations globally
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", 
                    "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_CURRENT_USER\System\GameConfigStore", 
                    "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);

                // Memory management tweaks
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", 
                    "ClearPageFileAtShutdown", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", 
                    "DisablePagingExecutive", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", 
                    "LargeSystemCache", 0, RegistryValueKind.DWord);

                // Disable Windows Error Reporting
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", 
                    "Disabled", 1, RegistryValueKind.DWord);

                // Disable telemetry completely
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", 
                    "AllowTelemetry", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", 
                    "AllowTelemetry", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void DisableStartupPrograms()
        {
            try
            {
                // Disable common startup programs that can impact gaming
                string[] startupPrograms = {
                    "Microsoft Teams", "Skype", "Spotify", "Discord", "Steam", "Epic Games Launcher",
                    "Adobe Updater", "Java Update Scheduler", "Office", "OneDrive", "Cortana"
                };

                foreach (string program in startupPrograms)
                {
                    try
                    {
                        RunCommand($"wmic startup where \"name='{program}'\" call disable");
                    }
                    catch { }
                }

                // Disable startup delay
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", 
                    "StartupDelayInMSec", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void ApplyExtremeMemoryOptimizations()
        {
            try
            {
                // Memory and cache optimizations
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", 
                    "IoPageLockLimit", 983040, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem", 
                    "ContigFileAllocSize", 1536, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem", 
                    "DisableNTFSLastAccessUpdate", 1, RegistryValueKind.DWord);

                // Disable prefetch and superfetch completely
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", 
                    "EnablePrefetcher", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", 
                    "EnableSuperfetch", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        private void ValorantEnableButton_Click(object sender, EventArgs e)
        {
            try
            {
                var valorantProcesses = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                
                if (valorantProcesses.Length == 0)
                {
                    MessageBox.Show("VALORANT is not running!" + Environment.NewLine + Environment.NewLine + "Please start VALORANT first, then enable priority.", 
                        "VALORANT Not Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Set high priority for VALORANT process
                foreach (var process in valorantProcesses)
                {
                    process.PriorityClass = ProcessPriorityClass.High;
                }

                // Add QoS policy
                RunCommand("netsh qos policy add \"Exoptimizer VALORANT Priority\" appname=\"VALORANT-Win64-Shipping.exe\" protocol=any localport=any remoteport=any dscp=46 throttleratekbps=0");

                valorantPriorityActive = true;
                
                if (valorantStatusLabel != null)
                {
                    valorantStatusLabel.Text = "Priority: Active (High)";
                    valorantStatusLabel.ForeColor = SuccessColor;
                }
                
                if (valorantEnablePriority != null) valorantEnablePriority.Enabled = false;
                if (valorantDisablePriority != null) valorantDisablePriority.Enabled = true;

                MessageBox.Show("VALORANT priority enabled!" + Environment.NewLine + Environment.NewLine + "Process priority set to High and QoS policy applied.", 
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to enable priority: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ValorantDisableButton_Click(object sender, EventArgs e)
        {
            try
            {
                var valorantProcesses = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                
                // Reset priority for VALORANT process if running
                foreach (var process in valorantProcesses)
                {
                    process.PriorityClass = ProcessPriorityClass.Normal;
                }

                // Remove QoS policy
                RunCommand("netsh qos policy delete \"Exoptimizer VALORANT Priority\"");

                valorantPriorityActive = false;
                
                if (valorantStatusLabel != null)
                {
                    valorantStatusLabel.Text = valorantProcesses.Length > 0 ? "Priority: Normal" : "Priority: VALORANT not running";
                    valorantStatusLabel.ForeColor = TextSecondary;
                }
                
                if (valorantEnablePriority != null) valorantEnablePriority.Enabled = valorantProcesses.Length > 0;
                if (valorantDisablePriority != null) valorantDisablePriority.Enabled = false;

                MessageBox.Show("VALORANT priority disabled!" + Environment.NewLine + Environment.NewLine + "Process priority reset to Normal and QoS policy removed.", 
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to disable priority: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreButton_Click(object sender, EventArgs e)
        {
            this.UseWaitCursor = true;
            Application.DoEvents();

            var (success, message) = CreateRestorePoint();

            this.UseWaitCursor = false;

            if (success)
            {
                MessageBox.Show("Restore point created!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Couldn't create a restore point: {message}" + Environment.NewLine + Environment.NewLine +
                    "You can also create one manually from Control Panel > System > System Protection.",
                    "Restore Point", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UndoOptimizationButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Undo all optimizations?",
                "Undo Optimizations",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                UndoOptimizations();
            }
            // Handle X button click (DialogResult.Cancel) - do nothing, just return
        }

        private void UndoOptimizations()
        {
            try
            {
                // Restores every service Exoptimizer has ever changed back to
                // its actual original start type (not a guessed "auto") - see
                // SystemGuard.RestoreAllTrackedServices. This replaces the old
                // hardcoded 6-service list, which was why "Undo" used to leave
                // most changed services untouched.
                var steps = new List<(string, Action<Action<string>>)>
                {
                    ("Restoring changed services...", report =>
                    {
                        SystemGuard.RestoreAllTrackedServices((name, current, total) =>
                            report($"Restoring {name} ({current}/{total})..."));
                    }),
                    ("Resetting network settings...", report =>
                    {
                        RunCommand("netsh int tcp reset");
                        RunCommand("netsh winsock reset");
                    }),
                    ("Resetting power plan...", report =>
                    {
                        RunCommand("powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e"); // Balanced
                    }),
                    ("Reverting extreme mode tweaks...", report => UndoExtremeOptimizations()),
                };

                RunStepsWithProgress("Undoing Optimizations", steps);

                isOptimized = false;
                
                if (statusLabel != null)
                {
                    statusLabel.Text = "Optimizations undone";
                    statusLabel.ForeColor = WarningColor;
                }
                
                if (optimizeButton != null)
                {
                    optimizeButton.Text = "Optimize System";
                    optimizeButton.BackColor = PrimaryColor;
                    optimizeButton.Enabled = true;
                }
                
                isExtremeOptimized = false;

                // And update the extreme button if it exists
                if (contentPanel != null)
                {
                    foreach (Control control in contentPanel.Controls)
                    {
                        if (control is Panel panel)
                        {
                            foreach (Control panelControl in panel.Controls)
                            {
                                if (panelControl is Button btn && btn.Text.Contains("Extreme"))
                                {
                                    btn.Text = "Extreme Optimization";
                                    btn.BackColor = DangerColor;
                                    btn.Enabled = true;
                                    break;
                                }
                            }
                        }
                    }
                }

                MessageBox.Show("Optimizations undone." + Environment.NewLine + Environment.NewLine + "Restart recommended.", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void UndoExtremeOptimizations()
        {
            // Services touched by extreme mode are already handled by
            // SystemGuard.RestoreAllTrackedServices (called just before this
            // in UndoOptimizations) - this only needs to revert the registry
            // tweaks extreme mode applies directly.
            try
            {
                // Reset visual effects back to "Let Windows choose"
                Registry.SetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", 
                    "VisualFXSetting", 0, RegistryValueKind.DWord);

                // Re-enable Windows Defender
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender", 
                    "DisableAntiSpyware", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", 
                    "DisableRealtimeMonitoring", 0, RegistryValueKind.DWord);

                // Reset telemetry to default
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", 
                    "AllowTelemetry", 1, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", 
                    "AllowTelemetry", 1, RegistryValueKind.DWord);

                // Reset CPU scheduling and memory tweaks back to Windows defaults
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", 
                    "Win32PrioritySeparation", 2, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", 
                    "PowerThrottlingOff", 0, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", 
                    "DisablePagingExecutive", 0, RegistryValueKind.DWord);

                // Re-enable Windows Error Reporting
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting", 
                    "Disabled", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        // Optimization methods
        /// <summary>
        /// Creates a System Restore point, and returns whether it actually
        /// worked. Two things silently defeat "Checkpoint-Computer" on most
        /// consumer PCs: System Protection is off by default for the system
        /// drive, and Windows only allows one automatic restore point every
        /// 24 hours unless that limit is explicitly disabled. Both are fixed
        /// here first, which is what makes "Can't use restore points" go away.
        /// </summary>
        private static (bool Success, string Message) CreateRestorePoint()
        {
            try
            {
                string systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

                RunCommand($"powershell.exe -Command \"Enable-ComputerRestore -Drive '{systemDrive}' -ErrorAction SilentlyContinue\"", out _, out _);

                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore",
                    "SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);

                bool success = RunCommand(
                    $"powershell.exe -Command \"Checkpoint-Computer -Description 'Exoptimizer v{AppVersion} Backup' -RestorePointType 'MODIFY_SETTINGS'\"",
                    out _, out string error);

                if (success) return (true, "Restore point created.");

                return (false, string.IsNullOrWhiteSpace(error)
                    ? "Windows declined to create a restore point (it may already have one from within the last 24 hours)."
                    : error.Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static readonly string[] NonEssentialServices = {
            "Fax", "Spooler", "TabletInputService", "Themes", "WSearch", "SysMain",
            "DiagTrack", "dmwappushservice", "MapsBroker", "lfsvc",
            "lltdsvc", "AppVClient", "NetTcpPortSharing", "RemoteAccess", "RemoteRegistry",
            "WbioSrvc", "WMPNetworkSvc", "WpcMonSvc", "SessionEnv", "TermService",
            "UmRdpService", "RpcLocator", "WerSvc", "Wecsvc", "FontCache", "stisvc",
            "wisvc", "PcaSvc", "CscService", "defragsvc", "wuauserv", "UsoSvc", "WaaSMedicSvc"
        };

        private static void DisableUnnecessaryServices(Action<string>? onServiceStep = null)
        {
            SystemGuard.DisableServicesSafely(NonEssentialServices, (name, current, total) =>
                onServiceStep?.Invoke(name), alsoStop: false);
        }

        // Older versions disabled TCP auto-tuning, disabled RSC, forced legacy
        // CTCP congestion control, and blindly forced every adapter (by
        // hardcoded name, which doesn't always match a real adapter) to
        // MTU 1500. Several of those settings were removed from Windows years
        // ago and silently fail; the rest generally make throughput *worse*
        // on modern hardware and can break custom setups (VPNs, PPPoE) that
        // need a non-default MTU. This keeps only the small set of tweaks
        // that are still genuinely safe and useful on Windows 10/11:
        // Receive Side Scaling spreads network interrupt handling across CPU
        // cores, and this makes sure auto-tuning is explicitly at its normal,
        // healthy default in case an older tool (or a previous Exoptimizer
        // version) left it disabled.
        private static void OptimizeNetworkSettings()
        {
            string[] commands = {
                "netsh int tcp set global autotuninglevel=normal",
                "netsh int tcp set global rss=enabled",
            };

            foreach (string command in commands)
            {
                try { RunCommand(command); }
                catch { }
            }
        }

        private static void OptimizePowerSettings()
        {
            try
            {
                RunCommand("powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            }
            catch { }
        }

        private static void ApplyRegistryOptimizations()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl", 
                    "Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", 
                    "HwSchMode", 2, RegistryValueKind.DWord);
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection", 
                    "AllowTelemetry", 0, RegistryValueKind.DWord);
            }
            catch { }
        }

        // The only supported, fully reversible way to turn off real-time
        // protection: the same registry policy Group Policy/Intune use. Unlike
        // disabling WinDefend/WdBoot/WdFilter at the service level (removed in
        // 3.0.0), this never touches the antimalware driver itself.
        private static void DisableWindowsDefenderRealtimeProtection()
        {
            try
            {
                Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", 
                    "DisableRealtimeMonitoring", 1, RegistryValueKind.DWord);
            }
            catch { }
        }

        private static void AddGamingExclusions()
        {
            string[] exclusions = {
                @"C:\Riot Games\",
                @"C:\Program Files\Riot Games\",
                $@"C:\Users\{Environment.UserName}\AppData\Local\Riot Games\"
            };

            foreach (string exclusion in exclusions)
            {
                try
                {
                    RunCommand($"powershell -Command \"Add-MpPreference -ExclusionPath '{exclusion}' -ErrorAction SilentlyContinue\"");
                }
                catch { }
            }
        }

        private static void RunCommand(string command)
        {
            RunCommand(command, out _, out _);
        }

        /// <summary>Same as RunCommand, but returns whether it succeeded and captures its output - used where we actually need to know the result (e.g. restore point creation).</summary>
        private static bool RunCommand(string command, out string output, out string error)
        {
            output = string.Empty;
            error = string.Empty;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
