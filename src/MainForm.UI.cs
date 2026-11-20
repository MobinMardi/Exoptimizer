using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// The visual layer of the main window: sidebar navigation, page layouts and
    /// the live monitor display. All optimization / system logic lives in
    /// MainForm.cs; the handlers wired up here are the same ones as always.
    /// </summary>
    public partial class MainForm
    {
        // ---- Layout -------------------------------------------------------------------

        private const int WindowWidth = 1180;
        private const int WindowHeight = 720; // one size for every page; leaves the same ~28 px under the last card as above the page title
        private const int SidebarWidth = 244;
        private const int PageMargin = 48;
        private const int PageWidth = WindowWidth - SidebarWidth - PageMargin * 2; // 840

        // ---- Theme colors under the names the logic code already uses -----------------

        private static Color PrimaryColor => Theme.Accent;
        private static Color SuccessColor => Theme.Success;
        private static Color WarningColor => Theme.Warning;
        private static Color DangerColor => Theme.Danger;
        private static Color TextPrimary => Theme.TextPrimary;
        private static Color TextSecondary => Theme.TextSecondary;

        // ---- UI state -----------------------------------------------------------------

        private readonly List<NavItem> navItems = new();
        private readonly ToolTip navTip = new();
        private readonly bool isElevated = DetectElevation();
        private NavItem? settingsNavItem;

        private ModernButton? extremeOptimizeButton;
        private ToggleSwitch? exclusionsToggle;

        private MetricTile? cpuTile;
        private MetricTile? memoryTile;
        private MetricTile? networkTile;
        private StatusLabel? valorantProcessStatus;
        private StatusLabel? riotClientStatus;
        private StatusLabel? vanguardStatus;
        private ModernButton? startVanguardButton;
        private bool monitorRefreshing;

        private static bool DetectElevation()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this);
        }

        // ---- Theme --------------------------------------------------------------------

        private void ApplyTheme()
        {
            Theme.Apply(isDarkMode);

            // Before the window exists there is nothing to repaint; the controls pick
            // up the theme when they are created.
            if (!IsHandleCreated) return;

            BackColor = Theme.Canvas;
            WindowChrome.Apply(this);

            if (navigationPanel != null)
            {
                navigationPanel.BackColor = Theme.Canvas;
                navigationPanel.Invalidate(true);
            }

            if (contentPanel != null)
            {
                contentPanel.BackColor = Theme.Canvas;
            }

            // Rebuild the current page so every control picks up the new colors.
            string tempSection = currentSection;
            currentSection = "";
            NavigateToSection(tempSection);

            Refresh();
        }

        // ---- Window + sidebar ---------------------------------------------------------

        private void InitializeComponent()
        {
            SuspendLayout();

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

            // Fixed-size window, as before.
            AutoScaleMode = AutoScaleMode.None;
            Text = "Exoptimizer";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(WindowWidth, WindowHeight);
            MinimumSize = Size;
            MaximumSize = Size;
            BackColor = Theme.Canvas;
            Font = Theme.Font(9f);
            DoubleBuffered = true;

            CreateInterface();
            ResumeLayout(false);
        }

        private void CreateInterface()
        {
            // Page area
            contentPanel = new BufferedPanel
            {
                Location = new Point(SidebarWidth, 0),
                Size = new Size(WindowWidth - SidebarWidth, WindowHeight),
                BackColor = Theme.Canvas,
                AutoScroll = true
            };

            // Sidebar
            navigationPanel = new BufferedPanel
            {
                Location = new Point(0, 0),
                Size = new Size(SidebarWidth, WindowHeight),
                BackColor = Theme.Canvas
            };
            navigationPanel.Paint += PaintSidebar;

            var entries = new (string Text, string Section)[]
            {
                ("System Optimization", "optimization"),
                ("VALORANT Priority", "valorant"),
                ("Performance Tools", "tools"),
                ("System Monitor", "monitor"),
                ("System Restore", "restore"),
                ("Settings", "settings"),
            };

            int y = 110;
            foreach (var entry in entries)
            {
                string section = entry.Section;
                var item = new NavItem
                {
                    Text = entry.Text,
                    Section = section,
                    Location = new Point(16, y),
                    Size = new Size(SidebarWidth - 32, 42),
                    Selected = section == currentSection
                };
                item.Click += (s, e) => NavigateToSection(section);

                navigationPanel.Controls.Add(item);
                navItems.Add(item);
                if (section == "settings") settingsNavItem = item;
                y += 46;
            }

            Controls.Add(contentPanel);
            Controls.Add(navigationPanel);
        }

        private void PaintSidebar(object? sender, PaintEventArgs e)
        {
            if (sender is not Control panel) return;

            var g = e.Graphics;
            Gfx.Smooth(g);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Divider between sidebar and page
            using (var pen = new Pen(Theme.Border, 1f))
            {
                g.DrawLine(pen, panel.Width - 1, 0, panel.Width - 1, panel.Height);
            }

            // Brand
            var logoRect = new Rectangle(24, 28, 40, 40);
            var logo = AppAssets.Logo(40);
            if (logo != null)
            {
                g.DrawImage(logo, logoRect);
            }
            else
            {
                using var fallbackPath = Gfx.RoundedRect(logoRect, 10f);
                using var fallbackBrush = new SolidBrush(Theme.Accent);
                g.FillPath(fallbackBrush, fallbackPath);
            }

            using (var ringPath = Gfx.RoundedRect(new RectangleF(23.5f, 27.5f, 41f, 41f), 4.5f))
            using (var ringPen = new Pen(Color.FromArgb(Theme.IsDark ? 130 : 60, Theme.BorderStrong), 1f))
            {
                g.DrawPath(ringPen, ringPath);
            }

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, "Exoptimizer", Theme.Font(14f, true), new Rectangle(76, 24, 150, 24), Theme.TextPrimary, flags);
            TextRenderer.DrawText(g, "v" + AppVersion, Theme.Font(9f), new Rectangle(76, 52, 150, 18), Theme.TextTertiary, flags);

            // Privilege indicator
            Color tone = isElevated ? Theme.Success : Theme.Warning;
            using (var dot = new SolidBrush(tone))
            {
                g.FillEllipse(dot, 28f, panel.Height - 38f, 8f, 8f);
            }

            TextRenderer.DrawText(g, isElevated ? "Running as administrator" : "Not running as administrator",
                Theme.Font(9f), new Rectangle(44, panel.Height - 44, panel.Width - 56, 20), Theme.TextSecondary, flags);
        }

        private void SetUpdateBadge(bool visible)
        {
            if (settingsNavItem == null || settingsNavItem.IsDisposed) return;

            settingsNavItem.ShowDot = visible;
            navTip.SetToolTip(settingsNavItem, visible ? "A new version is available - see Settings" : string.Empty);
        }

        private void NavigateToSection(string section)
        {
            if (section == currentSection) return;

            currentSection = section;

            foreach (var item in navItems)
            {
                item.Selected = item.Section == section;
            }

            ClearContent();

            contentPanel?.SuspendLayout();

            switch (section)
            {
                case "optimization": LoadOptimizationContent(); break;
                case "valorant": LoadValorantContent(); break;
                case "tools": LoadToolsContent(); break;
                case "monitor": LoadMonitorContent(); break;
                case "restore": LoadRestoreContent(); break;
                case "settings": LoadSettingsContent(); break;
            }

            contentPanel?.ResumeLayout(true);
        }

        /// <summary>Removes the current page. The old controls are disposed a moment later, so a handler can safely rebuild the page that raised it.</summary>
        private void ClearContent()
        {
            if (contentPanel == null) return;

            var old = new List<Control>();
            foreach (Control control in contentPanel.Controls) old.Add(control);

            contentPanel.Controls.Clear();
            contentPanel.AutoScrollPosition = new Point(0, 0);

            // Forget the references to controls that are no longer on screen.
            optimizeButton = null;
            extremeOptimizeButton = null;
            defenderCheckBox = null;
            exclusionsToggle = null;
            valorantEnablePriority = null;
            valorantDisablePriority = null;
            valorantStatusLabel = null;
            updateStatusLabel = null;
            cpuTile = null;
            memoryTile = null;
            networkTile = null;
            valorantProcessStatus = null;
            riotClientStatus = null;
            vanguardStatus = null;
            startVanguardButton = null;

            if (IsHandleCreated)
            {
                BeginInvoke(new Action(() =>
                {
                    foreach (var control in old) control.Dispose();
                }));
            }
            else
            {
                foreach (var control in old) control.Dispose();
            }
        }

        // ---- Small builders -----------------------------------------------------------

        private static Label MakeLabel(string text, float size, bool semibold, Color color, Rectangle bounds)
        {
            return new Label
            {
                Text = text,
                Font = Theme.Font(size, semibold),
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                UseMnemonic = false,
                Bounds = bounds
            };
        }

        private static ModernButton MakeButton(string text, Rectangle bounds, ButtonKind kind = ButtonKind.Filled, Color? fill = null, Color? tint = null)
        {
            var button = new ModernButton { Text = text, Bounds = bounds, Kind = kind };
            if (fill.HasValue) button.BackColor = fill.Value;
            if (tint.HasValue) button.Tint = tint.Value;
            return button;
        }

        private static CardPanel AddCard(Control host, int x, int y, int width, int height)
        {
            var card = new CardPanel { Bounds = new Rectangle(x, y, width, height) };
            host.Controls.Add(card);
            return card;
        }

        private static void AddPageHeader(Control host, string title, string subtitle)
        {
            host.Controls.Add(MakeLabel(title, 24f, true, Theme.TextPrimary, new Rectangle(PageMargin, 28, PageWidth, 52)));
            host.Controls.Add(MakeLabel(subtitle, 10.5f, false, Theme.TextSecondary, new Rectangle(PageMargin, 88, PageWidth, 24)));
        }

        /// <summary>Card title + supporting text, the shared pattern at the top of most cards.</summary>
        private static void AddCardText(CardPanel card, string title, string description, int descriptionHeight = 46)
        {
            card.Controls.Add(MakeLabel(title, 12f, true, Theme.TextPrimary, new Rectangle(24, 18, card.Width - 48, 30)));
            card.Controls.Add(MakeLabel(description, 9.5f, false, Theme.TextSecondary, new Rectangle(24, 54, card.Width - 48, descriptionHeight)));
        }

        private static Panel MakeDivider(int x, int y, int width)
        {
            return new Panel { Bounds = new Rectangle(x, y, width, 1), BackColor = Theme.Border };
        }

        // ---- Pages --------------------------------------------------------------------

        private void LoadOptimizationContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "System Optimization", "Optimize your Windows system for maximum gaming performance");

            // Check current optimization status
            CheckOptimizationStatus();
            CheckExtremeOptimizationStatus();

            int x = PageMargin;
            int y = 124;

            // Reassurance banner
            var safety = AddCard(page, x, y, PageWidth, 52);
            safety.FillColor = Theme.SuccessSoft;
            safety.BorderColor = Color.Transparent;
            safety.Controls.Add(new StatusLabel
            {
                Text = "System Restore, your network connection, and your firewall are never disabled by Exoptimizer - ever, at any setting.",
                Font = Theme.Font(9f),
                ForeColor = Theme.Success,
                Bounds = new Rectangle(22, 14, PageWidth - 44, 24)
            });

            const int cardW = 408;
            const int cardH = 152;
            const int gap = 24;
            int row1 = y + 52 + 16;
            int row2 = row1 + cardH + 16;
            int col2 = x + cardW + gap;

            // Optimize System
            var optimizeCard = AddCard(page, x, row1, cardW, cardH);
            AddCardText(optimizeCard, "Optimize System",
                "Creates and verifies a restore point, then tunes services, network, power plan and registry. Every change is recorded for Undo.");
            optimizeButton = MakeButton("Optimize System", new Rectangle(24, 104, 190, 38), ButtonKind.Filled, PrimaryColor);
            optimizeButton.Click += OptimizeButton_Click;
            optimizeCard.Controls.Add(optimizeButton);

            // Quick Boost
            var quickCard = AddCard(page, col2, row1, cardW, cardH);
            AddCardText(quickCard, "Quick Boost", "Switches to the High performance power plan and clears the DNS cache. Undo restores your previous plan.");
            var quickButton = MakeButton("Quick Boost", new Rectangle(24, 104, 150, 38), ButtonKind.Soft);
            quickButton.Click += QuickBoostButton_Click;
            quickCard.Controls.Add(quickButton);

            // Extreme Optimization
            var extremeCard = AddCard(page, x, row2, cardW, cardH);
            AddCardText(extremeCard, "Extreme Optimization",
                "Disables many non-essential Windows features for maximum FPS. A restore point is created and verified first - use with caution.");
            extremeOptimizeButton = MakeButton("Extreme Optimization", new Rectangle(24, 104, 200, 38), ButtonKind.Filled, DangerColor);
            extremeOptimizeButton.Click += ExtremeOptimizeButton_Click;
            extremeCard.Controls.Add(extremeOptimizeButton);

            // Longer Battery
            var batteryCard = AddCard(page, col2, row2, cardW, cardH);
            AddCardText(batteryCard, "Longer Battery",
                "Switches to the Power Saver plan and trims background services for longer battery life when unplugged.");
            var batteryButton = MakeButton("Longer Battery", new Rectangle(24, 104, 150, 38), ButtonKind.Soft);
            batteryButton.Click += BatteryOptimizeButton_Click;
            batteryCard.Controls.Add(batteryButton);

            // Windows Defender options - both off by default, and kept while you move between pages.
            // Each row has room for a two-line description so a longer text is never cut off.
            var advancedCard = AddCard(page, x, row2 + cardH + 16, PageWidth, 163);

            advancedCard.Controls.Add(MakeLabel("Exclude Riot Games folders from Defender scans", 10.5f, true, Theme.TextPrimary,
                new Rectangle(24, 12, 680, 22)));
            advancedCard.Controls.Add(MakeLabel("Off by default. Skips scanning your Riot Games folders; Undo removes only what Exoptimizer added.", 9.5f, false, Theme.TextSecondary,
                new Rectangle(24, 34, 716, 36)));
            exclusionsToggle = new ToggleSwitch { Location = new Point(PageWidth - 24 - 48, 27), Checked = optAddExclusions };
            exclusionsToggle.CheckedChanged += (s, e) => optAddExclusions = exclusionsToggle != null && exclusionsToggle.Checked;
            advancedCard.Controls.Add(exclusionsToggle);

            advancedCard.Controls.Add(MakeDivider(24, 80, PageWidth - 48));

            advancedCard.Controls.Add(MakeLabel("Turn off Defender real-time protection", 10.5f, true, Theme.TextPrimary,
                new Rectangle(24, 93, 680, 22)));
            advancedCard.Controls.Add(MakeLabel("Off by default. Advanced: Tamper Protection may block it, and Undo restores it.", 9.5f, false, Theme.TextSecondary,
                new Rectangle(24, 115, 716, 36)));

            var realtimeToggle = new ToggleSwitch { Location = new Point(PageWidth - 24 - 48, 108), Checked = optDisableRealtime };
            bool reverting = false;
            realtimeToggle.CheckedChanged += (s, e) =>
            {
                if (reverting) return;

                if (realtimeToggle.Checked)
                {
                    var confirmRealtime = AppDialog.Show(
                        "Turn off Windows Defender real-time protection when optimizing?" + Environment.NewLine + Environment.NewLine +
                        "Your PC will be less protected against malware while it is off. Exoptimizer records the original setting, checks that Windows really applied the change, and Undo turns it back on.",
                        "Turn off real-time protection?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

                    if (confirmRealtime != DialogResult.Yes)
                    {
                        reverting = true;
                        realtimeToggle.Checked = false;
                        reverting = false;
                        optDisableRealtime = false;
                        return;
                    }
                }

                optDisableRealtime = realtimeToggle.Checked;
            };
            defenderCheckBox = realtimeToggle;
            advancedCard.Controls.Add(realtimeToggle);

            RefreshOptimizationButtons();
        }

        private void LoadValorantContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "VALORANT Priority", "Boost VALORANT performance with high process priority and network optimization");

            int x = PageMargin;

            var priorityCard = AddCard(page, x, 124, PageWidth, 196);
            priorityCard.Controls.Add(MakeLabel("VALORANT Process Priority", 13f, true, Theme.TextPrimary, new Rectangle(24, 20, 600, 32)));
            priorityCard.Controls.Add(MakeLabel("Sets VALORANT to High CPU priority and adds a network QoS policy. VALORANT needs to be running.",
                9.5f, false, Theme.TextSecondary, new Rectangle(24, 58, PageWidth - 48, 24)));

            valorantEnablePriority = MakeButton("Enable Priority", new Rectangle(24, 96, 160, 40), ButtonKind.Filled);
            valorantEnablePriority.Click += ValorantEnableButton_Click;

            valorantDisablePriority = MakeButton("Disable Priority", new Rectangle(196, 96, 160, 40), ButtonKind.Ghost, null, Theme.Danger);
            valorantDisablePriority.Click += ValorantDisableButton_Click;

            valorantStatusLabel = new StatusLabel
            {
                Text = "Priority: Checking...",
                Font = Theme.Font(10f, true),
                ForeColor = Theme.TextSecondary,
                Bounds = new Rectangle(24, 152, 600, 26)
            };

            priorityCard.Controls.AddRange(new Control[] { valorantEnablePriority, valorantDisablePriority, valorantStatusLabel });

            var infoCard = AddCard(page, x, 124 + 196 + 16, PageWidth, 188);
            infoCard.Controls.Add(MakeLabel("What this changes", 12f, true, Theme.TextPrimary, new Rectangle(24, 18, 400, 30)));
            infoCard.Controls.Add(MakeLabel("Process priority", 10f, true, Theme.TextPrimary, new Rectangle(24, 58, 400, 24)));
            infoCard.Controls.Add(MakeLabel("VALORANT-Win64-Shipping.exe runs at High priority while this is enabled, and returns to Normal when you disable it.",
                9.5f, false, Theme.TextSecondary, new Rectangle(24, 86, PageWidth - 48, 22)));
            infoCard.Controls.Add(MakeLabel("Network QoS", 10f, true, Theme.TextPrimary, new Rectangle(24, 124, 400, 24)));
            infoCard.Controls.Add(MakeLabel("A policy named “Exoptimizer VALORANT Priority” marks VALORANT traffic as high priority (DSCP 46). Disabling removes it.",
                9.5f, false, Theme.TextSecondary, new Rectangle(24, 152, PageWidth - 48, 22)));

            // Check current VALORANT priority status
            CheckValorantPriorityStatus();
        }

        private void LoadToolsContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "Performance Tools", "Quick access to essential Windows system management tools and cleanup utilities");

            int x = PageMargin;

            var toolsCard = AddCard(page, x, 124, PageWidth, 242);
            toolsCard.Controls.Add(MakeLabel("System tools", 12f, true, Theme.TextPrimary, new Rectangle(24, 18, 400, 30)));

            var tools = new (string Title, string Description, string Executable)[]
            {
                ("Task Manager", "Processes and performance", "taskmgr"),
                ("Resource Monitor", "CPU, memory, disk and network", "resmon.exe"),
                ("Disk Cleanup", "Free up drive space", "cleanmgr.exe"),
                ("Services", "Manage Windows services", "services.msc"),
                ("Registry Editor", "Edit the Windows registry", "regedit.exe"),
                ("System Config", "Startup and boot options", "msconfig.exe"),
            };

            const int tileW = 254;
            const int tileH = 72;
            const int tileGap = 15;
            for (int i = 0; i < tools.Length; i++)
            {
                string executable = tools[i].Executable;
                var tile = new ToolTile
                {
                    Text = tools[i].Title,
                    Description = tools[i].Description,
                    Bounds = new Rectangle(24 + (i % 3) * (tileW + tileGap), 60 + (i / 3) * (tileH + 14), tileW, tileH)
                };
                tile.Click += (s, e) => { try { Process.Start(executable); } catch { } };
                toolsCard.Controls.Add(tile);
            }

            var cleanupCard = AddCard(page, x, 124 + 242 + 16, PageWidth, 168);
            AddCardText(cleanupCard, "Deep System Cleanup",
                "Choose what to clean and see how much each category holds first. Files in use are skipped; logs and crash dumps are kept unless you select them.", 44);
            var deepCleanupBtn = MakeButton("Deep System Cleanup", new Rectangle(24, 108, 200, 40), ButtonKind.Filled);
            deepCleanupBtn.Click += DeepCleanupButton_Click;
            cleanupCard.Controls.Add(deepCleanupBtn);
        }

        private void LoadMonitorContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "System Monitor", "Monitor your system performance and resource usage in real-time");

            int x = PageMargin;
            int y = 124;
            const int tileW = 264;
            const int tileGap = 24;

            cpuTile = new MetricTile { Text = "CPU", Bounds = new Rectangle(x, y, tileW, 132) };
            cpuTile.SetState("-", "Loading...", 0, Theme.TextTertiary);

            memoryTile = new MetricTile { Text = "Memory", Bounds = new Rectangle(x + tileW + tileGap, y, tileW, 132) };
            memoryTile.SetState("-", "Loading...", 0, Theme.TextTertiary);

            networkTile = new MetricTile { Text = "Network latency", Bounds = new Rectangle(x + (tileW + tileGap) * 2, y, tileW, 132) };
            networkTile.SetState("-", "Testing...", 0, Theme.TextTertiary);

            page.Controls.AddRange(new Control[] { cpuTile, memoryTile, networkTile });

            var processCard = AddCard(page, x, y + 132 + 20, PageWidth, 208);
            processCard.Controls.Add(MakeLabel("VALORANT processes", 12f, true, Theme.TextPrimary, new Rectangle(24, 18, 400, 30)));

            valorantProcessStatus = new StatusLabel { Text = "VALORANT: Checking...", Bounds = new Rectangle(24, 64, 560, 28) };
            riotClientStatus = new StatusLabel { Text = "Riot Client: Checking...", Bounds = new Rectangle(24, 108, 560, 28) };
            vanguardStatus = new StatusLabel { Text = "Riot Vanguard: Checking...", Bounds = new Rectangle(24, 152, 560, 28) };

            // Only shown while Vanguard is not running.
            startVanguardButton = MakeButton("Start Vanguard", new Rectangle(PageWidth - 24 - 140, 149, 140, 34), ButtonKind.Soft, null, Theme.Danger);
            startVanguardButton.Visible = false;
            startVanguardButton.Click += StartVanguardButton_Click;

            processCard.Controls.AddRange(new Control[] { valorantProcessStatus, riotClientStatus, vanguardStatus, startVanguardButton });

            // Start updating monitor data
            UpdateMonitorData(null, EventArgs.Empty);
        }

        private void LoadRestoreContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "System Restore", "Backup and restore your system settings safely");

            int x = PageMargin;

            var restoreCard = AddCard(page, x, 124, PageWidth, 150);
            AddCardText(restoreCard, "Restore points",
                "Create a restore point before making changes, or roll your system back.", 22);

            var createRestoreBtn = MakeButton("Create Restore Point", new Rectangle(24, 90, 180, 40), ButtonKind.Filled);
            createRestoreBtn.Click += RestoreButton_Click;

            var systemRestoreBtn = MakeButton("System Restore", new Rectangle(216, 90, 150, 40), ButtonKind.Ghost);
            systemRestoreBtn.Click += (s, e) => { try { Process.Start("rstrui.exe"); } catch { } };

            var undoBtn = MakeButton("Undo Optimizations", new Rectangle(378, 90, 180, 40), ButtonKind.Ghost, null, Theme.Warning);
            undoBtn.Click += UndoOptimizationButton_Click;

            var rebootBtn = MakeButton("Restart PC", new Rectangle(570, 90, 120, 40), ButtonKind.Ghost, null, Theme.Danger);
            rebootBtn.Click += (s, e) =>
            {
                var result = AppDialog.Show("Restart now?", "Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    try { Process.Start("shutdown", "/r /t 0"); } catch { }
                }
            };

            restoreCard.Controls.AddRange(new Control[] { createRestoreBtn, systemRestoreBtn, undoBtn, rebootBtn });

            var repairCard = AddCard(page, x, 124 + 150 + 16, PageWidth, 164);
            AddCardText(repairCard, "Upgraded from an older Exoptimizer version?",
                "If critical services or Defender policies were left changed by an older version or another tool, this previews exactly what would change, saves your current values, then moves them back toward how Windows ships them.", 40);

            var repairBtn = MakeButton("Repair Critical Services", new Rectangle(24, 104, 210, 40), ButtonKind.Filled, SuccessColor);
            repairBtn.Click += RepairCriticalServicesButton_Click;

            var protectedInfoBtn = MakeButton("What does Exoptimizer never touch?", new Rectangle(246, 104, 340, 40), ButtonKind.Ghost);
            protectedInfoBtn.Click += (s, e) =>
            {
                string list = string.Join(Environment.NewLine, SystemGuard.ProtectedServices.OrderBy(n => n));
                AppDialog.Show(
                    "Exoptimizer will never disable or stop these Windows services, at any setting:" +
                    Environment.NewLine + Environment.NewLine + list,
                    "Protected Services", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            repairCard.Controls.AddRange(new Control[] { repairBtn, protectedInfoBtn });
        }

        private void LoadSettingsContent()
        {
            var page = contentPanel;
            if (page == null) return;

            AddPageHeader(page, "Settings", "Configure application preferences and display options");

            int x = PageMargin;

            // Preferences
            var prefsCard = AddCard(page, x, 124, PageWidth, 144);

            prefsCard.Controls.Add(MakeLabel("Dark mode", 10.5f, true, Theme.TextPrimary, new Rectangle(24, 14, 600, 26)));
            prefsCard.Controls.Add(MakeLabel("Use a dark interface that is easier on the eyes.", 9.5f, false, Theme.TextSecondary, new Rectangle(24, 42, 600, 22)));
            var darkModeToggle = new ToggleSwitch { Location = new Point(PageWidth - 24 - 48, 22), Checked = isDarkMode };

            prefsCard.Controls.Add(MakeDivider(24, 72, PageWidth - 48));

            prefsCard.Controls.Add(MakeLabel("System tray", 10.5f, true, Theme.TextPrimary, new Rectangle(24, 86, 600, 26)));
            prefsCard.Controls.Add(MakeLabel("Minimize to the system tray instead of closing the window.", 9.5f, false, Theme.TextSecondary, new Rectangle(24, 114, 600, 22)));
            var trayToggle = new ToggleSwitch { Location = new Point(PageWidth - 24 - 48, 94), Checked = systemTrayEnabled };

            darkModeToggle.CheckedChanged += (s, e) =>
            {
                isDarkMode = darkModeToggle.Checked;
                ApplyTheme();
                SaveSettings();

                // Force immediate visual update
                this.Invalidate(true);
                this.Update();
            };

            trayToggle.CheckedChanged += (s, e) =>
            {
                systemTrayEnabled = trayToggle.Checked;
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

            prefsCard.Controls.Add(darkModeToggle);
            prefsCard.Controls.Add(trayToggle);

            // About
            var aboutCard = AddCard(page, x, 124 + 144 + 16, PageWidth, 214);
            aboutCard.Controls.Add(MakeLabel("About", 12f, true, Theme.TextPrimary, new Rectangle(24, 18, 400, 30)));
            aboutCard.Controls.Add(MakeLabel(
                $"Version: {AppVersion}" + Environment.NewLine + "mDev (Mobin Mardi)" + Environment.NewLine + "Copyright © 2026",
                10f, false, Theme.TextSecondary, new Rectangle(24, 56, 400, 64)));

            var checkUpdatesBtn = MakeButton("Check for Updates", new Rectangle(24, 128, 170, 38), ButtonKind.Filled);
            checkUpdatesBtn.Click += CheckForUpdatesButton_Click;

            var viewChangelogBtn = MakeButton("View Changelog", new Rectangle(206, 128, 150, 38), ButtonKind.Ghost);
            viewChangelogBtn.Click += (s, e) =>
            {
                using var changelogForm = new ChangelogForm();
                changelogForm.ShowDialog(this);
            };

            updateStatusLabel = MakeLabel(BuildUpdateStatusText(), 9.5f, false,
                (latestUpdateResult?.Success == true && latestUpdateResult.UpdateAvailable) ? SuccessColor : TextSecondary,
                new Rectangle(24, 176, PageWidth - 48, 24));

            aboutCard.Controls.AddRange(new Control[] { checkUpdatesBtn, viewChangelogBtn, updateStatusLabel });

            // Reset
            var resetBtn = MakeButton("Reset to Defaults", new Rectangle(x, 124 + 144 + 16 + 214 + 16, 170, 38), ButtonKind.Ghost, null, Theme.Warning);
            resetBtn.Click += (s, e) =>
            {
                var result = AppDialog.Show("Reset all settings to default?", "Reset Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    systemTrayEnabled = false;
                    isDarkMode = false;
                    trayToggle.Checked = false;
                    darkModeToggle.Checked = false;

                    ApplyTheme();

                    allowVisible = true;
                    this.Show();
                    this.ShowInTaskbar = true;
                    if (notifyIcon != null) notifyIcon.Visible = false;

                    SaveSettings();
                    AppDialog.Show("Settings reset to defaults!", "Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            page.Controls.Add(resetBtn);
        }

        // ---- Live monitor -------------------------------------------------------------

        private sealed class MonitorSnapshot
        {
            public double Cpu;

            public bool MemoryOk;
            public double MemoryUsedGb;
            public double MemoryTotalGb;
            public double MemoryPercent;

            /// <summary>0 = answered, 1 = no reply, 2 = could not run the test.</summary>
            public int LatencyState = 2;
            public long LatencyMs;
        }

        /// <summary>Runs on a background thread, so the window never stalls while Windows is queried or the ping is waiting.</summary>
        private static MonitorSnapshot CollectMonitorSnapshot()
        {
            var snapshot = new MonitorSnapshot { Cpu = GetCpuUsage() };

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024;
                    var free = Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024 / 1024;
                    var used = total - free;

                    snapshot.MemoryUsedGb = used;
                    snapshot.MemoryTotalGb = total;
                    snapshot.MemoryPercent = (used / total) * 100;
                    snapshot.MemoryOk = true;
                    break;
                }
            }
            catch
            {
                snapshot.MemoryOk = false;
            }

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send("8.8.8.8", 1000);

                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    snapshot.LatencyState = 0;
                    snapshot.LatencyMs = reply.RoundtripTime;
                }
                else
                {
                    snapshot.LatencyState = 1;
                }
            }
            catch
            {
                snapshot.LatencyState = 2;
            }

            return snapshot;
        }

        private async void UpdateMonitorData(object? sender, EventArgs e)
        {
            if (currentSection != "monitor" || cpuTile == null || memoryTile == null || networkTile == null) return;

            // Process checks are quick, so they stay on the UI thread.
            UpdateProcessStatuses();

            if (monitorRefreshing) return;
            monitorRefreshing = true;

            try
            {
                var snapshot = await Task.Run(() => CollectMonitorSnapshot());

                if (IsDisposed || currentSection != "monitor") return;
                ApplyMonitorSnapshot(snapshot);
            }
            catch
            {
                // A failed sample just leaves the previous values on screen.
            }
            finally
            {
                monitorRefreshing = false;
            }
        }

        private void ApplyMonitorSnapshot(MonitorSnapshot snapshot)
        {
            if (cpuTile != null && !cpuTile.IsDisposed)
            {
                double cpu = snapshot.Cpu;
                Color tone = cpu > 80 ? DangerColor : cpu > 50 ? WarningColor : SuccessColor;
                cpuTile.SetState($"{cpu:F1}%", "Total processor usage", cpu / 100.0, tone);
            }

            if (memoryTile != null && !memoryTile.IsDisposed)
            {
                if (snapshot.MemoryOk)
                {
                    double pct = snapshot.MemoryPercent;
                    Color tone = pct > 80 ? DangerColor : pct > 60 ? WarningColor : SuccessColor;
                    memoryTile.SetState($"{pct:F1}%", $"{snapshot.MemoryUsedGb:F1} GB of {snapshot.MemoryTotalGb:F1} GB in use", pct / 100.0, tone);
                }
                else
                {
                    memoryTile.SetState("-", "Unable to retrieve", 0, TextSecondary);
                }
            }

            if (networkTile != null && !networkTile.IsDisposed)
            {
                switch (snapshot.LatencyState)
                {
                    case 0:
                    {
                        long ms = snapshot.LatencyMs;
                        Color tone = ms > 100 ? DangerColor : ms > 50 ? WarningColor : SuccessColor;
                        networkTile.SetState($"{ms} ms", "Google DNS (8.8.8.8)", ms / 200.0, tone);
                        break;
                    }
                    case 1:
                        networkTile.SetState("-", "Connection failed", 0, DangerColor);
                        break;
                    default:
                        networkTile.SetState("-", "Unable to test", 0, TextSecondary);
                        break;
                }
            }
        }

        private void UpdateProcessStatuses()
        {
            try
            {
                // Check VALORANT
                var valorantProcesses = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                if (valorantProcessStatus != null)
                {
                    if (valorantProcesses.Length > 0)
                    {
                        valorantProcessStatus.Text = $"VALORANT: Running (PID: {valorantProcesses[0].Id})";
                        valorantProcessStatus.ForeColor = SuccessColor;
                    }
                    else
                    {
                        valorantProcessStatus.Text = "VALORANT: Not running";
                        valorantProcessStatus.ForeColor = TextSecondary;
                    }
                }

                // Check Riot Client
                var riotProcesses = Process.GetProcessesByName("RiotClientServices");
                if (riotClientStatus != null)
                {
                    if (riotProcesses.Length > 0)
                    {
                        riotClientStatus.Text = $"Riot Client: Running (PID: {riotProcesses[0].Id})";
                        riotClientStatus.ForeColor = SuccessColor;
                    }
                    else
                    {
                        riotClientStatus.Text = "Riot Client: Not running";
                        riotClientStatus.ForeColor = TextSecondary;
                    }
                }

                // Check Riot Vanguard and manage start button visibility
                var vanguardProcesses = Process.GetProcessesByName("vgc");
                bool vanguardRunning = vanguardProcesses.Length > 0;

                if (vanguardStatus != null)
                {
                    if (vanguardRunning)
                    {
                        vanguardStatus.Text = $"Riot Vanguard: Running (PID: {vanguardProcesses[0].Id})";
                        vanguardStatus.ForeColor = SuccessColor;
                    }
                    else
                    {
                        vanguardStatus.Text = "Riot Vanguard: Not running";
                        vanguardStatus.ForeColor = TextSecondary;
                    }
                }

                // Show the start Vanguard button only while Vanguard is not running
                if (currentSection == "monitor" && startVanguardButton != null && !startVanguardButton.IsDisposed)
                {
                    startVanguardButton.Visible = !vanguardRunning;
                    startVanguardButton.BringToFront();
                }
            }
            catch
            {
                if (valorantProcessStatus != null)
                    valorantProcessStatus.Text = "VALORANT: Unable to check";
                if (riotClientStatus != null)
                    riotClientStatus.Text = "Riot Client: Unable to check";
                if (vanguardStatus != null)
                    vanguardStatus.Text = "Riot Vanguard: Unable to check";
            }
        }
    }
}
