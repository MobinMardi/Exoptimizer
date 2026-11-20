using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Exoptimizer
{
    /// <summary>One registry value Exoptimizer sets as part of an optimization.</summary>
    internal sealed class RegTweak
    {
        public RegTweak(string hive, string subKey, string name, object value, RegistryValueKind kind)
        {
            Hive = hive;
            SubKey = subKey;
            Name = name;
            Value = value;
            Kind = kind;
        }

        public string Hive { get; }
        public string SubKey { get; }
        public string Name { get; }
        public object Value { get; }
        public RegistryValueKind Kind { get; }

        public string Describe() => $"{Hive}\\{SubKey}\\{Name}";
    }

    /// <summary>
    /// The complete list of registry values Exoptimizer is allowed to write. It doubles
    /// as the allow-list the recovery journal is validated against: a journal entry for
    /// anything not listed here is ignored, so a tampered journal cannot make Undo write
    /// arbitrary registry values.
    /// </summary>
    internal static class TweakCatalog
    {
        private const string Lm = "HKLM";
        private const string Cu = "HKCU";
        private const RegistryValueKind D = RegistryValueKind.DWord;
        private const RegistryValueKind S = RegistryValueKind.String;

        public const string StartupApprovedRunKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        private const string MemMgmt = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
        private const string DefenderRtp = @"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection";

        /// <summary>Normal optimization.</summary>
        public static readonly RegTweak[] Regular =
        {
            new(Lm, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, D),
            new(Lm, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, D),
            new(Lm, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, D),
        };

        /// <summary>The only security-related value Exoptimizer can write, and only when the user opts in.</summary>
        public static readonly RegTweak[] DefenderRealtime =
        {
            new(Lm, DefenderRtp, "DisableRealtimeMonitoring", 1, D),
        };

        public static readonly RegTweak[] VisualEffects =
        {
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, D),
            new(Cu, @"Control Panel\Desktop", "DragFullWindows", "0", S),
            new(Cu, @"Control Panel\Desktop", "MenuShowDelay", "0", S),
            new(Cu, @"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", S),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0, D),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, D),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewShadow", 0, D),
            new(Cu, @"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, D),
            new(Cu, @"Software\Microsoft\Windows\DWM", "AlwaysHibernateThumbnails", 0, D),
        };

        public static readonly RegTweak[] BackgroundApps =
        {
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, D),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, D),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, D),
            new(Cu, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, D),
        };

        /// <summary>Extreme mode, beyond what normal optimization already sets.</summary>
        public static readonly RegTweak[] ExtremeRegistry =
        {
            new(Lm, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, D),
            new(Cu, @"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, D),
            new(Cu, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, D),
            new(Cu, @"System\GameConfigStore", "GameDVR_Enabled", 0, D),
            new(Cu, @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, D),
            new(Lm, MemMgmt, "ClearPageFileAtShutdown", 0, D),
            new(Lm, MemMgmt, "DisablePagingExecutive", 1, D),
            new(Lm, MemMgmt, "LargeSystemCache", 0, D),
            new(Lm, @"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, D),
            new(Lm, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0, D),
        };

        public static readonly RegTweak[] ExtremeMemory =
        {
            new(Lm, MemMgmt, "IoPageLockLimit", 983040, D),
            new(Lm, @"SYSTEM\CurrentControlSet\Control\FileSystem", "ContigFileAllocSize", 1536, D),
            new(Lm, @"SYSTEM\CurrentControlSet\Control\FileSystem", "DisableNTFSLastAccessUpdate", 1, D),
            new(Lm, MemMgmt + @"\PrefetchParameters", "EnablePrefetcher", 0, D),
            new(Lm, MemMgmt + @"\PrefetchParameters", "EnableSuperfetch", 0, D),
        };

        private static readonly HashSet<string> KnownTargets = BuildKnownTargets();

        private static HashSet<string> BuildKnownTargets()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in new[] { Regular, DefenderRealtime, VisualEffects, BackgroundApps, ExtremeRegistry, ExtremeMemory })
            {
                foreach (var t in group) set.Add(Key(t.Hive, t.SubKey, t.Name));
            }
            return set;
        }

        private static string Key(string hive, string subKey, string name) => hive + "|" + subKey + "|" + name;

        /// <summary>True if Exoptimizer itself writes this value (including per-entry startup toggles).</summary>
        public static bool IsKnownTarget(string hive, string subKey, string name)
        {
            if (string.IsNullOrEmpty(hive) || string.IsNullOrEmpty(subKey) || string.IsNullOrEmpty(name)) return false;

            if (KnownTargets.Contains(Key(hive, subKey, name))) return true;

            return hive.Equals(Cu, StringComparison.OrdinalIgnoreCase) &&
                   subKey.Equals(StartupApprovedRunKey, StringComparison.OrdinalIgnoreCase) &&
                   name.Length <= 260;
        }
    }
}
