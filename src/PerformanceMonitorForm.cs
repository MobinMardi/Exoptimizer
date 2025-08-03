using System;
using System.Diagnostics;
using System.Drawing;
using System.Management;
using System.Windows.Forms;

namespace Exoptimizer
{
    public partial class PerformanceMonitorForm : Form
    {
        private System.Windows.Forms.Timer? updateTimer;
        private Label? cpuLabel;
        private Label? memoryLabel;
        private Label? networkLabel;
        private Label? processLabel;

        public PerformanceMonitorForm()
        {
            InitializeComponent();
            SetupTimer();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Set application icon
            try
            {
                string iconPath = System.IO.Path.Combine(Application.StartupPath, "icon-new.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
                else
                {
                    // Try alternative path
                    iconPath = System.IO.Path.Combine(Application.StartupPath, "..", "assets", "icon-new.ico");
                    if (System.IO.File.Exists(iconPath))
                    {
                        this.Icon = new Icon(iconPath);
                    }
                }
            }
            catch
            {
                // Use default icon if loading fails
            }

            // Form properties - Native Windows styling
            this.Text = "Exoptimizer Performance Monitor";
            this.Size = new Size(600, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;
            this.Font = SystemFonts.DefaultFont;

            // Header
            var headerLabel = new Label
            {
                Text = "Exoptimizer Performance Monitor - Real Time",
                Font = new Font(SystemFonts.CaptionFont.FontFamily, 14F, FontStyle.Bold),
                ForeColor = SystemColors.HotTrack,
                Location = new Point(20, 20),
                Size = new Size(500, 30)
            };

            // Performance labels
            cpuLabel = new Label
            {
                Text = "CPU Usage: Loading...",
                Location = new Point(20, 70),
                Size = new Size(500, 25),
                Font = new Font("Consolas", 10F)
            };

            memoryLabel = new Label
            {
                Text = "Memory Usage: Loading...",
                Location = new Point(20, 100),
                Size = new Size(500, 25),
                Font = new Font("Consolas", 10F)
            };

            networkLabel = new Label
            {
                Text = "Network Latency: Testing...",
                Location = new Point(20, 130),
                Size = new Size(500, 25),
                Font = new Font("Consolas", 10F)
            };

            processLabel = new Label
            {
                Text = "VALORANT Processes: Checking...",
                Location = new Point(20, 160),
                Size = new Size(500, 100),
                Font = new Font("Consolas", 9F)
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Location = new Point(480, 320),
                Font = SystemFonts.DefaultFont,
                FlatStyle = FlatStyle.System
            };
            closeButton.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { 
                headerLabel, cpuLabel, memoryLabel, networkLabel, processLabel, closeButton 
            });

            this.ResumeLayout(false);
        }

        private void SetupTimer()
        {
            updateTimer = new System.Windows.Forms.Timer
            {
                Interval = 2000 // Update every 2 seconds
            };
            updateTimer.Tick += UpdatePerformanceData;
            updateTimer.Start();
        }

        private void UpdatePerformanceData(object? sender, EventArgs e)
        {
            try
            {
                UpdateCpuUsage();
                UpdateMemoryUsage();
                UpdateNetworkLatency();
                UpdateValorantProcesses();
            }
            catch (Exception ex)
            {
                if (cpuLabel != null)
                {
                    cpuLabel.Text = $"Error updating data: {ex.Message}";
                }
            }
        }

        private void UpdateCpuUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("select * from Win32_PerfRawData_PerfOS_Processor where Name='_Total'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var usage = GetCpuUsage();
                    if (cpuLabel != null)
                    {
                        cpuLabel.Text = $"CPU Usage: {usage:F1}%";
                        cpuLabel.ForeColor = usage > 80 ? Color.Red : usage > 50 ? Color.Orange : Color.Green;
                    }
                    break;
                }
            }
            catch
            {
                if (cpuLabel != null)
                {
                    cpuLabel.Text = "CPU Usage: Unable to retrieve";
                }
            }
        }

        private static double GetCpuUsage()
        {
            using var pc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            pc.NextValue(); // First call returns 0
            System.Threading.Thread.Sleep(100);
            return pc.NextValue();
        }

        private void UpdateMemoryUsage()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var total = Convert.ToDouble(obj["TotalVisibleMemorySize"]) / 1024 / 1024; // Convert to GB
                    var free = Convert.ToDouble(obj["FreePhysicalMemory"]) / 1024 / 1024;
                    var used = total - free;
                    var percentage = (used / total) * 100;

                    if (memoryLabel != null)
                    {
                        memoryLabel.Text = $"Memory Usage: {used:F1}GB / {total:F1}GB ({percentage:F1}%)";
                        memoryLabel.ForeColor = percentage > 80 ? Color.Red : percentage > 60 ? Color.Orange : Color.Green;
                    }
                    break;
                }
            }
            catch
            {
                if (memoryLabel != null)
                {
                    memoryLabel.Text = "Memory Usage: Unable to retrieve";
                }
            }
        }

        private void UpdateNetworkLatency()
        {
            try
            {
                var ping = new System.Net.NetworkInformation.Ping();
                var reply = ping.Send("8.8.8.8", 1000);
                
                if (networkLabel != null)
                {
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        networkLabel.Text = $"Network Latency: {reply.RoundtripTime}ms (Google DNS)";
                        networkLabel.ForeColor = reply.RoundtripTime > 100 ? Color.Red : 
                                               reply.RoundtripTime > 50 ? Color.Orange : Color.Green;
                    }
                    else
                    {
                        networkLabel.Text = "Network Latency: Connection failed";
                        networkLabel.ForeColor = Color.Red;
                    }
                }
            }
            catch
            {
                if (networkLabel != null)
                {
                    networkLabel.Text = "Network Latency: Unable to test";
                }
            }
        }

        private void UpdateValorantProcesses()
        {
            try
            {
                var processes = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                var riotProcesses = Process.GetProcessesByName("RiotClientServices");
                
                string processInfo = "VALORANT Processes:\n";
                
                if (processes.Length > 0)
                {
                    processInfo += $"• VALORANT: Running (PID: {processes[0].Id})\n";
                }
                else
                {
                    processInfo += "• VALORANT: Not running\n";
                }
                
                if (riotProcesses.Length > 0)
                {
                    processInfo += $"• Riot Client: Running (PID: {riotProcesses[0].Id})";
                }
                else
                {
                    processInfo += "• Riot Client: Not running";
                }

                if (processLabel != null)
                {
                    processLabel.Text = processInfo;
                    processLabel.ForeColor = (processes.Length > 0 || riotProcesses.Length > 0) ? Color.Green : SystemColors.GrayText;
                }
            }
            catch
            {
                if (processLabel != null)
                {
                    processLabel.Text = "VALORANT Processes: Unable to check";
                }
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            updateTimer?.Stop();
            updateTimer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
