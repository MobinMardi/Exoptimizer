using System.Collections.Generic;
using Xunit;

namespace Exoptimizer.Tests
{
    public class ServiceRulesTests
    {
        [Theory]
        [InlineData("Spooler", true)]
        [InlineData("MSSQL$SQLEXPRESS", true)]
        [InlineData("Some-Service_1.0", true)]
        [InlineData("", false)]
        [InlineData("two words", false)]
        [InlineData("x\" & calc", false)]
        [InlineData("..\\..\\evil", false)]
        [InlineData("name;rm", false)]
        public void ValidatesServiceNames(string name, bool expected)
        {
            Assert.Equal(expected, ServiceRules.IsValidName(name));
        }

        [Theory]
        [InlineData("auto", true)]
        [InlineData("DEMAND", true)]
        [InlineData("delayed-auto", true)]
        [InlineData("disabled", true)]
        [InlineData("boot", false)]
        [InlineData("demand & calc", false)]
        [InlineData("", false)]
        public void ValidatesStartTypes(string type, bool expected)
        {
            Assert.Equal(expected, ServiceRules.IsValidStartType(type));
        }

        [Fact]
        public void ProtectedServicesAreRecognised()
        {
            Assert.True(SystemGuard.IsProtected("VSS"));
            Assert.True(SystemGuard.IsProtected("vss")); // case-insensitive
            Assert.False(SystemGuard.IsProtected("Fax"));
        }
    }

    public class JournalSanitizeTests
    {
        [Fact]
        public void RemovesEntriesThatExoptimizerCouldNotHaveWritten()
        {
            var data = new JournalData();
            data.Services.Add(new ServiceEntry { Name = "Fax", StartType = "AUTO", WasRunning = true });
            data.Services.Add(new ServiceEntry { Name = "VSS", StartType = "auto" });            // protected
            data.Services.Add(new ServiceEntry { Name = "bad name", StartType = "auto" });       // invalid name
            data.Services.Add(new ServiceEntry { Name = "Spooler", StartType = "weird" });       // invalid type
            data.Services.Add(new ServiceEntry { Name = "fax", StartType = "demand" });          // duplicate: first original wins

            data.Registry.Add(new RegistryEntry { Hive = "HKLM", SubKey = @"SOFTWARE\Evil", Name = "X", ValueExisted = true, Kind = "DWord", Data = "1" });
            data.Registry.Add(new RegistryEntry { Hive = "HKLM", SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", Name = "HwSchMode", ValueExisted = true, Kind = "DWord", Data = "1" });
            data.Registry.Add(new RegistryEntry { Hive = "HKLM", SubKey = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", Name = "HwSchMode2", ValueExisted = true, Kind = "DWord", Data = "1" });

            data.Power.OriginalActiveScheme = "not-a-guid";
            data.Power.Settings.Add(new PowerSettingEntry { Scheme = "nope", Subgroup = "x", Setting = "y", Ac = 1 });
            data.Network.AutoTuning = "bogus";
            data.AddedDefenderExclusions.Add("relative\\path");
            data.AddedDefenderExclusions.Add(@"C:\Riot Games");

            var problems = data.Sanitize();

            Assert.Single(data.Services);
            Assert.Equal("Fax", data.Services[0].Name);
            Assert.Equal("auto", data.Services[0].StartType);
            Assert.True(data.Services[0].WasRunning);

            Assert.Single(data.Registry);
            Assert.Equal("HwSchMode", data.Registry[0].Name);

            Assert.Null(data.Power.OriginalActiveScheme);
            Assert.Empty(data.Power.Settings);
            Assert.Null(data.Network.AutoTuning);
            Assert.Single(data.AddedDefenderExclusions);
            Assert.NotEmpty(problems);
        }
    }

    public class OutputParserTests
    {
        [Fact]
        public void ParsesActiveSchemeOnEnglishAndLocalizedOutput()
        {
            Assert.Equal("381b4222-f694-41f0-9685-ff5bb260df2e",
                OutputParsers.ParseActiveSchemeGuid("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"));
            Assert.Equal("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                OutputParsers.ParseActiveSchemeGuid("GUID du modèle de gestion de l'alimentation : 8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C  (Performances élevées)"));
            Assert.Null(OutputParsers.ParseActiveSchemeGuid("nothing here"));
        }

        [Fact]
        public void ParsesSchemeList()
        {
            var set = OutputParsers.ParseSchemeGuids(
                "Existing Power Schemes (* Active)\n-----------------------------------\n" +
                "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *\n" +
                "Power Scheme GUID: a1841308-3541-4fab-bc81-f71556f20b4a  (Power saver)\n");

            Assert.Equal(2, set.Count);
            Assert.Contains("a1841308-3541-4fab-bc81-f71556f20b4a", set);
        }

        [Fact]
        public void ReadsCurrentPowerIndexesFromTheLastTwoHexValues()
        {
            string output =
                "  Power Setting GUID: 3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e  (Turn off display after)\n" +
                "    Minimum Possible Setting: 0x00000000\n" +
                "    Maximum Possible Setting: 0xffffffff\n" +
                "    Possible Settings increment: 0x00000001\n" +
                "    Possible Settings units: Seconds\n" +
                "  Current AC Power Setting Index: 0x00000258\n" +
                "  Current DC Power Setting Index: 0x0000012c\n";

            Assert.True(OutputParsers.TryParsePowerSettingIndexes(output, out long ac, out long dc));
            Assert.Equal(600, ac);
            Assert.Equal(300, dc);

            Assert.False(OutputParsers.TryParsePowerSettingIndexes("no numbers", out _, out _));
        }

        [Theory]
        [InlineData("SERVICE_NAME: Spooler\n        STATE              : 4  RUNNING", true)]
        [InlineData("        STATE              : 1  STOPPED", false)]
        [InlineData("garbage", null)]
        public void ParsesServiceState(string output, bool? expected)
        {
            Assert.Equal(expected, OutputParsers.ParseServiceRunning(output));
        }

        [Fact]
        public void ParsesTcpGlobalSettings()
        {
            string output =
                "Querying active state...\n\nTCP Global Parameters\n----------------------------------------------\n" +
                "Receive-Side Scaling State          : enabled\n" +
                "Receive Window Auto-Tuning Level    : normal\n";

            Assert.True(OutputParsers.TryParseTcpGlobal(output, out string? autoTuning, out string? rss));
            Assert.Equal("normal", autoTuning);
            Assert.Equal("enabled", rss);

            Assert.False(OutputParsers.TryParseTcpGlobal("Zustand: aktiviert", out _, out _));
        }
    }

    public class OperationReportTests
    {
        [Fact]
        public void StepStatusReflectsWhatActuallyHappened()
        {
            var allSkipped = new StepResult();
            allSkipped.Skip("a", "already set");
            Assert.Equal(StepStatus.Skipped, allSkipped.Status);

            var applied = new StepResult();
            applied.Ok("a");
            applied.Skip("b", "not installed");
            Assert.Equal(StepStatus.Succeeded, applied.Status);

            var partial = new StepResult();
            partial.Ok("a");
            partial.Fail("b", "access denied");
            Assert.Equal(StepStatus.Failed, partial.Status);
        }

        [Fact]
        public void ReportIsOnlySuccessfulWhenNoStepFailed()
        {
            var report = new OperationReport();
            report.Add("Step one...", StepResult.Success());
            report.Add("Step two...", StepResult.Skipped("nothing to do"));
            Assert.True(report.AllSucceeded);

            report.Add("Step three...", StepResult.Failure("boom"));
            Assert.False(report.AllSucceeded);
            Assert.Contains("Step three", report.FailedStepLabels());
            Assert.Contains("boom", report.BuildSummary());
        }
    }

    public class OperationGateTests
    {
        [Fact]
        public void OnlyOneOperationRunsAtATime()
        {
            Assert.True(OperationGate.TryEnter("first", out _));
            try
            {
                Assert.True(OperationGate.IsBusy);
                Assert.False(OperationGate.TryEnter("second", out string? holder));
                Assert.Equal("first", holder);
            }
            finally
            {
                OperationGate.Exit();
            }

            Assert.False(OperationGate.IsBusy);
        }
    }
}
