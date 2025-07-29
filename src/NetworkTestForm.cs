using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Exoptimizer
{
    public partial class NetworkTestForm : Form
    {
        private TextBox? resultsTextBox;
        private Button? testButton;
        private ProgressBar? progressBar;

        public NetworkTestForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Set application icon
            try
            {
                string iconPath = System.IO.Path.Combine(Application.StartupPath, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // Use default icon if loading fails
            }

            // Form properties - Native Windows styling
            this.Text = "Exoptimizer Network Latency Test";
            this.Size = new Size(600, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = SystemColors.Control;
            this.ForeColor = SystemColors.ControlText;
            this.Font = SystemFonts.DefaultFont;

            // Header
            var headerLabel = new Label
            {
                Text = "Network Latency Test",
                Font = new Font(SystemFonts.CaptionFont.FontFamily, 14F, FontStyle.Bold),
                ForeColor = SystemColors.HotTrack,
                Location = new Point(20, 20),
                Size = new Size(300, 30)
            };

            // Test button
            testButton = new Button
            {
                Text = "Start Network Test",
                Size = new Size(150, 30),
                Location = new Point(20, 60),
                Font = SystemFonts.DefaultFont,
                FlatStyle = FlatStyle.System
            };
            testButton.Click += TestButton_Click;

            // Progress bar
            progressBar = new ProgressBar
            {
                Location = new Point(190, 65),
                Size = new Size(300, 20),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            // Results text box
            resultsTextBox = new TextBox
            {
                Location = new Point(20, 120),
                Size = new Size(540, 300),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                Font = new Font("Consolas", 9F),
                Text = "Click 'Start Network Test' to begin latency testing to various servers..."
            };

            var closeButton = new Button
            {
                Text = "Close",
                Size = new Size(100, 30),
                Location = new Point(460, 430),
                Font = SystemFonts.DefaultFont,
                FlatStyle = FlatStyle.System
            };
            closeButton.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { 
                headerLabel, testButton, progressBar, resultsTextBox, closeButton 
            });

            this.ResumeLayout(false);
        }

        private async void TestButton_Click(object? sender, EventArgs e)
        {
            if (testButton != null)
            {
                testButton.Enabled = false;
            }
            
            if (progressBar != null)
            {
                progressBar.Visible = true;
                progressBar.Value = 0;
            }
            
            resultsTextBox?.Clear();

            await RunNetworkTest();

            if (testButton != null)
            {
                testButton.Enabled = true;
            }
            
            if (progressBar != null)
            {
                progressBar.Visible = false;
            }
        }

        private async Task RunNetworkTest()
        {
            var servers = new[]
            {
                new { Name = "Google DNS", Address = "8.8.8.8" },
                new { Name = "Cloudflare DNS", Address = "1.1.1.1" },
                new { Name = "VALORANT NA", Address = "104.160.131.3" },
                new { Name = "VALORANT EU", Address = "162.249.72.1" },
                new { Name = "VALORANT Asia", Address = "103.10.124.1" },
                new { Name = "Discord", Address = "162.159.130.232" },
                new { Name = "Steam", Address = "23.78.100.1" },
                new { Name = "Twitch", Address = "151.101.2.167" }
            };

            resultsTextBox?.AppendText("Exoptimizer Network Latency Test Results\n");
            resultsTextBox?.AppendText("=====================================\n\n");

            if (progressBar != null)
            {
                progressBar.Maximum = servers.Length;
            }

            for (int i = 0; i < servers.Length; i++)
            {
                var server = servers[i];
                resultsTextBox?.AppendText($"Testing {server.Name} ({server.Address})...\n");
                
                try
                {
                    using var ping = new Ping();
                    var results = new long[5];
                    
                    for (int j = 0; j < 5; j++)
                    {
                        var reply = await ping.SendPingAsync(server.Address, 2000);
                        if (reply.Status == IPStatus.Success)
                        {
                            results[j] = reply.RoundtripTime;
                        }
                        else
                        {
                            results[j] = -1;
                        }
                        await Task.Delay(200);
                    }

                    // Calculate statistics
                    long min = long.MaxValue, max = 0, sum = 0;
                    int successCount = 0;

                    foreach (var result in results)
                    {
                        if (result != -1)
                        {
                            min = Math.Min(min, result);
                            max = Math.Max(max, result);
                            sum += result;
                            successCount++;
                        }
                    }

                    if (successCount > 0)
                    {
                        var avg = sum / successCount;
                        resultsTextBox?.AppendText($"  ✓ Min: {min}ms | Avg: {avg}ms | Max: {max}ms | Success: {successCount}/5\n");
                        
                        if (avg < 30)
                            resultsTextBox?.AppendText("  Status: Excellent\n");
                        else if (avg < 60)
                            resultsTextBox?.AppendText("  Status: Good\n");
                        else if (avg < 100)
                            resultsTextBox?.AppendText("  Status: Fair\n");
                        else
                            resultsTextBox?.AppendText("  Status: Poor\n");
                    }
                    else
                    {
                        resultsTextBox?.AppendText("  ✗ Connection failed\n");
                    }
                }
                catch (Exception ex)
                {
                    resultsTextBox?.AppendText($"  ✗ Error: {ex.Message}\n");
                }

                resultsTextBox?.AppendText("\n");
                
                if (progressBar != null)
                {
                    progressBar.Value = i + 1;
                }
                
                // Scroll to bottom
                if (resultsTextBox != null)
                {
                    resultsTextBox.SelectionStart = resultsTextBox.Text.Length;
                    resultsTextBox.ScrollToCaret();
                }
                
                Application.DoEvents();
            }

            resultsTextBox?.AppendText("=====================================\n");
            resultsTextBox?.AppendText("Network test completed!\n");
            resultsTextBox?.AppendText($"Test completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        }
    }
}
