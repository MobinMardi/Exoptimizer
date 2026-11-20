using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Exoptimizer.Tests
{
    // These tests change process-wide state (the command interceptor and the journal folder), so they never run in parallel.
    [CollectionDefinition("StaticState", DisableParallelization = true)]
    public class StaticStateCollection { }

    internal sealed class TempJournalScope : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "exo-tests-" + Guid.NewGuid().ToString("N"));
        public List<string> Commands { get; } = new();

        public TempJournalScope(Func<string, string, CommandResult>? responder = null)
        {
            Directory.CreateDirectory(Root);
            ChangeJournal.DirectoryOverride = Path.Combine(Root, "data");
            ChangeJournal.LegacyStatePathOverride = Path.Combine(Root, "legacy.json");
            ChangeJournal.ResetForTests();

            CommandRunner.Interceptor = (file, args) =>
            {
                Commands.Add(file + " " + args);
                if (responder != null) return responder(file, args);
                return new CommandResult(true, 0, "STATE : 4 RUNNING", string.Empty, false);
            };
        }

        public void Dispose()
        {
            CommandRunner.Interceptor = null;
            ChangeJournal.DirectoryOverride = null;
            ChangeJournal.LegacyStatePathOverride = null;
            ChangeJournal.ResetForTests();
            try { Directory.Delete(Root, true); } catch { }
        }
    }

    [Collection("StaticState")]
    public class CommandRunnerTests
    {
        [Fact]
        public void ReportsOutputAndSuccess()
        {
            var r = CommandRunner.Run("cmd.exe", "/c echo hello");
            Assert.True(r.Success);
            Assert.Contains("hello", r.Output);
        }

        [Fact]
        public void ReportsExitCodes()
        {
            var r = CommandRunner.Run("cmd.exe", "/c exit 3");
            Assert.False(r.Success);
            Assert.Equal(3, r.ExitCode);
            Assert.Contains("3", r.Describe());
        }

        [Fact]
        public void LargeOutputDoesNotHang()
        {
            var r = CommandRunner.Run("cmd.exe", "/c \"for /L %i in (1,1,30000) do @echo line%i\"", 60_000);
            Assert.True(r.Success);
            Assert.False(r.TimedOut);
            Assert.True(r.Output.Length > 200_000);
        }

        [Fact]
        public void TimesOutAndKillsTheProcess()
        {
            var r = CommandRunner.Run("ping.exe", "-n 30 127.0.0.1", 800);
            Assert.True(r.TimedOut);
            Assert.False(r.Success);
        }

        [Fact]
        public void ReportsMissingPrograms()
        {
            var r = CommandRunner.Run("definitely-not-a-real-program.exe", "");
            Assert.False(r.Started);
            Assert.False(r.Success);
        }

        [Fact]
        public void PowerShellQuotingEscapesSingleQuotes()
        {
            Assert.Equal("'C:\\O''Brien'", CommandRunner.PsQuote("C:\\O'Brien"));
        }
    }

    [Collection("StaticState")]
    public class ChangeJournalTests
    {
        [Fact]
        public void RecordedChangesSurviveAReload()
        {
            using var scope = new TempJournalScope();

            Assert.True(ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "Fax", StartType = "auto", WasRunning = true }), out string error), error);

            ChangeJournal.ResetForTests(); // simulate restarting the app
            var loaded = ChangeJournal.Snapshot();

            Assert.Single(loaded.Services);
            Assert.Equal("Fax", loaded.Services[0].Name);
            Assert.True(loaded.Services[0].WasRunning);
        }

        [Fact]
        public void ChangeIsRefusedAndStateKeptWhenTheJournalCannotBeWritten()
        {
            using var scope = new TempJournalScope();

            // Make the journal folder impossible to create: its parent is a file.
            string blocker = Path.Combine(scope.Root, "blocker");
            File.WriteAllText(blocker, "x");
            ChangeJournal.DirectoryOverride = Path.Combine(blocker, "sub");
            ChangeJournal.ResetForTests();

            bool saved = ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "Fax", StartType = "auto" }), out string error);

            Assert.False(saved);
            Assert.False(string.IsNullOrEmpty(error));
            Assert.Empty(ChangeJournal.Snapshot().Services); // the in-memory journal was rolled back too
        }

        [Fact]
        public void CorruptJournalIsSetAsideNotSilentlyLost()
        {
            using var scope = new TempJournalScope();

            Directory.CreateDirectory(ChangeJournal.DirectoryPath);
            File.WriteAllText(ChangeJournal.FilePath, "{ this is not json");

            var loaded = ChangeJournal.Snapshot();

            Assert.Empty(loaded.Services);
            Assert.NotNull(ChangeJournal.LoadNotice);
            Assert.Contains("set aside", ChangeJournal.LoadNotice);
            Assert.Contains(Directory.GetFiles(ChangeJournal.DirectoryPath), f => f.Contains(".corrupt-"));
        }

        [Fact]
        public void PreviousCopyIsUsedWhenTheMainFileIsDamaged()
        {
            using var scope = new TempJournalScope();

            Assert.True(ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "Fax", StartType = "auto" }), out _));
            Assert.True(ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "SysMain", StartType = "auto" }), out _));

            File.WriteAllText(ChangeJournal.FilePath, "garbage");
            ChangeJournal.ResetForTests();

            var loaded = ChangeJournal.Snapshot();
            Assert.NotEmpty(loaded.Services);
            Assert.Contains(loaded.Services, s => s.Name == "Fax");
        }

        [Fact]
        public void OlderServiceStateIsImportedAndRemoved()
        {
            using var scope = new TempJournalScope();

            File.WriteAllText(ChangeJournal.LegacyStatePathOverride!,
                "{\"Fax\":\"auto\",\"VSS\":\"auto\",\"bad name\":\"demand\",\"SysMain\":\"weird\"}");
            ChangeJournal.ResetForTests();

            var loaded = ChangeJournal.Snapshot();

            Assert.Single(loaded.Services);
            Assert.Equal("Fax", loaded.Services[0].Name);
            Assert.False(File.Exists(ChangeJournal.LegacyStatePathOverride!));
        }

        [Fact]
        public void UnreadableJournalBlocksNewChangesInsteadOfOverwritingIt()
        {
            using var scope = new TempJournalScope();

            Assert.True(ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "Fax", StartType = "auto" }), out _));
            ChangeJournal.ResetForTests();

            // Hold the file open exclusively so it can't be read.
            using var lockHandle = new FileStream(ChangeJournal.FilePath, FileMode.Open, FileAccess.Read, FileShare.None);

            bool saved = ChangeJournal.TryRecord(j => j.Services.Add(new ServiceEntry { Name = "SysMain", StartType = "auto" }), out string error);

            Assert.False(saved);
            Assert.Contains("could not be read", error);
        }
    }

    [Collection("StaticState")]
    public class ServiceChangeFailureTests
    {
        [Fact]
        public void ServiceIsLeftUntouchedWhenItsOriginalStateCannotBeSaved()
        {
            using var scope = new TempJournalScope();

            string blocker = Path.Combine(scope.Root, "blocker");
            File.WriteAllText(blocker, "x");
            ChangeJournal.DirectoryOverride = Path.Combine(blocker, "sub");
            ChangeJournal.ResetForTests();

            var result = SystemGuard.DisableServicesSafely(new[] { "Spooler" });

            Assert.Equal(0, result.Succeeded);
            Assert.DoesNotContain(scope.Commands, c => c.Contains("config", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void ProtectedAndInvalidServicesAreNeverTouched()
        {
            using var scope = new TempJournalScope();

            var result = SystemGuard.DisableServicesSafely(new[] { "VSS", "bad name", "x\" & calc" });

            Assert.Equal(0, result.Succeeded);
            Assert.DoesNotContain(scope.Commands, c => c.Contains("config", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(result.Skips, s => s.Contains("protected"));
            Assert.True(result.Failures.Count >= 2);
        }
    }
}
