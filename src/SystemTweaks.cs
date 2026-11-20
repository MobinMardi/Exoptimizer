using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace Exoptimizer
{
    /// <summary>
    /// Registry changes with a guarantee: the original value (or its absence) is recorded
    /// durably before anything is written, every write is read back to confirm it took
    /// effect, and Undo restores exactly what was recorded.
    /// </summary>
    internal static class RegistryTweaks
    {
        private static RegistryKey? Root(string hive) => hive switch
        {
            "HKLM" => Registry.LocalMachine,
            "HKCU" => Registry.CurrentUser,
            _ => null
        };

        public static StepResult Apply(IEnumerable<RegTweak> tweaks)
        {
            var result = new StepResult();
            foreach (var tweak in tweaks) ApplyOne(tweak, result);
            return result;
        }

        private static void ApplyOne(RegTweak t, StepResult result)
        {
            string label = t.Describe();
            var root = Root(t.Hive);
            if (root == null) { result.Fail(label, "unknown registry hive"); return; }

            if (!RegistryCodec.TryEncode(t.Kind, t.Value, out string targetData))
            {
                result.Fail(label, "unsupported value type");
                return;
            }

            bool keyExisted;
            bool valueExisted = false;
            string kindName = string.Empty;
            string data = string.Empty;

            try
            {
                using var key = root.OpenSubKey(t.SubKey, false);
                keyExisted = key != null;

                if (key != null && key.GetValueNames().Contains(t.Name, StringComparer.OrdinalIgnoreCase))
                {
                    valueExisted = true;
                    var kind = key.GetValueKind(t.Name);
                    object? raw = key.GetValue(t.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

                    if (!RegistryCodec.TryEncode(kind, raw, out data))
                    {
                        result.Fail(label, $"the existing value has a type ({kind}) Exoptimizer doesn't handle; left unchanged");
                        return;
                    }
                    kindName = kind.ToString();
                }
            }
            catch (Exception ex)
            {
                result.Fail(label, "couldn't read the current value (" + ex.Message + ")");
                return;
            }

            bool sameAsTarget = valueExisted && kindName == t.Kind.ToString() && data == targetData;
            bool recorded = ChangeJournal.Snapshot().Registry.Exists(e => e.Matches(t.Hive, t.SubKey, t.Name));

            if (sameAsTarget && !recorded)
            {
                result.Skip(label, "already set");
                return;
            }

            if (!recorded)
            {
                var entry = new RegistryEntry
                {
                    Hive = t.Hive,
                    SubKey = t.SubKey,
                    Name = t.Name,
                    KeyExisted = keyExisted,
                    ValueExisted = valueExisted,
                    Kind = valueExisted ? kindName : t.Kind.ToString(),
                    Data = valueExisted ? data : string.Empty
                };

                if (!ChangeJournal.TryRecord(j => j.Registry.Add(entry), out string error))
                {
                    result.Fail(label, "couldn't save recovery data (" + error + "); left unchanged");
                    return;
                }
            }

            if (sameAsTarget)
            {
                result.Ok(label);
                return;
            }

            try
            {
                using var key = root.CreateSubKey(t.SubKey, true);
                key.SetValue(t.Name, t.Value, t.Kind);
            }
            catch (Exception ex)
            {
                result.Fail(label, ex.Message);
                return;
            }

            if (!Matches(root, t.SubKey, t.Name, t.Kind, targetData))
            {
                result.Fail(label, "the value did not stick (blocked by a policy or security software?)");
                return;
            }

            result.Ok(label);
        }

        private static bool Matches(RegistryKey root, string subKey, string name, RegistryValueKind kind, string expectedData)
        {
            try
            {
                using var key = root.OpenSubKey(subKey, false);
                if (key == null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
                if (key.GetValueKind(name) != kind) return false;

                object? raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                return RegistryCodec.TryEncode(kind, raw, out string actual) && actual == expectedData;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Restores every recorded registry value to what it was before Exoptimizer changed it.</summary>
        public static StepResult RestoreAll()
        {
            var result = new StepResult();
            foreach (var entry in ChangeJournal.Snapshot().Registry)
            {
                RestoreEntry(entry, result);
            }
            return result;
        }

        /// <summary>Restores a single recorded value, if there is a record for it.</summary>
        public static StepResult Revert(string hive, string subKey, string name)
        {
            var result = new StepResult();
            var entry = ChangeJournal.Snapshot().Registry.FirstOrDefault(e => e.Matches(hive, subKey, name));
            if (entry == null)
            {
                result.Skip($"{hive}\\{subKey}\\{name}", "nothing recorded");
                return result;
            }
            RestoreEntry(entry, result);
            return result;
        }

        private static void RestoreEntry(RegistryEntry e, StepResult result)
        {
            string label = $"{e.Hive}\\{e.SubKey}\\{e.Name}";
            var root = Root(e.Hive);
            if (root == null) { result.Fail(label, "unknown registry hive"); return; }

            try
            {
                if (e.ValueExisted)
                {
                    if (!Enum.TryParse<RegistryValueKind>(e.Kind, out var kind) || !RegistryCodec.TryDecode(kind, e.Data, out object? value) || value == null)
                    {
                        result.Fail(label, "the recorded value is unreadable");
                        return;
                    }

                    using (var key = root.CreateSubKey(e.SubKey, true))
                    {
                        key.SetValue(e.Name, value, kind);
                    }

                    if (!Matches(root, e.SubKey, e.Name, kind, e.Data))
                    {
                        result.Fail(label, "the original value could not be put back");
                        return;
                    }
                }
                else
                {
                    using (var key = root.OpenSubKey(e.SubKey, true))
                    {
                        key?.DeleteValue(e.Name, false);
                    }

                    using (var check = root.OpenSubKey(e.SubKey, false))
                    {
                        if (check != null && check.GetValueNames().Contains(e.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            result.Fail(label, "the value could not be removed");
                            return;
                        }
                    }

                    if (!e.KeyExisted) TryDeleteEmptyKey(root, e.SubKey);
                }

                if (!ChangeJournal.TryRecord(j => j.Registry.RemoveAll(r => r.Matches(e.Hive, e.SubKey, e.Name)), out string error))
                {
                    result.Note($"{label}: restored, but the recovery record could not be cleared ({error})");
                }

                result.Ok(label);
            }
            catch (Exception ex)
            {
                result.Fail(label, ex.Message);
            }
        }

        private static void TryDeleteEmptyKey(RegistryKey root, string subKey)
        {
            try
            {
                int slash = subKey.LastIndexOf('\\');
                if (slash <= 0) return;

                string parentPath = subKey.Substring(0, slash);
                string leafName = subKey.Substring(slash + 1);

                bool empty;
                using (var leaf = root.OpenSubKey(subKey, false))
                {
                    empty = leaf != null && leaf.ValueCount == 0 && leaf.SubKeyCount == 0;
                }

                if (!empty) return;

                using var parent = root.OpenSubKey(parentPath, true);
                parent?.DeleteSubKey(leafName, false);
            }
            catch
            {
                // leaving an empty key behind is harmless
            }
        }
    }

    /// <summary>Power plan changes, recorded and verified like every other change.</summary>
    internal static class PowerTweaks
    {
        public const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string PowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";

        private const string SubProcessor = "54533251-82be-4824-96c1-47b60b740d00";
        private const string SettingMaxProcessor = "bc5038f7-23e0-4960-96da-33abaf5935ec";
        private const string SubDisplay = "7516b95f-f776-4464-8c53-06167f40cc99";
        private const string SettingDisplayTimeout = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";
        private const string SettingAdaptiveBrightness = "fbd9aa66-9553-4097-ba44-ed6e9d65eab8";
        private const string SubSleep = "238c9fa8-0aad-41ed-83f4-97be242c8f20";
        private const string SettingStandbyTimeout = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da";
        private const string SettingHibernateTimeout = "9d7815a6-7ee4-497e-8888-515a05f02364";
        private const string SubDisk = "0012ee47-9041-4b5d-9b77-535fba8b1442";
        private const string SettingDiskTimeout = "6738e2c4-e8a5-4a42-b16a-e040e769756e";
        private const string SubUsb = "2a737441-1930-4402-8d77-b2bebba308a3";
        private const string SettingUsbSelectiveSuspend = "48e6b7a6-50f5-4782-a5d4-53bb8f07e226";

        public static string? GetActiveScheme()
        {
            var r = CommandRunner.Run("powercfg.exe", "/getactivescheme", 30_000);
            return r.Success ? OutputParsers.ParseActiveSchemeGuid(r.Output) : null;
        }

        public static HashSet<string> ListSchemes()
        {
            var r = CommandRunner.Run("powercfg.exe", "/list", 30_000);
            return r.Success ? OutputParsers.ParseSchemeGuids(r.Output) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Switches the active plan, recording the plan that was active before the first switch.</summary>
        private static void Activate(string guid, string label, StepResult result)
        {
            string? current = GetActiveScheme();
            if (current == null) { result.Fail(label, "couldn't read the active power plan"); return; }
            if (current.Equals(guid, StringComparison.OrdinalIgnoreCase)) { result.Skip(label, "already active"); return; }
            if (!ListSchemes().Contains(guid)) { result.Skip(label, "not available on this PC"); return; }

            if (!ChangeJournal.TryRecord(j => { j.Power.OriginalActiveScheme ??= current; }, out string error))
            {
                result.Fail(label, "couldn't save recovery data (" + error + "); left unchanged");
                return;
            }

            var set = CommandRunner.Run("powercfg.exe", $"/setactive {guid}", 30_000);
            if (!set.Success) { result.Fail(label, set.Describe()); return; }

            string? after = GetActiveScheme();
            if (after == null || !after.Equals(guid, StringComparison.OrdinalIgnoreCase))
            {
                result.Fail(label, "Windows did not switch plans");
                return;
            }

            result.Ok(label);
        }

        private static bool TryQuery(string scheme, string sub, string setting, out long ac, out long dc)
        {
            ac = 0;
            dc = 0;
            var q = CommandRunner.Run("powercfg.exe", $"/query {scheme} {sub} {setting}", 30_000);
            return q.Success && OutputParsers.TryParsePowerSettingIndexes(q.Output, out ac, out dc);
        }

        /// <summary>Changes one setting of a plan, after recording its current AC/DC values.</summary>
        private static void SetIndex(string scheme, string sub, string setting, long? ac, long? dc, string label, StepResult result)
        {
            if (!TryQuery(scheme, sub, setting, out long oldAc, out long oldDc))
            {
                result.Skip(label, "couldn't read the current value; left unchanged");
                return;
            }

            bool needAc = ac.HasValue && ac.Value != oldAc;
            bool needDc = dc.HasValue && dc.Value != oldDc;
            if (!needAc && !needDc) { result.Skip(label, "already set"); return; }

            if (!ChangeJournal.TryRecord(j =>
            {
                var entry = j.Power.Settings.FirstOrDefault(p => p.Matches(scheme, sub, setting));
                if (entry == null)
                {
                    entry = new PowerSettingEntry { Scheme = scheme, Subgroup = sub, Setting = setting };
                    j.Power.Settings.Add(entry);
                }
                if (needAc && entry.Ac == null) entry.Ac = oldAc;
                if (needDc && entry.Dc == null) entry.Dc = oldDc;
            }, out string error))
            {
                result.Fail(label, "couldn't save recovery data (" + error + "); left unchanged");
                return;
            }

            if (needAc)
            {
                var r = CommandRunner.Run("powercfg.exe", $"/setacvalueindex {scheme} {sub} {setting} {ac!.Value}", 30_000);
                if (!r.Success) { result.Fail(label, r.Describe()); return; }
            }
            if (needDc)
            {
                var r = CommandRunner.Run("powercfg.exe", $"/setdcvalueindex {scheme} {sub} {setting} {dc!.Value}", 30_000);
                if (!r.Success) { result.Fail(label, r.Describe()); return; }
            }

            if (!TryQuery(scheme, sub, setting, out long newAc, out long newDc) ||
                (needAc && newAc != ac!.Value) || (needDc && newDc != dc!.Value))
            {
                result.Fail(label, "the new value could not be confirmed");
                return;
            }

            result.Ok(label);
        }

        /// <summary>Re-activates the plan if it is the active one, so changed values take effect.</summary>
        private static void ApplyIfActive(string scheme)
        {
            string? active = GetActiveScheme();
            if (active != null && active.Equals(scheme, StringComparison.OrdinalIgnoreCase))
            {
                CommandRunner.Run("powercfg.exe", $"/setactive {scheme}", 30_000);
            }
        }

        public static StepResult ApplyHighPerformance()
        {
            var result = new StepResult();
            Activate(HighPerformance, "High performance power plan", result);
            return result;
        }

        /// <summary>Extreme mode: stop the display, disks and system from sleeping while on AC power.</summary>
        public static StepResult ApplyNeverSleepOnAc()
        {
            var result = new StepResult();
            string? scheme = GetActiveScheme();
            if (scheme == null) { result.Fail("Power timeouts", "couldn't read the active power plan"); return result; }

            SetIndex(scheme, SubDisplay, SettingDisplayTimeout, 0, null, "Display never turns off (on AC power)", result);
            SetIndex(scheme, SubDisk, SettingDiskTimeout, 0, null, "Disks never spin down (on AC power)", result);
            SetIndex(scheme, SubSleep, SettingStandbyTimeout, 0, null, "No automatic sleep (on AC power)", result);
            SetIndex(scheme, SubSleep, SettingHibernateTimeout, 0, null, "No automatic hibernate (on AC power)", result);

            ApplyIfActive(scheme);
            return result;
        }

        public static StepResult ApplyPowerSaver()
        {
            var result = new StepResult();

            if (!ListSchemes().Contains(PowerSaver))
            {
                result.Skip("Power Saver plan", "not available on this PC");
                return result;
            }

            SetIndex(PowerSaver, SubProcessor, SettingMaxProcessor, 50, 50, "Maximum processor state 50%", result);
            SetIndex(PowerSaver, SubDisplay, SettingDisplayTimeout, null, 300, "Screen off after 5 minutes on battery", result);
            SetIndex(PowerSaver, SubSleep, SettingStandbyTimeout, null, 600, "Sleep after 10 minutes on battery", result);
            SetIndex(PowerSaver, SubUsb, SettingUsbSelectiveSuspend, null, 1, "USB selective suspend on battery", result);
            SetIndex(PowerSaver, SubDisplay, SettingAdaptiveBrightness, null, 1, "Adaptive brightness on battery", result);

            Activate(PowerSaver, "Power Saver plan", result);
            ApplyIfActive(PowerSaver);
            return result;
        }

        public static StepResult RestoreAll()
        {
            var result = new StepResult();
            var power = ChangeJournal.Snapshot().Power;

            foreach (var e in power.Settings)
            {
                string label = $"Power setting {e.Setting}";
                bool ok = true;

                if (e.Ac.HasValue)
                {
                    var r = CommandRunner.Run("powercfg.exe", $"/setacvalueindex {e.Scheme} {e.Subgroup} {e.Setting} {e.Ac.Value}", 30_000);
                    if (!r.Success) { result.Fail(label, r.Describe()); ok = false; }
                }
                if (ok && e.Dc.HasValue)
                {
                    var r = CommandRunner.Run("powercfg.exe", $"/setdcvalueindex {e.Scheme} {e.Subgroup} {e.Setting} {e.Dc.Value}", 30_000);
                    if (!r.Success) { result.Fail(label, r.Describe()); ok = false; }
                }

                if (ok && TryQuery(e.Scheme, e.Subgroup, e.Setting, out long ac, out long dc) &&
                    (!e.Ac.HasValue || ac == e.Ac.Value) && (!e.Dc.HasValue || dc == e.Dc.Value))
                {
                    var captured = e;
                    if (!ChangeJournal.TryRecord(j => j.Power.Settings.RemoveAll(p => p.Matches(captured.Scheme, captured.Subgroup, captured.Setting)), out string error))
                        result.Note($"{label}: restored, but the recovery record could not be cleared ({error})");
                    result.Ok(label);
                }
                else if (ok)
                {
                    result.Fail(label, "the original value could not be confirmed");
                }
            }

            string? original = power.OriginalActiveScheme;
            if (original != null)
            {
                var r = CommandRunner.Run("powercfg.exe", $"/setactive {original}", 30_000);
                string? after = r.Success ? GetActiveScheme() : null;

                if (after != null && after.Equals(original, StringComparison.OrdinalIgnoreCase))
                {
                    if (!ChangeJournal.TryRecord(j => { j.Power.OriginalActiveScheme = null; }, out string error))
                        result.Note($"Power plan restored, but the recovery record could not be cleared ({error})");
                    result.Ok("Original power plan");
                }
                else
                {
                    result.Fail("Original power plan", r.Success ? "Windows did not switch back" : r.Describe());
                }
            }
            else if (power.Settings.Count > 0)
            {
                string? active = GetActiveScheme();
                if (active != null) ApplyIfActive(active);
            }

            return result;
        }
    }

    /// <summary>TCP tuning, changed only when it differs from the target and always restorable.</summary>
    internal static class NetworkTweaks
    {
        public static StepResult OptimizeTcp()
        {
            var result = new StepResult();

            var show = CommandRunner.Run("netsh.exe", "int tcp show global", 30_000);
            if (!show.Success || !OutputParsers.TryParseTcpGlobal(show.Output, out string? autoTuning, out string? rss))
            {
                result.Skip("TCP tuning", "couldn't read the current settings (supported on English Windows only); left unchanged");
                return result;
            }

            ApplySetting(result, "TCP receive auto-tuning", "autotuninglevel", "normal", autoTuning!,
                (j, v) => { j.Network.AutoTuning ??= v; });
            ApplySetting(result, "Receive-side scaling", "rss", "enabled", rss!,
                (j, v) => { j.Network.Rss ??= v; });

            return result;
        }

        private static void ApplySetting(StepResult result, string label, string netshName, string target, string current, Action<JournalData, string> record)
        {
            if (current == target) { result.Skip(label, "already " + target); return; }

            var allowed = netshName == "rss" ? OutputParsers.RssValues : OutputParsers.AutoTuningValues;
            if (!allowed.Contains(current, StringComparer.OrdinalIgnoreCase))
            {
                result.Skip(label, $"current value '{current}' isn't one Exoptimizer can restore; left unchanged");
                return;
            }

            if (!ChangeJournal.TryRecord(j => record(j, current), out string error))
            {
                result.Fail(label, "couldn't save recovery data (" + error + "); left unchanged");
                return;
            }

            var set = CommandRunner.Run("netsh.exe", $"int tcp set global {netshName}={target}", 30_000);
            if (!set.Success) { result.Fail(label, set.Describe()); return; }

            var verify = CommandRunner.Run("netsh.exe", "int tcp show global", 30_000);
            if (verify.Success && OutputParsers.TryParseTcpGlobal(verify.Output, out string? a, out string? r))
            {
                string actual = netshName == "rss" ? r! : a!;
                if (actual == target) { result.Ok(label); return; }
            }

            result.Fail(label, "the change could not be confirmed");
        }

        public static StepResult Restore()
        {
            var result = new StepResult();
            var net = ChangeJournal.Snapshot().Network;

            if (net.AutoTuning != null)
            {
                var r = CommandRunner.Run("netsh.exe", $"int tcp set global autotuninglevel={net.AutoTuning}", 30_000);
                if (r.Success)
                {
                    ChangeJournal.TryRecord(j => { j.Network.AutoTuning = null; }, out _);
                    result.Ok("TCP receive auto-tuning");
                }
                else result.Fail("TCP receive auto-tuning", r.Describe());
            }

            if (net.Rss != null)
            {
                var r = CommandRunner.Run("netsh.exe", $"int tcp set global rss={net.Rss}", 30_000);
                if (r.Success)
                {
                    ChangeJournal.TryRecord(j => { j.Network.Rss = null; }, out _);
                    result.Ok("Receive-side scaling");
                }
                else result.Fail("Receive-side scaling", r.Describe());
            }

            return result;
        }
    }

    /// <summary>
    /// Windows Defender changes. Both are opt-in; each is verified; each is undone from
    /// what was recorded. Exoptimizer never touches anything it didn't add itself.
    /// </summary>
    internal static class DefenderTweaks
    {
        /// <summary>The Riot Games folders that can be excluded from Defender scans (only those that exist are used).</summary>
        public static List<string> ExclusionCandidates()
        {
            var candidates = new List<string>
            {
                @"C:\Riot Games",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Riot Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games"),
            };

            return candidates
                .Select(p => Normalize(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<string> ExistingExclusionCandidates() =>
            ExclusionCandidates().Where(Directory.Exists).ToList();

        private static string Normalize(string path)
        {
            try { return Path.GetFullPath(path).TrimEnd('\\'); }
            catch { return path.TrimEnd('\\'); }
        }

        private static List<string>? ReadExclusions(out string error)
        {
            error = string.Empty;
            var r = CommandRunner.RunPowerShell("(Get-MpPreference).ExclusionPath | ForEach-Object { $_ }", 60_000);
            if (!r.Success)
            {
                error = r.Describe();
                return null;
            }

            return r.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => Normalize(l.Trim()))
                           .ToList();
        }

        public static StepResult AddExclusions()
        {
            var result = new StepResult();
            var paths = ExistingExclusionCandidates();

            if (paths.Count == 0)
            {
                result.Skip("Defender exclusions", "no Riot Games folders were found");
                return result;
            }

            var existing = ReadExclusions(out string readError);
            if (existing == null)
            {
                result.Fail("Defender exclusions", "couldn't read Defender's current settings (" + readError + "); nothing was added");
                return result;
            }

            foreach (var path in paths)
            {
                if (existing.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    result.Skip(path, "already excluded");
                    continue;
                }

                if (!ChangeJournal.TryRecord(j =>
                {
                    if (!j.AddedDefenderExclusions.Contains(path, StringComparer.OrdinalIgnoreCase)) j.AddedDefenderExclusions.Add(path);
                }, out string error))
                {
                    result.Fail(path, "couldn't save recovery data (" + error + "); not added");
                    continue;
                }

                var add = CommandRunner.RunPowerShell($"Add-MpPreference -ExclusionPath {CommandRunner.PsQuote(path)}", 60_000);
                var after = add.Success ? ReadExclusions(out _) : null;

                if (after != null && after.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    result.Ok(path);
                }
                else
                {
                    // It wasn't added, so there is nothing to undo later.
                    ChangeJournal.TryRecord(j => j.AddedDefenderExclusions.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase)), out _);
                    result.Fail(path, add.Success ? "Defender did not keep the exclusion" : add.Describe());
                }
            }

            return result;
        }

        public static StepResult RemoveAddedExclusions()
        {
            var result = new StepResult();
            var added = ChangeJournal.Snapshot().AddedDefenderExclusions;
            if (added.Count == 0) return result;

            var existing = ReadExclusions(out string readError);
            if (existing == null)
            {
                foreach (var p in added) result.Fail(p, "couldn't read Defender's settings (" + readError + ")");
                return result;
            }

            foreach (var path in added)
            {
                if (existing.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    var remove = CommandRunner.RunPowerShell($"Remove-MpPreference -ExclusionPath {CommandRunner.PsQuote(path)}", 60_000);
                    var after = remove.Success ? ReadExclusions(out _) : null;
                    if (after == null || after.Contains(path, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Fail(path, remove.Success ? "the exclusion is still present" : remove.Describe());
                        continue;
                    }
                }

                string captured = path;
                ChangeJournal.TryRecord(j => j.AddedDefenderExclusions.RemoveAll(p => p.Equals(captured, StringComparison.OrdinalIgnoreCase)), out _);
                result.Ok(path);
            }

            return result;
        }

        /// <summary>True/false for Defender's real-time protection state; null if it can't be read (for example with another antivirus).</summary>
        public static bool? IsRealtimeEnabled()
        {
            var r = CommandRunner.RunPowerShell("(Get-MpComputerStatus).RealTimeProtectionEnabled", 60_000);
            if (!r.Success) return null;

            string text = r.Output.Trim();
            if (text.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
            if (text.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        /// <summary>
        /// Turns real-time protection off through policy, then checks that Windows actually
        /// reports it as off. If Tamper Protection or another policy blocks it, the policy
        /// value is rolled back and the step reports failure instead of claiming success.
        /// </summary>
        public static StepResult DisableRealtime()
        {
            var tweak = TweakCatalog.DefenderRealtime[0];
            var result = RegistryTweaks.Apply(TweakCatalog.DefenderRealtime);
            if (result.Status == StepStatus.Failed) return result;

            bool? enabled = null;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                enabled = IsRealtimeEnabled();
                if (enabled != true) break;
                Thread.Sleep(1500);
            }

            if (enabled == true)
            {
                RegistryTweaks.Revert(tweak.Hive, tweak.SubKey, tweak.Name);
                var failed = new StepResult();
                failed.Fail("Defender real-time protection",
                    "Windows still reports it as ON - Tamper Protection or another policy blocked the change, so nothing was changed");
                return failed;
            }

            if (enabled == null)
            {
                result.Note("Defender's state could not be read (another antivirus may be active), so the result couldn't be confirmed.");
            }

            return result;
        }
    }

    /// <summary>Turns off common launcher entries in the current user's startup list - reversibly, using the same switch as Task Manager's Startup tab.</summary>
    internal static class StartupTweaks
    {
        private static readonly string[] Keywords = { "teams", "skype", "spotify", "discord", "steam", "epic", "adobe", "java", "onedrive" };

        // 12 bytes, first byte 03 = "disabled" (the format Task Manager uses).
        private static readonly byte[] DisabledMarker = { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        public static StepResult DisableCommonEntries()
        {
            var names = new List<string>();

            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false);
                if (run != null)
                {
                    foreach (var name in run.GetValueNames())
                    {
                        if (name.Length > 0 && name.Length <= 260 &&
                            Keywords.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            names.Add(name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StepResult.Failure("couldn't read your startup list (" + ex.Message + ")");
            }

            if (names.Count == 0)
            {
                return StepResult.Skipped("none of the common launchers are in your startup list");
            }

            var tweaks = names.Select(n => new RegTweak("HKCU", TweakCatalog.StartupApprovedRunKey, n, DisabledMarker, RegistryValueKind.Binary));
            return RegistryTweaks.Apply(tweaks);
        }
    }
}
