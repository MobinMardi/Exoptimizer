using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// Everything that changes the system. Each operation runs one at a time (OperationGate),
    /// off the UI thread, records what it changes before changing it (ChangeJournal), and
    /// reports exactly what happened - step by step - instead of assuming success.
    /// </summary>
    public partial class MainForm
    {
        private readonly record struct OptimizationOptions(bool DisableRealtimeProtection, bool AddDefenderExclusions);

        private bool journalNoticeShown;

        // ---- Services lists (never include anything in SystemGuard.ProtectedServices) --------

        private static readonly string[] NonEssentialServices = {
            "Fax", "Spooler", "TabletInputService", "Themes", "WSearch", "SysMain",
            "DiagTrack", "dmwappushservice", "MapsBroker", "lfsvc",
            "lltdsvc", "AppVClient", "NetTcpPortSharing", "RemoteAccess", "RemoteRegistry",
            "WbioSrvc", "WMPNetworkSvc", "WpcMonSvc", "SessionEnv", "TermService",
            "UmRdpService", "RpcLocator", "WerSvc", "Wecsvc", "FontCache", "stisvc",
            "wisvc", "PcaSvc", "CscService", "defragsvc", "wuauserv", "UsoSvc", "WaaSMedicSvc"
        };

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

        // ---- Busy state ----------------------------------------------------------------

        private bool BeginOperation(string name)
        {
            if (!OperationGate.TryEnter(name, out string? holder))
            {
                AppDialog.Show($"Another operation is already running ({holder}). Please wait until it finishes.",
                    "Please wait", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            SetBusy(true);
            return true;
        }

        private void EndOperation()
        {
            OperationGate.Exit();
            SetBusy(false);
        }

        private void SetBusy(bool busy)
        {
            if (navigationPanel != null && !navigationPanel.IsDisposed) navigationPanel.Enabled = !busy;
            if (contentPanel != null && !contentPanel.IsDisposed) contentPanel.Enabled = !busy;
        }

        private async Task RunOperationAsync(string name, Func<Task> body)
        {
            if (!BeginOperation(name)) return;

            try
            {
                await body();
            }
            catch (Exception ex)
            {
                AppDialog.Show($"Something unexpected went wrong: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ShowJournalNotice();
        }

        private void ShowJournalNotice()
        {
            if (journalNoticeShown) return;
            journalNoticeShown = true;

            try
            {
                ChangeJournal.Snapshot(); // forces a load so any notice is set
                string? notice = ChangeJournal.LoadNotice;
                if (!string.IsNullOrEmpty(ChangeJournal.AclWarning)) notice = (notice == null ? "" : notice + Environment.NewLine + Environment.NewLine) + ChangeJournal.AclWarning;
                if (!string.IsNullOrEmpty(notice))
                {
                    AppDialog.Show(notice, "Recovery data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch
            {
                // never block start-up on this
            }
        }

        // ---- Status (from what was actually recorded, not guessed from system state) ----

        private void CheckOptimizationStatus()
        {
            try
            {
                var runs = ChangeJournal.Snapshot().Runs;
                var normal = runs.Where(r => r.Mode == "regular" || r.Mode == "extreme").ToList();
                isOptimized = normal.Any(r => r.Succeeded);
                optimizationPartial = !isOptimized && normal.Any(r => !r.Succeeded);
            }
            catch
            {
                isOptimized = false;
                optimizationPartial = false;
            }
        }

        private void CheckExtremeOptimizationStatus()
        {
            try
            {
                var runs = ChangeJournal.Snapshot().Runs.Where(r => r.Mode == "extreme").ToList();
                isExtremeOptimized = runs.Any(r => r.Succeeded);
                extremePartial = !isExtremeOptimized && runs.Any(r => !r.Succeeded);
            }
            catch
            {
                isExtremeOptimized = false;
                extremePartial = false;
            }
        }

        private void RefreshOptimizationButtons()
        {
            if (optimizeButton != null && !optimizeButton.IsDisposed)
            {
                if (isOptimized)
                {
                    optimizeButton.Text = "✓ System Optimized";
                    optimizeButton.BackColor = SuccessColor;
                    optimizeButton.Enabled = false;
                }
                else if (optimizationPartial)
                {
                    optimizeButton.Text = "↻ Retry Optimization";
                    optimizeButton.BackColor = WarningColor;
                    optimizeButton.Enabled = true;
                }
                else
                {
                    optimizeButton.Text = "Optimize System";
                    optimizeButton.BackColor = PrimaryColor;
                    optimizeButton.Enabled = true;
                }
            }

            if (extremeOptimizeButton != null && !extremeOptimizeButton.IsDisposed)
            {
                if (isExtremeOptimized)
                {
                    extremeOptimizeButton.Text = "✓ Extreme Applied";
                    extremeOptimizeButton.BackColor = SuccessColor;
                    extremeOptimizeButton.Enabled = false;
                }
                else if (extremePartial)
                {
                    extremeOptimizeButton.Text = "↻ Retry Extreme";
                    extremeOptimizeButton.BackColor = WarningColor;
                    extremeOptimizeButton.Enabled = true;
                }
                else
                {
                    extremeOptimizeButton.Text = "Extreme Optimization";
                    extremeOptimizeButton.BackColor = DangerColor;
                    extremeOptimizeButton.Enabled = true;
                }
            }
        }

        private static void RecordRun(string mode, OperationReport report)
        {
            ChangeJournal.TryRecord(j => j.Runs.Add(new RunRecord
            {
                Mode = mode,
                TimeUtc = DateTime.UtcNow.ToString("o"),
                Succeeded = report.AllSucceeded,
                FailedSteps = report.FailedStepLabels()
            }), out _);
        }

        // ---- Step runner (background thread, no DoEvents) ------------------------------

        private async Task<OperationReport> RunStepsAsync(string title, IReadOnlyList<OperationStep> steps)
        {
            var report = new OperationReport();
            var dialog = new ProgressDialog(title, closable: false);
            dialog.Status = "Starting...";
            dialog.Show(this);

            try
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    var step = steps[i];
                    dialog.Status = step.Label;
                    dialog.Value = (int)(100.0 * i / steps.Count);

                    Action<string> progress = text =>
                    {
                        if (dialog.IsDisposed || !dialog.IsHandleCreated) return;
                        try
                        {
                            dialog.BeginInvoke(new Action(() =>
                            {
                                if (!dialog.IsDisposed) dialog.Status = text;
                            }));
                        }
                        catch { /* dialog closed meanwhile */ }
                    };

                    StepResult result;
                    try
                    {
                        result = await Task.Run(() => step.Run(progress));
                    }
                    catch (Exception ex)
                    {
                        result = StepResult.Failure(ex.Message);
                    }

                    report.Add(step.Label, result);
                }

                dialog.Value = 100;
            }
            finally
            {
                dialog.Close();
                dialog.Dispose();
            }

            return report;
        }

        // ---- Restore points ------------------------------------------------------------

        private bool AskToEnableSystemProtection()
        {
            bool answer = false;
            try
            {
                Invoke(new Action(() =>
                {
                    answer = AppDialog.Show(
                        "Windows couldn't create a restore point. System Protection may be turned off for your system drive." + Environment.NewLine + Environment.NewLine +
                        "Turn System Protection on now and try again? This changes a Windows setting, and it stays on afterwards.",
                        "Turn on System Protection?", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                }));
            }
            catch
            {
                answer = false;
            }
            return answer;
        }

        private async Task<RestorePointResult> CreateRestorePointWithProgressAsync()
        {
            string description = RestorePointService.NewDescription(AppVersion);
            var dialog = new ProgressDialog("Creating a restore point", closable: false);
            dialog.Status = "Creating a restore point and confirming it exists - this can take a minute...";
            dialog.Value = 35;
            dialog.Show(this);

            try
            {
                return await Task.Run(() => RestorePointService.CreateVerified(description, AskToEnableSystemProtection));
            }
            finally
            {
                dialog.Close();
                dialog.Dispose();
            }
        }

        /// <summary>Creates and verifies a restore point first. If that fails, nothing changes unless the user explicitly chooses to continue.</summary>
        private async Task<(bool Proceed, string Note)> EnsureRestorePointAsync()
        {
            var rp = await CreateRestorePointWithProgressAsync();

            if (rp.Verified)
            {
                return (true, rp.EnabledProtection
                    ? "A restore point was created and confirmed (System Protection was turned on for it)."
                    : "A restore point was created and confirmed before any change was made.");
            }

            var choice = AppDialog.Show(
                rp.Message + Environment.NewLine + Environment.NewLine +
                "Continue WITHOUT a restore point? Undo will still work (every change is recorded), but System Restore won't be available as a fallback.",
                "No restore point", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (choice != DialogResult.Yes) return (false, string.Empty);

            return (true, "No restore point was created - you chose to continue without one.");
        }

        private async void RestoreButton_Click(object? sender, EventArgs e)
        {
            await RunOperationAsync("Create restore point", async () =>
            {
                var rp = await CreateRestorePointWithProgressAsync();

                if (rp.Verified)
                {
                    AppDialog.Show("Restore point created and confirmed." + (rp.EnabledProtection ? Environment.NewLine + "System Protection was turned on for it." : string.Empty),
                        "Restore Point", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppDialog.Show(rp.Message, "Restore Point Not Created", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        // ---- Optimize / Extreme --------------------------------------------------------

        private static string DescribeSecurityChoices(OptimizationOptions o)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Windows Defender:");
            sb.AppendLine(o.DisableRealtimeProtection
                ? "  • Real-time protection will be turned OFF (you switched this option on)."
                : "  • Real-time protection is left unchanged.");

            if (o.AddDefenderExclusions)
            {
                var paths = DefenderTweaks.ExistingExclusionCandidates();
                if (paths.Count > 0)
                {
                    sb.AppendLine("  • These folders will be excluded from Defender scans:");
                    foreach (var p in paths) sb.AppendLine("       " + p);
                }
                else
                {
                    sb.AppendLine("  • Scan exclusions: no Riot Games folders were found.");
                }
            }
            else
            {
                sb.AppendLine("  • No scan exclusions are added.");
            }

            return sb.ToString().TrimEnd();
        }

        private async void OptimizeButton_Click(object? sender, EventArgs e)
        {
            var options = new OptimizationOptions(optDisableRealtime, optAddExclusions);

            var confirm = AppDialog.Show(
                "Optimize your system for gaming?" + Environment.NewLine + Environment.NewLine +
                "First, Exoptimizer will create a system restore point and confirm it exists (Windows' one-per-24-hours limit is lifted only for that request, then put back), and it records the original value of everything it changes so Undo can put it all back." + Environment.NewLine + Environment.NewLine +
                "Then it will:" + Environment.NewLine +
                "• Set non-essential services to Manual (never System Restore, networking or the firewall)" + Environment.NewLine +
                "• Reset TCP auto-tuning and receive-side scaling to healthy defaults, only if they differ" + Environment.NewLine +
                "• Switch to the High performance power plan, if your PC has one" + Environment.NewLine +
                "• Apply a few registry tweaks (GPU scheduling, CPU priority, telemetry)" + Environment.NewLine + Environment.NewLine +
                DescribeSecurityChoices(options) + Environment.NewLine + Environment.NewLine +
                "Continue?",
                "Optimize System", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Optimize System", () => ApplyOptimizationsAsync(options, extreme: false));
        }

        private async void ExtremeOptimizeButton_Click(object? sender, EventArgs e)
        {
            var options = new OptimizationOptions(optDisableRealtime, optAddExclusions);

            var confirm = AppDialog.Show(
                "⚠️ EXTREME OPTIMIZATION ⚠️" + Environment.NewLine + Environment.NewLine +
                "This applies everything in 'Optimize System' plus AGGRESSIVE changes that may:" + Environment.NewLine +
                "• Set many more services to Manual (including Windows Update and telemetry services)" + Environment.NewLine +
                "• Turn off Windows visual effects" + Environment.NewLine +
                "• Turn off common launchers in YOUR startup list" + Environment.NewLine +
                "• Stop the display, disks and system from sleeping on AC power" + Environment.NewLine +
                "• Apply extra memory, game and background-app tweaks" + Environment.NewLine + Environment.NewLine +
                DescribeSecurityChoices(options) + Environment.NewLine + Environment.NewLine +
                "Exoptimizer never disables System Restore, your network or your firewall, at any setting." + Environment.NewLine +
                "A restore point is created and confirmed first (Windows' one-per-24-hours limit is lifted only for that request, then put back), and every change is recorded for Undo." + Environment.NewLine + Environment.NewLine +
                "ONLY CONTINUE IF YOU UNDERSTAND THE RISKS.",
                "Extreme Gaming Optimization", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Extreme Optimization", () => ApplyOptimizationsAsync(options, extreme: true));
        }

        private static List<OperationStep> BuildRegularSteps(OptimizationOptions o)
        {
            var steps = new List<OperationStep>
            {
                new OperationStep("Adjusting non-essential services...", report =>
                    SystemGuard.DisableServicesSafely(NonEssentialServices, (n, i, t) => report($"Adjusting services ({i}/{t}): {n}"), alsoStop: false)),
                new OperationStep("Tuning network settings...", _ => NetworkTweaks.OptimizeTcp()),
                new OperationStep("Applying the High performance power plan...", _ => PowerTweaks.ApplyHighPerformance()),
                new OperationStep("Applying registry tweaks...", _ => RegistryTweaks.Apply(TweakCatalog.Regular)),
            };

            if (o.AddDefenderExclusions)
                steps.Add(new OperationStep("Adding Defender exclusions...", _ => DefenderTweaks.AddExclusions()));
            if (o.DisableRealtimeProtection)
                steps.Add(new OperationStep("Turning off Defender real-time protection...", _ => DefenderTweaks.DisableRealtime()));

            return steps;
        }

        private static List<OperationStep> BuildExtremeSteps()
        {
            return new List<OperationStep>
            {
                new OperationStep("Applying extreme service changes...", report =>
                    SystemGuard.DisableServicesSafely(ExtremeModeServices, (n, i, t) => report($"Adjusting services ({i}/{t}): {n}"))),
                new OperationStep("Setting power timeouts...", _ => PowerTweaks.ApplyNeverSleepOnAc()),
                new OperationStep("Turning off visual effects...", _ => RegistryTweaks.Apply(TweakCatalog.VisualEffects)),
                new OperationStep("Adjusting background apps...", _ => RegistryTweaks.Apply(TweakCatalog.BackgroundApps)),
                new OperationStep("Applying extreme registry tweaks...", _ => RegistryTweaks.Apply(TweakCatalog.ExtremeRegistry)),
                new OperationStep("Turning off common startup launchers...", _ => StartupTweaks.DisableCommonEntries()),
                new OperationStep("Applying memory tweaks...", _ => RegistryTweaks.Apply(TweakCatalog.ExtremeMemory)),
            };
        }

        private async Task ApplyOptimizationsAsync(OptimizationOptions options, bool extreme)
        {
            var (proceed, restoreNote) = await EnsureRestorePointAsync();
            if (!proceed) return;

            var steps = BuildRegularSteps(options);
            if (extreme) steps.AddRange(BuildExtremeSteps());

            var report = await RunStepsAsync(extreme ? "Applying Extreme Optimization" : "Optimizing System", steps);

            RecordRun(extreme ? "extreme" : "regular", report);
            CheckOptimizationStatus();
            CheckExtremeOptimizationStatus();
            RefreshOptimizationButtons();

            var sb = new StringBuilder();
            if (report.AllSucceeded)
            {
                sb.AppendLine(extreme
                    ? "Extreme optimization is complete. A restart is required for everything to take effect."
                    : "System optimization is complete. A restart is recommended.");
            }
            else
            {
                sb.AppendLine("Optimization finished, but some changes could not be applied, so it is NOT marked as optimized. You can retry it, or use Undo.");
            }

            if (!string.IsNullOrEmpty(restoreNote)) { sb.AppendLine(); sb.AppendLine(restoreNote); }

            string details = report.BuildSummary();
            if (details.Length > 0) { sb.AppendLine(); sb.AppendLine(details); }

            sb.AppendLine();
            sb.Append("System Restore, your network connection and your firewall were never touched.");

            AppDialog.Show(sb.ToString(),
                report.AllSucceeded ? "Optimization Complete" : "Optimization Finished With Problems",
                MessageBoxButtons.OK, report.AllSucceeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        // ---- Quick Boost / Battery -----------------------------------------------------

        private async void QuickBoostButton_Click(object? sender, EventArgs e)
        {
            var confirm = AppDialog.Show(
                "Quick Boost does two small things:" + Environment.NewLine + Environment.NewLine +
                "• Switches to the High performance power plan (if your PC has one)" + Environment.NewLine +
                "• Clears the DNS cache" + Environment.NewLine + Environment.NewLine +
                "Undo switches your power plan back. Continue?",
                "Quick Boost", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Quick Boost", async () =>
            {
                var steps = new List<OperationStep>
                {
                    new OperationStep("Switching to the High performance power plan...", _ => PowerTweaks.ApplyHighPerformance()),
                    new OperationStep("Clearing the DNS cache...", _ =>
                    {
                        var r = CommandRunner.Run("ipconfig.exe", "/flushdns", 30_000);
                        return r.Success ? StepResult.Success() : StepResult.Failure("DNS cache: " + r.Describe());
                    }),
                };

                var report = await RunStepsAsync("Quick Boost", steps);
                RecordRun("quick", report);
                ShowSimpleSummary("Quick Boost", report, "Quick Boost applied.");
            });
        }

        private async void BatteryOptimizeButton_Click(object? sender, EventArgs e)
        {
            var confirm = AppDialog.Show(
                "Apply battery optimization settings?" + Environment.NewLine + Environment.NewLine +
                "This will:" + Environment.NewLine +
                "• Switch to the Power Saver plan and cap the maximum processor state at 50%" + Environment.NewLine +
                "• Turn the screen off after 5 minutes and sleep after 10 minutes on battery" + Environment.NewLine +
                "• Turn on USB selective suspend and adaptive brightness on battery" + Environment.NewLine +
                "• Set Windows Search, Windows Update and SysMain to Manual" + Environment.NewLine + Environment.NewLine +
                "Every change is recorded, and Undo restores it.",
                "Battery Optimization", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Battery optimization", async () =>
            {
                var steps = new List<OperationStep>
                {
                    new OperationStep("Applying the Power Saver plan...", _ => PowerTweaks.ApplyPowerSaver()),
                    new OperationStep("Adjusting background services...", progress =>
                        SystemGuard.DisableServicesSafely(new[] { "WSearch", "wuauserv", "SysMain" }, (n, i, t) => progress($"Adjusting services ({i}/{t}): {n}"))),
                };

                var report = await RunStepsAsync("Optimizing for Battery Life", steps);
                RecordRun("battery", report);
                ShowSimpleSummary("Battery Optimization", report, "Battery optimization applied.");
            });
        }

        private static void ShowSimpleSummary(string title, OperationReport report, string successText)
        {
            var sb = new StringBuilder();
            sb.AppendLine(report.AllSucceeded ? successText : "Finished, but some changes could not be applied.");

            string details = report.BuildSummary();
            if (details.Length > 0) { sb.AppendLine(); sb.Append(details); }

            AppDialog.Show(sb.ToString().TrimEnd(), title, MessageBoxButtons.OK,
                report.AllSucceeded ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }

        // ---- Undo ----------------------------------------------------------------------

        private async void UndoOptimizationButton_Click(object? sender, EventArgs e)
        {
            var journal = ChangeJournal.Snapshot();

            if (!journal.HasRecordedChanges)
            {
                AppDialog.Show(
                    "Exoptimizer has no recorded changes to undo." + Environment.NewLine + Environment.NewLine +
                    "Changes made by versions before 3.2.0 were not recorded in detail. If something still looks wrong, use a System Restore point, or 'Repair Critical Services' for services and Defender policies.",
                    "Nothing to Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = AppDialog.Show(
                "Undo will put back what Exoptimizer changed, exactly as it was before:" + Environment.NewLine + Environment.NewLine +
                $"• {journal.Services.Count} service setting(s)" + Environment.NewLine +
                $"• {journal.Registry.Count} registry value(s)" + Environment.NewLine +
                $"• {journal.Power.Settings.Count + (journal.Power.OriginalActiveScheme != null ? 1 : 0)} power setting(s)" + Environment.NewLine +
                $"• {(journal.Network.AutoTuning != null ? 1 : 0) + (journal.Network.Rss != null ? 1 : 0)} network setting(s)" + Environment.NewLine +
                $"• {journal.AddedDefenderExclusions.Count} Defender exclusion(s) that Exoptimizer added" + Environment.NewLine + Environment.NewLine +
                "Continue?",
                "Undo Optimizations", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Undo optimizations", async () =>
            {
                var steps = new List<OperationStep>
                {
                    new OperationStep("Restoring services...", progress =>
                        SystemGuard.RestoreRecordedServices((n, i, t) => progress($"Restoring services ({i}/{t}): {n}"))),
                    new OperationStep("Restoring registry values...", _ => RegistryTweaks.RestoreAll()),
                    new OperationStep("Restoring power settings...", _ => PowerTweaks.RestoreAll()),
                    new OperationStep("Restoring network settings...", _ => NetworkTweaks.Restore()),
                    new OperationStep("Removing Defender exclusions that Exoptimizer added...", _ => DefenderTweaks.RemoveAddedExclusions()),
                };

                var report = await RunStepsAsync("Undoing Optimizations", steps);

                bool complete = !report.HasFailures && !ChangeJournal.Snapshot().HasRecordedChanges;
                if (complete)
                {
                    ChangeJournal.TryRecord(j => j.Runs.Clear(), out _);
                }

                CheckOptimizationStatus();
                CheckExtremeOptimizationStatus();
                RefreshOptimizationButtons();

                var sb = new StringBuilder();
                sb.AppendLine(complete
                    ? "Everything Exoptimizer changed has been put back. A restart is recommended."
                    : "Undo is incomplete: some changes could not be restored. They are still recorded, so you can try again.");

                string details = report.BuildSummary();
                if (details.Length > 0) { sb.AppendLine(); sb.Append(details); }

                AppDialog.Show(sb.ToString().TrimEnd(), complete ? "Undo Complete" : "Undo Incomplete",
                    MessageBoxButtons.OK, complete ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        // ---- Repair --------------------------------------------------------------------

        private async void RepairCriticalServicesButton_Click(object? sender, EventArgs e)
        {
            await RunOperationAsync("Repair critical services", async () =>
            {
                var plan = await Task.Run(() => SystemGuard.BuildRepairPlan());

                if (plan.IsEmpty)
                {
                    AppDialog.Show("Nothing needs repairing: the critical services and Defender policies already match how Windows ships them.",
                        "Repair Critical Services", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string message =
                    "This moves the services and policy values below back toward how Windows ships them. Your current values are saved first, in " +
                    System.IO.Path.Combine(ChangeJournal.DirectoryPath, "backups") + "." + Environment.NewLine + Environment.NewLine +
                    plan.Describe();

                if (plan.LikelyManaged)
                {
                    message += Environment.NewLine + Environment.NewLine +
                               "⚠ This PC looks managed by an organization (domain-joined or device-managed). These settings may be intentional, and your administrator may reverse the repair. Only continue if you are sure.";
                }

                message += Environment.NewLine + Environment.NewLine + "Continue?";

                var confirm = AppDialog.Show(message, "Repair Critical Services", MessageBoxButtons.YesNo,
                    plan.LikelyManaged ? MessageBoxIcon.Warning : MessageBoxIcon.Question,
                    plan.LikelyManaged ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);

                if (confirm != DialogResult.Yes) return;

                var steps = new List<OperationStep>
                {
                    new OperationStep("Repairing services and policies...", progress =>
                        SystemGuard.ApplyRepairPlan(plan, (n, i, t) => progress($"Repairing ({i}/{t}): {n}")))
                };

                var result = await RunStepsAsync("Repairing Critical Services", steps);
                ShowSimpleSummary("Repair Critical Services", result, "Critical services and policies were repaired. A restart is recommended.");
            });
        }

        // ---- Deep cleanup --------------------------------------------------------------

        private async void DeepCleanupButton_Click(object? sender, EventArgs e)
        {
            await RunOperationAsync("Deep System Cleanup", async () =>
            {
                var categories = CleanupService.BuildCategories();

                var scanDialog = new ProgressDialog("Deep System Cleanup", closable: false);
                scanDialog.Status = "Checking how much space each category uses...";
                scanDialog.Value = 30;
                scanDialog.Show(this);

                try
                {
                    await Task.Run(() =>
                    {
                        foreach (var category in categories) CleanupService.Scan(category);
                    });
                }
                finally
                {
                    scanDialog.Close();
                    scanDialog.Dispose();
                }

                using var chooser = new CleanupDialog(categories);
                if (chooser.ShowDialog(this) != DialogResult.OK) return;

                var chosen = chooser.SelectedCategories.ToList();
                if (chosen.Count == 0) return;

                var progress = new ProgressDialog("Deep System Cleanup", closable: false);
                progress.Status = "Starting...";
                progress.Show(this);

                CleanupResult? result = null;
                Exception? failure = null;

                try
                {
                    result = await Task.Run(() => CleanupService.Run(chosen, (text, percent) =>
                    {
                        if (progress.IsDisposed || !progress.IsHandleCreated) return;
                        try
                        {
                            progress.BeginInvoke(new Action(() =>
                            {
                                if (progress.IsDisposed) return;
                                progress.Status = text;
                                progress.Value = percent;
                            }));
                        }
                        catch { /* dialog closed */ }
                    }));
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    progress.Close();
                    progress.Dispose();
                }

                if (failure != null || result == null)
                {
                    AppDialog.Show("The cleanup stopped unexpectedly: " + (failure?.Message ?? "unknown error"),
                        "Deep System Cleanup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Cleanup finished. Freed {CleanupService.FormatBytes(result.BytesFreed)} by removing {result.FilesDeleted:N0} file(s).");
                if (result.FilesSkipped > 0)
                    sb.AppendLine($"{result.FilesSkipped:N0} item(s) were skipped because they are in use, protected, or links.");
                foreach (var note in result.Notes) { sb.AppendLine(); sb.AppendLine(note); }

                AppDialog.Show(sb.ToString().TrimEnd(), "Deep System Cleanup", MessageBoxButtons.OK,
                    result.Notes.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            });
        }

        // ---- VALORANT ------------------------------------------------------------------

        private const string QosPolicyName = "Exoptimizer VALORANT Priority";

        private async void ValorantEnableButton_Click(object? sender, EventArgs e)
        {
            await RunOperationAsync("VALORANT priority", async () =>
            {
                var processes = Process.GetProcessesByName("VALORANT-Win64-Shipping");

                if (processes.Length == 0)
                {
                    AppDialog.Show("VALORANT is not running!" + Environment.NewLine + Environment.NewLine + "Please start VALORANT first, then enable priority.",
                        "VALORANT Not Running", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int prioritised = 0;
                var problems = new List<string>();

                foreach (var process in processes)
                {
                    try
                    {
                        process.PriorityClass = ProcessPriorityClass.High;
                        prioritised++;
                    }
                    catch (Exception ex)
                    {
                        problems.Add("Process priority: " + ex.Message);
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                string script =
                    $"$n = {CommandRunner.PsQuote(QosPolicyName)}; " +
                    "if (Get-NetQosPolicy -Name $n -ErrorAction SilentlyContinue) { Remove-NetQosPolicy -Name $n -Confirm:$false }; " +
                    "New-NetQosPolicy -Name $n -AppPathNameMatchCondition 'VALORANT-Win64-Shipping.exe' -DSCPAction 46 | Out-Null";

                var qos = await Task.Run(() => CommandRunner.RunPowerShell(script, 60_000));
                bool qosOk = qos.Success;
                if (!qosOk) problems.Add("Network QoS policy: " + qos.Describe());

                valorantPriorityActive = prioritised > 0;

                if (valorantStatusLabel != null && !valorantStatusLabel.IsDisposed)
                {
                    valorantStatusLabel.Text = prioritised > 0 ? "Priority: Active (High)" : "Priority: could not be set";
                    valorantStatusLabel.ForeColor = prioritised > 0 ? SuccessColor : WarningColor;
                }

                if (valorantEnablePriority != null && !valorantEnablePriority.IsDisposed) valorantEnablePriority.Enabled = prioritised == 0;
                if (valorantDisablePriority != null && !valorantDisablePriority.IsDisposed) valorantDisablePriority.Enabled = prioritised > 0 || qosOk;

                if (problems.Count == 0)
                {
                    AppDialog.Show("VALORANT priority enabled!" + Environment.NewLine + Environment.NewLine + "Process priority set to High and the network QoS policy was added.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppDialog.Show((prioritised > 0 ? "VALORANT priority was only partly applied:" : "VALORANT priority could not be applied:") +
                                   Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "• " + p)),
                        "VALORANT Priority", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        private async void ValorantDisableButton_Click(object? sender, EventArgs e)
        {
            await RunOperationAsync("VALORANT priority", async () =>
            {
                var processes = Process.GetProcessesByName("VALORANT-Win64-Shipping");
                var problems = new List<string>();
                int running = processes.Length;

                foreach (var process in processes)
                {
                    try { process.PriorityClass = ProcessPriorityClass.Normal; }
                    catch (Exception ex) { problems.Add("Process priority: " + ex.Message); }
                    finally { process.Dispose(); }
                }

                string script =
                    $"$n = {CommandRunner.PsQuote(QosPolicyName)}; " +
                    "if (Get-NetQosPolicy -Name $n -ErrorAction SilentlyContinue) { Remove-NetQosPolicy -Name $n -Confirm:$false }";

                var qos = await Task.Run(() => CommandRunner.RunPowerShell(script, 60_000));
                if (!qos.Success) problems.Add("Network QoS policy: " + qos.Describe());

                valorantPriorityActive = false;

                if (valorantStatusLabel != null && !valorantStatusLabel.IsDisposed)
                {
                    valorantStatusLabel.Text = running > 0 ? "Priority: Normal" : "Priority: VALORANT not running";
                    valorantStatusLabel.ForeColor = TextSecondary;
                }

                if (valorantEnablePriority != null && !valorantEnablePriority.IsDisposed) valorantEnablePriority.Enabled = running > 0;
                if (valorantDisablePriority != null && !valorantDisablePriority.IsDisposed) valorantDisablePriority.Enabled = false;

                if (problems.Count == 0)
                {
                    AppDialog.Show("VALORANT priority disabled!" + Environment.NewLine + Environment.NewLine + "Process priority reset to Normal and the QoS policy removed.",
                        "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppDialog.Show("Priority was disabled, with problems:" + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, problems.Select(p => "• " + p)),
                        "VALORANT Priority", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }

        private async void StartVanguardButton_Click(object? sender, EventArgs e)
        {
            var confirm = AppDialog.Show(
                "Start Riot Vanguard service?" + Environment.NewLine + Environment.NewLine +
                "This will start the vgc service which is required for VALORANT anti-cheat." + Environment.NewLine +
                "You may need to restart VALORANT after starting Vanguard.",
                "Start Riot Vanguard", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            await RunOperationAsync("Start Vanguard", async () =>
            {
                var start = await Task.Run(() => CommandRunner.Run("sc.exe", "start vgc", 30_000));
                await Task.Delay(2000);
                UpdateProcessStatuses();

                bool? running = await Task.Run(() => SystemGuard.IsRunning("vgc"));

                if (running == true)
                {
                    AppDialog.Show("Riot Vanguard is running.", "Vanguard Start", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    AppDialog.Show(
                        "Riot Vanguard did not start" + (start.Success ? "." : $" ({start.Describe()}).") + Environment.NewLine + Environment.NewLine +
                        "You may need to:" + Environment.NewLine +
                        "• Restart your computer" + Environment.NewLine +
                        "• Reinstall VALORANT" + Environment.NewLine +
                        "• Check Windows services manually",
                        "Vanguard Start", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            });
        }
    }
}
