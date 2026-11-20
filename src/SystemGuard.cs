using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace Exoptimizer
{
    /// <summary>
    /// Central safety layer for every Windows service change Exoptimizer makes.
    ///
    /// Versions before 3.0.0 called "sc config &lt;service&gt; start= disabled" directly
    /// from several places in the app, including on services like VSS/swprv
    /// (System Restore), nsi/NlaSvc/iphlpsvc/RasMan (the network stack) and
    /// Winmgmt/Schedule/EventLog (core Windows plumbing). Once a service's start
    /// type is set to "Disabled", Windows itself can no longer start it - which
    /// is exactly why restore points stopped working, why the network adapter
    /// service couldn't start, and why running System Restore didn't fix
    /// anything (System Restore itself depends on VSS being disabled).
    ///
    /// Nothing in the app should call "sc config"/"sc stop" on a service
    /// directly anymore - everything goes through this class so a protected
    /// service can never be touched, every change is set to "Manual" (start on
    /// demand) instead of "Disabled" so Windows can still start it if something
    /// genuinely needs it, and every change is recorded so it can be undone
    /// exactly, not just guessed at.
    /// </summary>
    internal static class SystemGuard
    {
        /// <summary>
        /// Services Exoptimizer will never disable or stop, no matter which
        /// preset calls for it. Grouped by why they're protected.
        /// </summary>
        public static readonly HashSet<string> ProtectedServices = new(StringComparer.OrdinalIgnoreCase)
        {
            // System Restore / Volume Shadow Copy - restore points can be
            // neither created nor applied once these are disabled. This is
            // the direct cause of "restore point service can't start" and
            // "can't use restore points" from older versions.
            "VSS", "swprv",

            // Core plumbing that a very large share of Windows - including
            // System Restore, Windows Update, and most background services -
            // quietly depends on.
            "Winmgmt",          // Windows Management Instrumentation
            "RpcSs",            // Remote Procedure Call
            "RpcEptMapper",
            "DcomLaunch",
            "Schedule",         // Task Scheduler
            "EventLog",         // Windows Event Log
            "gpsvc",            // Group Policy Client - Microsoft explicitly warns never to disable this
            "PlugPlay",
            "W32Time",          // Windows Time - drift here silently breaks HTTPS/auth over time

            // Networking stack - disabling any of these is what breaks
            // "Network connection service can't start".
            "nsi",              // Network Store Interface Service
            "Dhcp",
            "Dnscache",
            "NlaSvc",           // Network Location Awareness
            "netprofm",         // Network List Service
            "iphlpsvc",         // IP Helper
            "RasMan",
            "LanmanWorkstation",
            "LanmanServer",
            "WinHttpAutoProxySvc",
            "SharedAccess",     // Internet Connection Sharing / mobile hotspot

            // Security baseline - an optimizer should never be able to leave
            // the machine with no firewall or no antivirus coverage.
            "MpsSvc",           // Windows Defender Firewall
            "BFE",              // Base Filtering Engine
            "WinDefend",
            "WdFilter",
            "WdBoot",
            "WdNisDrv",
            "WdNisSvc",
            "Sense",
        };

        /// <summary>Critical services worth offering a one-click repair for, with the start type Windows ships them with.</summary>
        public static readonly (string Name, string DefaultStartType)[] RepairTargets =
        {
            ("VSS", "demand"),
            ("swprv", "demand"),
            ("Winmgmt", "auto"),
            ("RpcSs", "auto"),
            ("Schedule", "auto"),
            ("EventLog", "auto"),
            ("gpsvc", "auto"),
            ("W32Time", "demand"),
            ("nsi", "auto"),
            ("Dhcp", "auto"),
            ("Dnscache", "auto"),
            ("NlaSvc", "auto"),
            ("netprofm", "demand"),
            ("iphlpsvc", "auto"),
            ("RasMan", "demand"),
            ("LanmanWorkstation", "auto"),
            ("LanmanServer", "auto"),
            ("WinHttpAutoProxySvc", "demand"),
            ("SharedAccess", "demand"),
            ("MpsSvc", "auto"),
            ("BFE", "auto"),
            ("WinDefend", "auto"),
        };

        public static bool IsProtected(string? serviceName) =>
            !string.IsNullOrWhiteSpace(serviceName) && ProtectedServices.Contains(serviceName);

        // ---- Reading service state -----------------------------------------------------

        public enum ServiceReadStatus
        {
            Ok,
            NotInstalled,
            Unsupported,
            Error
        }

        public readonly struct ServiceConfigRead
        {
            public ServiceConfigRead(ServiceReadStatus status, string startType, string detail)
            {
                Status = status;
                StartType = startType;
                Detail = detail;
            }

            public ServiceReadStatus Status { get; }
            public string StartType { get; }
            public string Detail { get; }
        }

        /// <summary>Reads a service's start type straight from the registry (auto, delayed-auto, demand or disabled).</summary>
        public static ServiceConfigRead ReadServiceConfig(string name)
        {
            if (!ServiceRules.IsValidName(name))
                return new ServiceConfigRead(ServiceReadStatus.Error, string.Empty, "invalid service name");

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, false);
                if (key == null)
                    return new ServiceConfigRead(ServiceReadStatus.NotInstalled, string.Empty, "not installed on this PC");

                if (key.GetValue("Type") is not int type || (type & 0x30) == 0)
                    return new ServiceConfigRead(ServiceReadStatus.Unsupported, string.Empty, "not a Windows service");

                if (key.GetValue("Start") is not int start)
                    return new ServiceConfigRead(ServiceReadStatus.Unsupported, string.Empty, "start type could not be read");

                int delayed = key.GetValue("DelayedAutostart") is int d ? d : 0;

                string? mapped = start switch
                {
                    2 => delayed == 1 ? "delayed-auto" : "auto",
                    3 => "demand",
                    4 => "disabled",
                    _ => null
                };

                if (mapped == null)
                    return new ServiceConfigRead(ServiceReadStatus.Unsupported, string.Empty, $"start type {start} is not managed by Exoptimizer");

                return new ServiceConfigRead(ServiceReadStatus.Ok, mapped, string.Empty);
            }
            catch (Exception ex)
            {
                return new ServiceConfigRead(ServiceReadStatus.Error, string.Empty, ex.Message);
            }
        }

        /// <summary>True/false if the service is running/not running; null if that couldn't be determined.</summary>
        public static bool? IsRunning(string name)
        {
            if (!ServiceRules.IsValidName(name)) return null;

            var result = CommandRunner.Run("sc.exe", $"query \"{name}\"", 30_000);
            if (!result.Started || result.TimedOut) return null;
            return OutputParsers.ParseServiceRunning(result.Output);
        }

        // ---- Changing services (always recorded first) ---------------------------------

        public static StepResult DisableServicesSafely(IEnumerable<string> serviceNames, Action<string, int, int>? onProgress = null, bool alsoStop = true)
        {
            var result = new StepResult();
            var list = serviceNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < list.Count; i++)
            {
                onProgress?.Invoke(list[i], i + 1, list.Count);
                DisableOne(list[i], alsoStop, result);
            }

            return result;
        }

        private static void DisableOne(string name, bool alsoStop, StepResult result)
        {
            if (!ServiceRules.IsValidName(name)) { result.Fail(name, "invalid service name"); return; }
            if (IsProtected(name)) { result.Skip(name, "protected - never changed"); return; }

            var read = ReadServiceConfig(name);
            if (read.Status == ServiceReadStatus.NotInstalled) { result.Skip(name, "not installed"); return; }
            if (read.Status == ServiceReadStatus.Unsupported) { result.Skip(name, read.Detail); return; }
            if (read.Status == ServiceReadStatus.Error) { result.Fail(name, "couldn't read its configuration (" + read.Detail + ")"); return; }

            if (read.StartType == "disabled") { result.Skip(name, "already disabled by you or Windows - left alone"); return; }

            bool alreadyRecorded = ChangeJournal.Snapshot().Services.Exists(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (read.StartType == "demand")
            {
                if (alreadyRecorded) result.Ok(name);
                else result.Skip(name, "already set to manual");
                return;
            }

            // Capture the running state; if it can't be read, don't change the service.
            bool? running = IsRunning(name);
            if (running == null) { result.Fail(name, "couldn't read its running state; left unchanged"); return; }

            if (!alreadyRecorded)
            {
                var entry = new ServiceEntry { Name = name, StartType = read.StartType, WasRunning = running.Value };
                if (!ChangeJournal.TryRecord(j => j.Services.Add(entry), out string error))
                {
                    result.Fail(name, "couldn't save recovery data (" + error + "); left unchanged");
                    return;
                }
            }

            var config = CommandRunner.Run("sc.exe", $"config \"{name}\" start= demand", 30_000);
            if (!config.Success)
            {
                // Exit code 5 = access denied: Windows itself protects a few services (for example the Update Medic service).
                if (config.ExitCode == 5) result.Skip(name, "Windows does not allow this service to be changed");
                else result.Fail(name, config.Describe());
                return;
            }

            var verify = ReadServiceConfig(name);
            if (verify.Status != ServiceReadStatus.Ok || verify.StartType != "demand")
            {
                result.Fail(name, "the change did not take effect");
                return;
            }

            if (alsoStop && running.Value)
            {
                var stop = CommandRunner.Run("sc.exe", $"stop \"{name}\"", 30_000);
                if (!stop.Success) result.Note($"{name}: set to manual but could not be stopped ({stop.Describe()})");
            }

            result.Ok(name);
        }

        /// <summary>Restores every service Exoptimizer recorded, to its recorded start type and running state.</summary>
        public static StepResult RestoreRecordedServices(Action<string, int, int>? onProgress = null)
        {
            var result = new StepResult();
            var entries = ChangeJournal.Snapshot().Services;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                onProgress?.Invoke(e.Name, i + 1, entries.Count);

                string type = ServiceRules.NormalizeStartType(e.StartType);

                var current = ReadServiceConfig(e.Name);
                if (current.Status == ServiceReadStatus.NotInstalled)
                {
                    Forget(e.Name, result);
                    result.Skip(e.Name, "no longer installed");
                    continue;
                }

                var config = CommandRunner.Run("sc.exe", $"config \"{e.Name}\" start= {type}", 30_000);
                if (!config.Success) { result.Fail(e.Name, config.Describe()); continue; }

                var verify = ReadServiceConfig(e.Name);
                if (verify.Status != ServiceReadStatus.Ok || verify.StartType != type)
                {
                    result.Fail(e.Name, "the restored start type did not take effect");
                    continue;
                }

                if (e.WasRunning && type != "disabled")
                {
                    var start = CommandRunner.Run("sc.exe", $"start \"{e.Name}\"", 30_000);
                    if (!start.Success && IsRunning(e.Name) != true)
                        result.Note($"{e.Name}: start type restored, but it could not be started ({start.Describe()})");
                }

                Forget(e.Name, result);
                result.Ok(e.Name);
            }

            return result;
        }

        private static void Forget(string name, StepResult result)
        {
            if (!ChangeJournal.TryRecord(j => j.Services.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)), out string error))
                result.Note($"{name}: restored, but the recovery record could not be cleared ({error})");
        }

        // ---- Repair --------------------------------------------------------------------

        private static readonly (string KeyPath, string ValueName)[] DefenderPolicyValues =
        {
            (@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware"),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring"),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableBehaviorMonitoring"),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableOnAccessProtection"),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableScanOnRealtimeEnable"),
        };

        public sealed class RepairServiceChange
        {
            public string Name { get; set; } = string.Empty;
            public string CurrentType { get; set; } = string.Empty;
            public string TargetType { get; set; } = string.Empty;
            public bool CurrentlyRunning { get; set; }
            public bool NeedsStart { get; set; }
            public bool NeedsConfig { get; set; }
        }

        public sealed class RepairPolicyChange
        {
            public string KeyPath { get; set; } = string.Empty;
            public string ValueName { get; set; } = string.Empty;
            public string CurrentValue { get; set; } = string.Empty;
        }

        public sealed class RepairPlan
        {
            public List<RepairServiceChange> Services { get; } = new();
            public List<RepairPolicyChange> Policies { get; } = new();
            public bool LikelyManaged { get; set; }
            public bool IsEmpty => Services.Count == 0 && Policies.Count == 0;

            public string Describe()
            {
                var sb = new System.Text.StringBuilder();

                if (Services.Count > 0)
                {
                    sb.AppendLine("Services that will be changed:");
                    foreach (var s in Services)
                    {
                        var parts = new List<string>();
                        if (s.NeedsConfig) parts.Add($"start type {s.CurrentType} → {s.TargetType}");
                        if (s.NeedsStart) parts.Add("will be started");
                        sb.AppendLine($"  • {s.Name}: {string.Join(", ", parts)}");
                    }
                }

                if (Policies.Count > 0)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("Windows Defender policy values that will be removed:");
                    foreach (var p in Policies) sb.AppendLine($"  • {p.ValueName} (currently {p.CurrentValue})");
                }

                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>Works out exactly what a repair would change on this PC, without changing anything.</summary>
        public static RepairPlan BuildRepairPlan()
        {
            var plan = new RepairPlan { LikelyManaged = IsLikelyManaged() };

            foreach (var (name, defaultType) in RepairTargets)
            {
                var read = ReadServiceConfig(name);
                if (read.Status != ServiceReadStatus.Ok) continue;

                bool typeMatches = read.StartType == defaultType ||
                                   (defaultType == "auto" && read.StartType == "delayed-auto");

                bool running = IsRunning(name) == true;
                bool needsStart = defaultType == "auto" && !running;

                if (!typeMatches || needsStart)
                {
                    plan.Services.Add(new RepairServiceChange
                    {
                        Name = name,
                        CurrentType = read.StartType,
                        TargetType = defaultType,
                        CurrentlyRunning = running,
                        NeedsConfig = !typeMatches,
                        NeedsStart = needsStart
                    });
                }
            }

            foreach (var (keyPath, valueName) in DefenderPolicyValues)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath, false);
                    object? value = key?.GetValue(valueName);
                    if (value != null)
                    {
                        plan.Policies.Add(new RepairPolicyChange { KeyPath = keyPath, ValueName = valueName, CurrentValue = value.ToString() ?? string.Empty });
                    }
                }
                catch
                {
                    // unreadable - treated as not present
                }
            }

            return plan;
        }

        /// <summary>Applies a repair plan: backs up what it will change first, then changes it and verifies each item.</summary>
        public static StepResult ApplyRepairPlan(RepairPlan plan, Action<string, int, int>? onProgress = null)
        {
            var result = new StepResult();

            // Back up the current values first. If that fails, change nothing.
            try
            {
                string path = ChangeJournal.WriteBackup("repair-backup", new
                {
                    CreatedAt = DateTime.Now.ToString("s"),
                    Services = plan.Services,
                    DefenderPolicyValues = plan.Policies
                });
                result.Note("Previous values were saved to " + path);
            }
            catch (Exception ex)
            {
                result.Fail("Backup", "couldn't save the current values first (" + ex.Message + "); nothing was changed");
                return result;
            }

            int total = plan.Services.Count + plan.Policies.Count;
            int done = 0;

            foreach (var s in plan.Services)
            {
                onProgress?.Invoke(s.Name, ++done, total);

                if (!ServiceRules.IsValidName(s.Name)) { result.Fail(s.Name, "invalid service name"); continue; }

                if (s.NeedsConfig)
                {
                    var config = CommandRunner.Run("sc.exe", $"config \"{s.Name}\" start= {s.TargetType}", 30_000);
                    if (!config.Success) { result.Fail(s.Name, config.Describe()); continue; }

                    var verify = ReadServiceConfig(s.Name);
                    if (verify.Status != ServiceReadStatus.Ok || verify.StartType != s.TargetType)
                    {
                        result.Fail(s.Name, "the start type change did not take effect");
                        continue;
                    }
                }

                if (s.NeedsStart)
                {
                    var start = CommandRunner.Run("sc.exe", $"start \"{s.Name}\"", 30_000);
                    if (!start.Success && IsRunning(s.Name) != true)
                    {
                        result.Fail(s.Name, "start type fixed, but the service could not be started (" + start.Describe() + ")");
                        continue;
                    }
                }

                result.Ok(s.Name);
            }

            foreach (var p in plan.Policies)
            {
                onProgress?.Invoke(p.ValueName, ++done, total);

                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(p.KeyPath, true);
                    key?.DeleteValue(p.ValueName, false);

                    using var check = Registry.LocalMachine.OpenSubKey(p.KeyPath, false);
                    if (check?.GetValue(p.ValueName) != null) result.Fail(p.ValueName, "the value is still present");
                    else result.Ok(p.ValueName);
                }
                catch (Exception ex)
                {
                    result.Fail(p.ValueName, ex.Message);
                }
            }

            return result;
        }

        /// <summary>
        /// A best-effort check for a PC that an organisation manages (domain-joined or MDM-enrolled),
        /// where service and policy settings may be intentional.
        /// </summary>
        public static bool IsLikelyManaged()
        {
            try
            {
                using (var tcp = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", false))
                {
                    if (tcp?.GetValue("Domain") is string domain && !string.IsNullOrWhiteSpace(domain)) return true;
                }

                using (var enrollments = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Enrollments", false))
                {
                    if (enrollments != null)
                    {
                        foreach (var sub in enrollments.GetSubKeyNames())
                        {
                            using var e = enrollments.OpenSubKey(sub, false);
                            if (e?.GetValue("ProviderID") is string provider && !string.IsNullOrWhiteSpace(provider)) return true;
                        }
                    }
                }
            }
            catch
            {
                // can't tell - assume not managed
            }

            return false;
        }
    }
}
