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

        private static readonly string StateFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exoptimizer_service_state.json");

        public static bool IsProtected(string serviceName) =>
            !string.IsNullOrWhiteSpace(serviceName) && ProtectedServices.Contains(serviceName);

        /// <summary>
        /// Sets a service to start on demand (manual) rather than disabling it, and
        /// stops it if it's currently running. Protected services are skipped.
        /// The service's original start type is remembered the first time it's
        /// touched, so it can be restored exactly later.
        /// </summary>
        public static void DisableServiceSafely(string serviceName, bool alsoStop = true)
        {
            if (string.IsNullOrWhiteSpace(serviceName)) return;

            if (IsProtected(serviceName))
            {
                Debug.WriteLine($"Exoptimizer: refused to touch protected service '{serviceName}'.");
                return;
            }

            RememberOriginalState(serviceName);

            // "demand" (manual start), not "disabled": Windows - or a repair
            // tool, or the service's own dependents - can still start the
            // service if it's genuinely needed. It just won't auto-start at
            // boot, which is what actually saves the resources this feature
            // is for.
            RunSc($"config \"{serviceName}\" start= demand");
            if (alsoStop)
            {
                RunSc($"stop \"{serviceName}\"");
            }
        }

        /// <summary>Disables a batch of services, reporting progress as it goes. Protected names are silently skipped.</summary>
        public static void DisableServicesSafely(IEnumerable<string> serviceNames, Action<string, int, int>? onProgress = null, bool alsoStop = true)
        {
            var list = serviceNames
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < list.Count; i++)
            {
                onProgress?.Invoke(list[i], i + 1, list.Count);
                DisableServiceSafely(list[i], alsoStop);
            }
        }

        /// <summary>
        /// Restores every service Exoptimizer has ever touched back to the start
        /// type it had before Exoptimizer changed it, then clears the tracking
        /// file so the next optimization pass starts from a clean slate.
        /// </summary>
        public static void RestoreAllTrackedServices(Action<string, int, int>? onProgress = null)
        {
            var state = LoadState();
            if (state.Count == 0) return;

            var entries = state.ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                var (name, originalStartType) = (entries[i].Key, entries[i].Value);
                onProgress?.Invoke(name, i + 1, entries.Count);

                RunSc($"config \"{name}\" start= {originalStartType}");
                if (string.Equals(originalStartType, "auto", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(originalStartType, "demand", StringComparison.OrdinalIgnoreCase))
                {
                    RunSc($"start \"{name}\"");
                }
            }

            try { File.Delete(StateFilePath); } catch { /* best effort */ }
        }

        /// <summary>
        /// One-click fix for machines that already have critical services stuck
        /// disabled from an older version of Exoptimizer (or another tool).
        /// Resets each critical service to the start type Windows ships with and
        /// starts it, and hands Windows Defender back its own default policy.
        /// </summary>
        public static void RepairCriticalServices(Action<string, int, int>? onProgress = null)
        {
            for (int i = 0; i < RepairTargets.Length; i++)
            {
                var (name, startType) = RepairTargets[i];
                onProgress?.Invoke(name, i + 1, RepairTargets.Length);

                RunSc($"config \"{name}\" start= {startType}");
                RunSc($"start \"{name}\"");
            }

            TryDeleteRegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware");
            TryDeleteRegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring");
            TryDeleteRegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableBehaviorMonitoring");
            TryDeleteRegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableOnAccessProtection");
            TryDeleteRegistryValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableScanOnRealtimeEnable");
        }

        /// <summary>Current start type of a service ("auto", "demand", "disabled", or "unknown"), used by the status indicators in the UI.</summary>
        public static string GetServiceStartType(string serviceName)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = $"qc \"{serviceName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };

                using var process = Process.Start(psi);
                if (process == null) return "unknown";

                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (output.Contains("DISABLED")) return "disabled";
                if (output.Contains("DEMAND_START")) return "demand";
                if (output.Contains("AUTO_START")) return "auto";
                return "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static void RememberOriginalState(string serviceName)
        {
            var state = LoadState();
            if (state.ContainsKey(serviceName)) return; // keep the first-ever recorded original

            string current = GetServiceStartType(serviceName);
            if (current == "unknown") return; // don't record something we can't be sure of

            state[serviceName] = current;
            SaveState(state);
        }

        private static Dictionary<string, string> LoadState()
        {
            try
            {
                if (File.Exists(StateFilePath))
                {
                    string json = File.ReadAllText(StateFilePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null) return loaded;
                }
            }
            catch { /* start fresh if the file is missing or corrupt */ }

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static void SaveState(Dictionary<string, string> state)
        {
            try
            {
                string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StateFilePath, json);
            }
            catch { /* non-fatal: worst case, that one service can't be auto-restored later */ }
        }

        private static void TryDeleteRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    keyPath.Replace("HKEY_LOCAL_MACHINE\\", string.Empty), writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            }
            catch { /* best effort */ }
        }

        private static void RunSc(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "sc",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
            }
            catch { /* best effort - a single service failing to change should never abort the batch */ }
        }
    }
}
