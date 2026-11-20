using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Exoptimizer
{
    internal enum StepStatus
    {
        Succeeded,
        Skipped,
        Failed
    }

    /// <summary>
    /// What one step of an operation actually did, item by item. A step is Failed if
    /// anything in it failed, Skipped if nothing was applied (everything was already
    /// set, not installed, protected...), and Succeeded otherwise.
    /// </summary>
    internal sealed class StepResult
    {
        private readonly List<string> failures = new();
        private readonly List<string> skips = new();
        private readonly List<string> notes = new();

        public int Succeeded { get; private set; }

        public IReadOnlyList<string> Failures => failures;
        public IReadOnlyList<string> Skips => skips;
        public IReadOnlyList<string> Notes => notes;

        public StepStatus Status
        {
            get
            {
                if (failures.Count > 0) return StepStatus.Failed;
                if (Succeeded == 0 && skips.Count > 0) return StepStatus.Skipped;
                return StepStatus.Succeeded;
            }
        }

        public void Ok(string item = "")
        {
            Succeeded++;
        }

        public void Skip(string item, string reason) => skips.Add(string.IsNullOrEmpty(item) ? reason : $"{item}: {reason}");

        public void Fail(string item, string reason) => failures.Add(string.IsNullOrEmpty(item) ? reason : $"{item}: {reason}");

        public void Note(string note) => notes.Add(note);

        public void Merge(StepResult other)
        {
            Succeeded += other.Succeeded;
            failures.AddRange(other.failures);
            skips.AddRange(other.skips);
            notes.AddRange(other.notes);
        }

        public static StepResult Success()
        {
            var r = new StepResult();
            r.Ok();
            return r;
        }

        public static StepResult Skipped(string reason)
        {
            var r = new StepResult();
            r.Skip(string.Empty, reason);
            return r;
        }

        public static StepResult Failure(string reason)
        {
            var r = new StepResult();
            r.Fail(string.Empty, reason);
            return r;
        }
    }

    /// <summary>One named step of an operation. The step receives a callback for finer-grained progress text.</summary>
    internal sealed class OperationStep
    {
        public OperationStep(string label, Func<Action<string>, StepResult> run)
        {
            Label = label;
            Run = run;
        }

        public string Label { get; }
        public Func<Action<string>, StepResult> Run { get; }
    }

    /// <summary>The collected outcome of every step in an operation.</summary>
    internal sealed class OperationReport
    {
        private readonly List<(string Label, StepResult Result)> entries = new();

        public IReadOnlyList<(string Label, StepResult Result)> Entries => entries;

        public void Add(string label, StepResult result) => entries.Add((label, result));

        public bool HasFailures => entries.Any(e => e.Result.Status == StepStatus.Failed);

        public bool AllSucceeded => !HasFailures;

        public int TotalSucceeded => entries.Sum(e => e.Result.Succeeded);

        public IEnumerable<string> FailureLines =>
            entries.Where(e => e.Result.Failures.Count > 0)
                   .SelectMany(e => e.Result.Failures.Select(f => $"{CleanLabel(e.Label)} - {f}"));

        public IEnumerable<string> SkipLines =>
            entries.Where(e => e.Result.Skips.Count > 0)
                   .SelectMany(e => e.Result.Skips.Select(s => $"{CleanLabel(e.Label)} - {s}"));

        public IEnumerable<string> NoteLines =>
            entries.Where(e => e.Result.Notes.Count > 0)
                   .SelectMany(e => e.Result.Notes);

        public string FailedStepLabels() =>
            string.Join(", ", entries.Where(e => e.Result.Status == StepStatus.Failed).Select(e => CleanLabel(e.Label)));

        /// <summary>A readable multi-line summary: what failed, what was skipped, and any notes.</summary>
        public string BuildSummary(int maxFailures = 12, int maxSkips = 6)
        {
            var sb = new StringBuilder();

            var failures = FailureLines.ToList();
            if (failures.Count > 0)
            {
                sb.AppendLine($"Could not complete ({failures.Count}):");
                foreach (var line in failures.Take(maxFailures)) sb.AppendLine("  ✗ " + line);
                if (failures.Count > maxFailures) sb.AppendLine($"  ... and {failures.Count - maxFailures} more");
            }

            var skips = SkipLines.ToList();
            if (skips.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"Skipped, no change needed or possible ({skips.Count}):");
                foreach (var line in skips.Take(maxSkips)) sb.AppendLine("  – " + line);
                if (skips.Count > maxSkips) sb.AppendLine($"  ... and {skips.Count - maxSkips} more");
            }

            var notes = NoteLines.ToList();
            if (notes.Count > 0)
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine("Notes:");
                foreach (var line in notes.Take(maxFailures)) sb.AppendLine("  • " + line);
            }

            return sb.ToString().TrimEnd();
        }

        private static string CleanLabel(string label) => label.TrimEnd('.', ' ');
    }

    /// <summary>
    /// Allows only one system-changing operation at a time - within this window, and
    /// across any second Exoptimizer instance. Always release it in a finally block.
    /// </summary>
    internal static class OperationGate
    {
        private static readonly object Sync = new();
        private static string? current;
        private static Mutex? crossProcess;

        public static bool IsBusy
        {
            get { lock (Sync) return current != null; }
        }

        public static string? Current
        {
            get { lock (Sync) return current; }
        }

        public static bool TryEnter(string name, out string? holder)
        {
            lock (Sync)
            {
                if (current != null)
                {
                    holder = current;
                    return false;
                }

                try
                {
                    crossProcess ??= new Mutex(false, @"Global\ExoptimizerOperationLock");
                    if (!crossProcess.WaitOne(0))
                    {
                        holder = "another Exoptimizer window";
                        return false;
                    }
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner exited without releasing it; we own it now.
                }
                catch
                {
                    // The named lock isn't available here; the in-process gate still applies.
                }

                current = name;
                holder = null;
                return true;
            }
        }

        public static void Exit()
        {
            lock (Sync)
            {
                if (current == null) return;
                current = null;

                try { crossProcess?.ReleaseMutex(); }
                catch { /* not owned (named lock was unavailable) */ }
            }
        }
    }
}
