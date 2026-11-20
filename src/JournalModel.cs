using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Exoptimizer
{
    /// <summary>Rules for service names and start types. Everything read back from the recovery journal is checked against these before it is ever passed to a command.</summary>
    internal static class ServiceRules
    {
        private static readonly Regex NamePattern = new(@"^[A-Za-z0-9_.$\-]{1,256}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static readonly string[] StartTypes = { "auto", "delayed-auto", "demand", "disabled" };

        public static bool IsValidName(string? name) => !string.IsNullOrEmpty(name) && NamePattern.IsMatch(name);

        public static bool IsValidStartType(string? type) =>
            !string.IsNullOrEmpty(type) && StartTypes.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

        public static string NormalizeStartType(string type) => type.Trim().ToLowerInvariant();
    }

    internal static class GuidRules
    {
        private static readonly Regex Pattern = new(@"^[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsGuid(string? value) => !string.IsNullOrEmpty(value) && Pattern.IsMatch(value);
    }

    /// <summary>Converts registry values to and from the text stored in the journal.</summary>
    internal static class RegistryCodec
    {
        public static bool IsSupported(RegistryValueKind kind) =>
            kind == RegistryValueKind.DWord || kind == RegistryValueKind.String || kind == RegistryValueKind.Binary;

        public static bool TryEncode(RegistryValueKind kind, object? value, out string data)
        {
            data = string.Empty;
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    if (value is int i) { data = i.ToString(CultureInfo.InvariantCulture); return true; }
                    return false;
                case RegistryValueKind.String:
                    if (value is string s) { data = s; return true; }
                    return false;
                case RegistryValueKind.Binary:
                    if (value is byte[] b) { data = Convert.ToBase64String(b); return true; }
                    return false;
                default:
                    return false;
            }
        }

        public static bool TryDecode(RegistryValueKind kind, string? data, out object? value)
        {
            value = null;
            if (data == null) return false;

            switch (kind)
            {
                case RegistryValueKind.DWord:
                    if (int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) { value = i; return true; }
                    return false;
                case RegistryValueKind.String:
                    value = data;
                    return true;
                case RegistryValueKind.Binary:
                    try { value = Convert.FromBase64String(data); return true; }
                    catch (FormatException) { return false; }
                default:
                    return false;
            }
        }
    }

    internal sealed class ServiceEntry
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>auto, delayed-auto, demand or disabled - as it was before Exoptimizer changed it.</summary>
        public string StartType { get; set; } = string.Empty;
        public bool WasRunning { get; set; }
    }

    internal sealed class RegistryEntry
    {
        public string Hive { get; set; } = string.Empty;
        public string SubKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool KeyExisted { get; set; }
        public bool ValueExisted { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string Data { get; set; } = string.Empty;

        public bool Matches(string hive, string subKey, string name) =>
            Hive.Equals(hive, StringComparison.OrdinalIgnoreCase) &&
            SubKey.Equals(subKey, StringComparison.OrdinalIgnoreCase) &&
            Name.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class PowerSettingEntry
    {
        public string Scheme { get; set; } = string.Empty;
        public string Subgroup { get; set; } = string.Empty;
        public string Setting { get; set; } = string.Empty;
        public long? Ac { get; set; }
        public long? Dc { get; set; }

        public bool Matches(string scheme, string subgroup, string setting) =>
            Scheme.Equals(scheme, StringComparison.OrdinalIgnoreCase) &&
            Subgroup.Equals(subgroup, StringComparison.OrdinalIgnoreCase) &&
            Setting.Equals(setting, StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class PowerState
    {
        public string? OriginalActiveScheme { get; set; }
        public List<PowerSettingEntry> Settings { get; set; } = new();
    }

    internal sealed class NetworkState
    {
        public string? AutoTuning { get; set; }
        public string? Rss { get; set; }
    }

    internal sealed class RunRecord
    {
        /// <summary>regular, extreme, battery or quick.</summary>
        public string Mode { get; set; } = string.Empty;
        public string TimeUtc { get; set; } = string.Empty;
        public bool Succeeded { get; set; }
        public string FailedSteps { get; set; } = string.Empty;
    }

    /// <summary>
    /// Everything Exoptimizer has changed and how to put it back: the original value of
    /// every setting it touched, recorded before the change.
    /// </summary>
    internal sealed class JournalData
    {
        public int Version { get; set; } = 2;
        public List<ServiceEntry> Services { get; set; } = new();
        public List<RegistryEntry> Registry { get; set; } = new();
        public PowerState Power { get; set; } = new();
        public NetworkState Network { get; set; } = new();
        public List<string> AddedDefenderExclusions { get; set; } = new();
        public List<RunRecord> Runs { get; set; } = new();

        public int RecordedChangeCount =>
            Services.Count + Registry.Count + Power.Settings.Count + AddedDefenderExclusions.Count +
            (Power.OriginalActiveScheme != null ? 1 : 0) +
            (Network.AutoTuning != null ? 1 : 0) + (Network.Rss != null ? 1 : 0);

        public bool HasRecordedChanges => RecordedChangeCount > 0;

        /// <summary>
        /// Removes anything that doesn't look like something Exoptimizer itself would
        /// have written (bad names, protected services, unknown registry targets,
        /// malformed GUIDs...). Returns a description of each problem found.
        /// </summary>
        public List<string> Sanitize()
        {
            var problems = new List<string>();

            Services ??= new();
            Registry ??= new();
            Power ??= new();
            Power.Settings ??= new();
            Network ??= new();
            AddedDefenderExclusions ??= new();
            Runs ??= new();

            // Services
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Services.RemoveAll(s =>
            {
                if (s == null || !ServiceRules.IsValidName(s.Name))
                {
                    problems.Add("ignored a service entry with an invalid name");
                    return true;
                }
                if (SystemGuard.IsProtected(s.Name))
                {
                    problems.Add($"ignored an entry for protected service '{s.Name}'");
                    return true;
                }
                if (!ServiceRules.IsValidStartType(s.StartType))
                {
                    problems.Add($"ignored service '{s.Name}' with an invalid start type");
                    return true;
                }
                if (!seen.Add(s.Name))
                {
                    return true; // duplicate - the first recorded original wins
                }
                s.StartType = ServiceRules.NormalizeStartType(s.StartType);
                return false;
            });

            // Registry values
            Registry.RemoveAll(e =>
            {
                if (e == null) return true;

                bool hiveOk = e.Hive == "HKLM" || e.Hive == "HKCU";
                bool kindOk = Enum.TryParse<RegistryValueKind>(e.Kind, out var kind) && RegistryCodec.IsSupported(kind);
                if (!hiveOk || !kindOk || !TweakCatalog.IsKnownTarget(e.Hive, e.SubKey, e.Name))
                {
                    problems.Add($"ignored an unrecognised registry entry ({e.Hive}\\{e.SubKey}\\{e.Name})");
                    return true;
                }
                if (e.ValueExisted && !RegistryCodec.TryDecode(kind, e.Data, out _))
                {
                    problems.Add($"ignored a registry entry with unreadable data ({e.Name})");
                    return true;
                }
                return false;
            });

            // Power
            if (Power.OriginalActiveScheme != null && !GuidRules.IsGuid(Power.OriginalActiveScheme))
            {
                problems.Add("ignored an invalid power plan identifier");
                Power.OriginalActiveScheme = null;
            }
            Power.Settings.RemoveAll(p =>
            {
                if (p == null || !GuidRules.IsGuid(p.Scheme) || !GuidRules.IsGuid(p.Subgroup) || !GuidRules.IsGuid(p.Setting) ||
                    (p.Ac == null && p.Dc == null))
                {
                    problems.Add("ignored an invalid power setting entry");
                    return true;
                }
                return false;
            });

            // Network
            if (Network.AutoTuning != null && !OutputParsers.AutoTuningValues.Contains(Network.AutoTuning, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add("ignored an invalid TCP auto-tuning value");
                Network.AutoTuning = null;
            }
            if (Network.Rss != null && !OutputParsers.RssValues.Contains(Network.Rss, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add("ignored an invalid receive-side scaling value");
                Network.Rss = null;
            }

            // Defender exclusions we added
            AddedDefenderExclusions.RemoveAll(p =>
            {
                bool ok = !string.IsNullOrWhiteSpace(p) && p.Length <= 520 && p.IndexOfAny(Path.GetInvalidPathChars()) < 0 && Path.IsPathRooted(p);
                if (!ok) problems.Add("ignored an invalid Defender exclusion path");
                return !ok;
            });

            if (Runs.Count > 50) Runs.RemoveRange(0, Runs.Count - 50);
            Runs.RemoveAll(r => r == null);

            return problems;
        }
    }

    /// <summary>Language-independent parsers for the output of the Windows tools we call.</summary>
    internal static class OutputParsers
    {
        public static readonly string[] AutoTuningValues = { "disabled", "highlyrestricted", "restricted", "normal", "experimental" };
        public static readonly string[] RssValues = { "enabled", "disabled" };

        private static readonly Regex GuidAnywhere = new(@"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
        private static readonly Regex HexValue = new(@"0x[0-9a-fA-F]{8}", RegexOptions.Compiled);
        private static readonly Regex ScState = new(@"STATE\s*:\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AutoTuningLine = new(@"Auto-?Tuning Level\s*:\s*([A-Za-z]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex RssLine = new(@"Receive-?Side Scaling State\s*:\s*([A-Za-z]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>The first GUID in "powercfg /getactivescheme" output.</summary>
        public static string? ParseActiveSchemeGuid(string output)
        {
            var m = GuidAnywhere.Match(output ?? string.Empty);
            return m.Success ? m.Value.ToLowerInvariant() : null;
        }

        /// <summary>Every power plan GUID listed by "powercfg /list".</summary>
        public static HashSet<string> ParseSchemeGuids(string output)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in GuidAnywhere.Matches(output ?? string.Empty)) set.Add(m.Value.ToLowerInvariant());
            return set;
        }

        /// <summary>
        /// Reads the current AC and DC index from "powercfg /query" output. The two
        /// current values are always the last two hexadecimal numbers printed, which
        /// holds on every display language.
        /// </summary>
        public static bool TryParsePowerSettingIndexes(string output, out long ac, out long dc)
        {
            ac = 0;
            dc = 0;
            var matches = HexValue.Matches(output ?? string.Empty);
            if (matches.Count < 2) return false;

            try
            {
                ac = Convert.ToInt64(matches[matches.Count - 2].Value.Substring(2), 16);
                dc = Convert.ToInt64(matches[matches.Count - 1].Value.Substring(2), 16);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>True if "sc query" says RUNNING, false if it reports another state, null if the state couldn't be read.</summary>
        public static bool? ParseServiceRunning(string output)
        {
            var m = ScState.Match(output ?? string.Empty);
            if (!m.Success) return null;
            return m.Groups[1].Value == "4";
        }

        /// <summary>Reads auto-tuning and RSS from "netsh int tcp show global" (English output only; returns false otherwise).</summary>
        public static bool TryParseTcpGlobal(string output, out string? autoTuning, out string? rss)
        {
            autoTuning = null;
            rss = null;

            var a = AutoTuningLine.Match(output ?? string.Empty);
            if (a.Success) autoTuning = a.Groups[1].Value.ToLowerInvariant();

            var r = RssLine.Match(output ?? string.Empty);
            if (r.Success) rss = r.Groups[1].Value.ToLowerInvariant();

            return autoTuning != null && rss != null;
        }
    }
}
