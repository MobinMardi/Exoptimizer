using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Exoptimizer
{
    internal sealed class CleanupTarget
    {
        public CleanupTarget(string path, bool isFile = false, string? pattern = null)
        {
            Path = path;
            IsFile = isFile;
            Pattern = pattern;
        }

        public string Path { get; }
        public bool IsFile { get; }
        /// <summary>When set (for example "*.log"), only matching files are touched.</summary>
        public string? Pattern { get; }
    }

    internal sealed class CleanupCategory
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool SelectedByDefault { get; set; }
        public bool NeedsWindowsUpdateStopped { get; set; }
        public List<CleanupTarget> Targets { get; } = new();

        public bool Selected { get; set; }
        public long Bytes { get; set; }
        public int Files { get; set; }
    }

    internal sealed class CleanupResult
    {
        public long BytesFreed { get; set; }
        public int FilesDeleted { get; set; }
        public int FilesSkipped { get; set; }
        public List<string> Notes { get; } = new();
    }

    /// <summary>
    /// Deep cleanup: the user picks categories (after seeing how much each holds), files in
    /// use are skipped, links are never followed, and the Windows Update service is only
    /// stopped if it was running - and is always started again afterwards.
    /// </summary>
    internal static class CleanupService
    {
        public static List<CleanupCategory> BuildCategories()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var list = new List<CleanupCategory>();

            var userTemp = new CleanupCategory
            {
                Id = "usertemp", Title = "Temporary files (your account)",
                Description = "Files programs leave in your temporary folder.", SelectedByDefault = true
            };
            userTemp.Targets.Add(new CleanupTarget(Path.GetTempPath().TrimEnd('\\')));
            list.Add(userTemp);

            var sysTemp = new CleanupCategory
            {
                Id = "systemtemp", Title = "Temporary files (Windows)",
                Description = "Windows' own temporary folder.", SelectedByDefault = true
            };
            sysTemp.Targets.Add(new CleanupTarget(Path.Combine(windows, "Temp")));
            list.Add(sysTemp);

            var wu = new CleanupCategory
            {
                Id = "wucache", Title = "Windows Update download cache",
                Description = "Already-downloaded update files. The Update service is paused while this runs, then restored.",
                SelectedByDefault = true, NeedsWindowsUpdateStopped = true
            };
            wu.Targets.Add(new CleanupTarget(Path.Combine(windows, "SoftwareDistribution", "Download")));
            list.Add(wu);

            var delivery = new CleanupCategory
            {
                Id = "deliveryopt", Title = "Delivery Optimization cache",
                Description = "Windows' peer-to-peer update cache.", SelectedByDefault = true
            };
            delivery.Targets.Add(new CleanupTarget(Path.Combine(commonData, "Microsoft", "Windows", "DeliveryOptimization")));
            list.Add(delivery);

            var prefetch = new CleanupCategory
            {
                Id = "prefetch", Title = "Prefetch data",
                Description = "Windows rebuilds this automatically; programs may start slightly slower once afterwards.", SelectedByDefault = true
            };
            prefetch.Targets.Add(new CleanupTarget(Path.Combine(windows, "Prefetch")));
            list.Add(prefetch);

            var logs = new CleanupCategory
            {
                Id = "logs", Title = "System log files",
                Description = "Useful when diagnosing problems, so this is off unless you choose it.", SelectedByDefault = false
            };
            logs.Targets.Add(new CleanupTarget(Path.Combine(windows, "Logs"), false, "*.log"));
            logs.Targets.Add(new CleanupTarget(Path.Combine(windows, "System32", "LogFiles"), false, "*.log"));
            list.Add(logs);

            var dumps = new CleanupCategory
            {
                Id = "dumps", Title = "Crash dumps",
                Description = "Evidence for diagnosing crashes, so this is off unless you choose it.", SelectedByDefault = false
            };
            dumps.Targets.Add(new CleanupTarget(Path.Combine(windows, "MEMORY.DMP"), true));
            dumps.Targets.Add(new CleanupTarget(Path.Combine(windows, "Minidump")));
            dumps.Targets.Add(new CleanupTarget(Path.Combine(local, "CrashDumps")));
            dumps.Targets.Add(new CleanupTarget(Path.Combine(roaming, "CrashDumps")));
            list.Add(dumps);

            foreach (var c in list) c.Selected = c.SelectedByDefault;
            return list;
        }

        private sealed class Tally
        {
            public long Bytes;
            public int Files;
            public int Skipped;
        }

        /// <summary>Measures how much a category holds, without deleting anything.</summary>
        public static void Scan(CleanupCategory category)
        {
            var tally = new Tally();
            foreach (var target in category.Targets) Walk(target, delete: false, tally);
            category.Bytes = tally.Bytes;
            category.Files = tally.Files;
        }

        public static CleanupResult Run(IEnumerable<CleanupCategory> selected, Action<string, int> report)
        {
            var result = new CleanupResult();
            var chosen = selected.ToList();
            bool needsWu = chosen.Any(c => c.NeedsWindowsUpdateStopped);

            bool? wuWasRunning = null;
            if (needsWu)
            {
                wuWasRunning = SystemGuard.IsRunning("wuauserv");
                if (wuWasRunning == true)
                {
                    report("Pausing Windows Update...", 5);
                    CommandRunner.Run("sc.exe", "stop wuauserv", 30_000);
                    for (int i = 0; i < 15 && SystemGuard.IsRunning("wuauserv") == true; i++) Thread.Sleep(1000);
                }
            }

            try
            {
                for (int i = 0; i < chosen.Count; i++)
                {
                    var category = chosen[i];
                    report($"Cleaning: {category.Title}...", 10 + (int)(80.0 * i / chosen.Count));

                    var tally = new Tally();
                    foreach (var target in category.Targets) Walk(target, delete: true, tally);

                    result.BytesFreed += tally.Bytes;
                    result.FilesDeleted += tally.Files;
                    result.FilesSkipped += tally.Skipped;
                }
            }
            finally
            {
                if (needsWu && wuWasRunning == true)
                {
                    report("Restoring Windows Update...", 95);
                    var start = CommandRunner.Run("sc.exe", "start wuauserv", 30_000);
                    if (!start.Success && SystemGuard.IsRunning("wuauserv") != true)
                        result.Notes.Add("Windows Update could not be started again automatically (" + start.Describe() + "); it will start on its own schedule or after a restart.");
                }
            }

            report("Done", 100);
            return result;
        }

        // ---- file walking --------------------------------------------------------------

        private static void Walk(CleanupTarget target, bool delete, Tally tally)
        {
            try
            {
                if (target.IsFile)
                {
                    if (!File.Exists(target.Path)) return;
                    var info = new FileInfo(target.Path);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0) { tally.Skipped++; return; }
                    HandleFile(info, delete, tally);
                    return;
                }

                if (!Directory.Exists(target.Path)) return;
                WalkDirectory(new DirectoryInfo(target.Path), target.Pattern, delete, tally, 0);
            }
            catch
            {
                tally.Skipped++;
            }
        }

        private static void WalkDirectory(DirectoryInfo dir, string? pattern, bool delete, Tally tally, int depth)
        {
            if (depth > 32) return;

            IEnumerable<FileSystemInfo> entries;
            try
            {
                entries = dir.EnumerateFileSystemInfos("*", new EnumerationOptions { IgnoreInaccessible = true }).ToList();
            }
            catch
            {
                tally.Skipped++;
                return;
            }

            foreach (var entry in entries)
            {
                try
                {
                    // Never follow (or delete through) junctions and symbolic links.
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) { tally.Skipped++; continue; }

                    if (entry is DirectoryInfo sub)
                    {
                        WalkDirectory(sub, pattern, delete, tally, depth + 1);
                        if (delete && pattern == null)
                        {
                            try { if (!sub.EnumerateFileSystemInfos().Any()) sub.Delete(); } catch { /* in use or not empty */ }
                        }
                    }
                    else if (entry is FileInfo file)
                    {
                        if (pattern != null && !MatchesPattern(file.Name, pattern)) continue;
                        HandleFile(file, delete, tally);
                    }
                }
                catch
                {
                    tally.Skipped++;
                }
            }
        }

        private static void HandleFile(FileInfo file, bool delete, Tally tally)
        {
            long length;
            try { length = file.Length; }
            catch { tally.Skipped++; return; }

            if (!delete)
            {
                tally.Bytes += length;
                tally.Files++;
                return;
            }

            try
            {
                if (file.IsReadOnly) file.IsReadOnly = false;
                file.Delete();
                tally.Bytes += length;
                tally.Files++;
            }
            catch
            {
                tally.Skipped++; // in use or protected - left alone
            }
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            if (pattern == "*.log") return name.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return unit == 0 ? $"{bytes} B" : $"{value:F1} {units[unit]}";
        }
    }
}
