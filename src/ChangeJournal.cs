using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Exoptimizer
{
    /// <summary>
    /// The recovery journal: before Exoptimizer changes a service, registry value, power
    /// setting or network setting, the original is written here - durably - so Undo can
    /// put back exactly what was there. If a change cannot be recorded, it is not made.
    ///
    /// The journal lives in %ProgramData%\Exoptimizer, which is locked to SYSTEM and
    /// Administrators, is written atomically (temp file + replace, with a .bak copy),
    /// survives uninstall unless the user chooses otherwise, and is validated entry by
    /// entry when read back.
    /// </summary>
    internal static class ChangeJournal
    {
        private static readonly object Sync = new();
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private static JournalData? cached;
        private static string? persistedJson;
        private static bool readFailed;
        private static string? readFailure;
        private static bool aclApplied;

        /// <summary>Test hook: use this folder instead of %ProgramData%\Exoptimizer.</summary>
        internal static string? DirectoryOverride { get; set; }

        /// <summary>Test hook: location of the pre-3.2 service state file.</summary>
        internal static string? LegacyStatePathOverride { get; set; }

        /// <summary>A message about something unusual found while loading (corrupt file set aside, invalid entries ignored...), or null.</summary>
        public static string? LoadNotice { get; private set; }

        /// <summary>A warning if the journal folder could not be locked down.</summary>
        public static string? AclWarning { get; private set; }

        public static string DirectoryPath =>
            DirectoryOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Exoptimizer");

        public static string FilePath => Path.Combine(DirectoryPath, "journal.json");

        private static string BackupPath => FilePath + ".bak";

        private static string LegacyStatePath =>
            LegacyStatePathOverride ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exoptimizer_service_state.json");

        /// <summary>Clears cached state (used by tests).</summary>
        internal static void ResetForTests()
        {
            lock (Sync)
            {
                cached = null;
                persistedJson = null;
                readFailed = false;
                readFailure = null;
                aclApplied = false;
                LoadNotice = null;
                AclWarning = null;
            }
        }

        /// <summary>A copy of the current journal. Changing the copy does nothing; use <see cref="TryRecord"/>.</summary>
        public static JournalData Snapshot()
        {
            lock (Sync)
            {
                EnsureLoaded();
                return Clone(cached!);
            }
        }

        /// <summary>
        /// Applies a change to the journal and writes it to disk. Returns false (and leaves
        /// the journal as it was) if it could not be saved - callers must then NOT make the
        /// system change the entry was meant to protect.
        /// </summary>
        public static bool TryRecord(Action<JournalData> mutate, out string error)
        {
            lock (Sync)
            {
                EnsureLoaded();

                if (readFailed)
                {
                    error = "the existing recovery data could not be read" + (readFailure != null ? $" ({readFailure})" : string.Empty);
                    return false;
                }

                try
                {
                    mutate(cached!);
                }
                catch (Exception ex)
                {
                    RestoreFromPersisted();
                    error = ex.Message;
                    return false;
                }

                if (!TrySave(out error))
                {
                    RestoreFromPersisted();
                    return false;
                }

                error = string.Empty;
                return true;
            }
        }

        /// <summary>Writes a standalone backup file (for example before a repair) next to the journal. Returns its path.</summary>
        public static string WriteBackup(string namePrefix, object payload)
        {
            string dir = Path.Combine(DirectoryPath, "backups");
            Directory.CreateDirectory(DirectoryPath);
            SecureDirectoryOnce();
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"{namePrefix}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            string json = JsonSerializer.Serialize(payload, JsonOptions);
            WriteAtomically(path, json, backup: null);
            return path;
        }

        // ---- internals ------------------------------------------------------------------

        private static void EnsureLoaded()
        {
            if (cached != null && !readFailed) return;

            readFailed = false;
            readFailure = null;

            var loaded = TryReadFile(FilePath, out bool unreadable, out string? failure);

            if (loaded == null && !unreadable)
            {
                loaded = TryReadFile(BackupPath, out unreadable, out failure);
                if (loaded != null) LoadNotice = "The recovery data file was damaged; the previous saved copy was used instead.";
            }

            if (loaded == null && File.Exists(FilePath) && !unreadable)
            {
                // Present but unusable (corrupt) and no good backup: set it aside so it is never silently lost.
                try
                {
                    string aside = FilePath + $".corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Move(FilePath, aside);
                    LoadNotice = $"The recovery data file was unreadable and was set aside as {Path.GetFileName(aside)}. New changes are recorded from now on.";
                }
                catch
                {
                    unreadable = true;
                    failure = "the file is damaged and could not be moved aside";
                }
            }

            if (unreadable)
            {
                readFailed = true;
                readFailure = failure;
                cached = new JournalData();
                persistedJson = null;
                LoadNotice = $"Exoptimizer could not read its recovery data ({failure}). It will not change your system until this is resolved.";
                return;
            }

            cached = loaded ?? new JournalData();
            persistedJson = loaded != null ? Serialize(cached) : null;

            MigrateLegacyServiceState();
        }

        private static JournalData? TryReadFile(string path, out bool unreadable, out string? failure)
        {
            unreadable = false;
            failure = null;

            if (!File.Exists(path)) return null;

            string json;
            try
            {
                json = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                unreadable = true;
                failure = ex.Message;
                return null;
            }

            try
            {
                var data = JsonSerializer.Deserialize<JournalData>(json);
                if (data == null) return null;

                var problems = data.Sanitize();
                if (problems.Count > 0)
                {
                    LoadNotice = $"{problems.Count} invalid recovery entr{(problems.Count == 1 ? "y was" : "ies were")} ignored.";
                }
                return data;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void MigrateLegacyServiceState()
        {
            string legacy = LegacyStatePath;
            try
            {
                if (!File.Exists(legacy)) return;

                var old = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(legacy));
                if (old == null || old.Count == 0)
                {
                    TryDelete(legacy);
                    return;
                }

                int added = 0;
                foreach (var pair in old)
                {
                    string type = pair.Value?.Trim().ToLowerInvariant() ?? string.Empty;
                    if (!ServiceRules.IsValidName(pair.Key) || SystemGuard.IsProtected(pair.Key)) continue;
                    if (type != "auto" && type != "demand" && type != "disabled") continue;
                    if (cached!.Services.Exists(s => s.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))) continue;

                    // Older versions didn't record whether a service was running; automatic services normally are.
                    cached.Services.Add(new ServiceEntry { Name = pair.Key, StartType = type, WasRunning = type == "auto" });
                    added++;
                }

                if (TrySave(out _))
                {
                    TryDelete(legacy);
                    if (added > 0) LoadNotice = $"Imported {added} service record(s) saved by an older version.";
                }
                else
                {
                    RestoreFromPersisted();
                }
            }
            catch
            {
                // Leave the old file in place; the next load tries again.
            }
        }

        private static bool TrySave(out string error)
        {
            try
            {
                string json = Serialize(cached!);

                Directory.CreateDirectory(DirectoryPath);
                SecureDirectoryOnce();
                WriteAtomically(FilePath, json, BackupPath);

                persistedJson = json;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void WriteAtomically(string path, string json, string? backup)
        {
            string tmp = path + ".tmp";
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // make sure the bytes reach the disk before the swap
            }

            if (File.Exists(path))
            {
                if (backup != null) File.Replace(tmp, path, backup, true);
                else File.Move(tmp, path, true);
            }
            else
            {
                File.Move(tmp, path);
            }
        }

        private static void SecureDirectoryOnce()
        {
            if (aclApplied) return;
            aclApplied = true;

            // *S-1-5-18 = SYSTEM, *S-1-5-32-544 = Administrators (well-known SIDs, so this works on every language).
            var result = CommandRunner.Run("icacls.exe",
                $"\"{DirectoryPath}\" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F", 30_000);

            if (!result.Success)
            {
                AclWarning = $"The recovery data folder could not be restricted to administrators ({result.Describe()}).";
            }
        }

        private static void RestoreFromPersisted()
        {
            try
            {
                cached = persistedJson != null ? JsonSerializer.Deserialize<JournalData>(persistedJson) ?? new JournalData() : new JournalData();
            }
            catch
            {
                cached = new JournalData();
            }
        }

        private static string Serialize(JournalData data) => JsonSerializer.Serialize(data, JsonOptions);

        private static JournalData Clone(JournalData data)
        {
            return JsonSerializer.Deserialize<JournalData>(Serialize(data)) ?? new JournalData();
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }
}
