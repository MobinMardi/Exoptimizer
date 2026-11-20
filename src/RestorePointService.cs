using System;
using Microsoft.Win32;

namespace Exoptimizer
{
    internal sealed class RestorePointResult
    {
        /// <summary>True only if Windows confirmed a new restore point with our description exists.</summary>
        public bool Verified { get; set; }

        public string Message { get; set; } = string.Empty;

        /// <summary>True if System Protection was turned on during this request (with the user's consent).</summary>
        public bool EnabledProtection { get; set; }
    }

    /// <summary>
    /// Creates a system restore point and proves it exists. The restore-point frequency
    /// limit is lifted only for the duration of the request and then put back, and System
    /// Protection is turned on only if the user agrees.
    /// </summary>
    internal static class RestorePointService
    {
        private const string SystemRestoreKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        private const string FrequencyValue = "SystemRestorePointCreationFrequency";

        public static string NewDescription(string version) => $"Exoptimizer v{version} Backup {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

        /// <param name="askToEnableProtection">Called (at most once) if the first attempt fails, to ask whether System Protection may be turned on for the system drive.</param>
        public static RestorePointResult CreateVerified(string description, Func<bool> askToEnableProtection)
        {
            // The frequency override is temporary: remember what was there and always put it back.
            bool hadFrequency = false;
            object? originalFrequency = null;
            RegistryValueKind originalKind = RegistryValueKind.DWord;
            bool overrideApplied = false;

            try
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(SystemRestoreKey, false);
                    if (key != null && key.GetValue(FrequencyValue) != null)
                    {
                        hadFrequency = true;
                        originalFrequency = key.GetValue(FrequencyValue);
                        originalKind = key.GetValueKind(FrequencyValue);
                    }

                    using var write = Registry.LocalMachine.CreateSubKey(SystemRestoreKey, true);
                    write.SetValue(FrequencyValue, 0, RegistryValueKind.DWord);
                    overrideApplied = true;
                }
                catch
                {
                    // Not fatal: Windows may then refuse a second restore point within 24 hours, which the verification below reports.
                }

                var attempt = TryCreate(description);
                bool enabled = false;

                if (!attempt.Verified && askToEnableProtection())
                {
                    var enable = CommandRunner.RunPowerShell(
                        "Enable-ComputerRestore -Drive ([System.IO.Path]::GetPathRoot($env:SystemDrive + '\\'))", 120_000);

                    if (enable.Success)
                    {
                        enabled = true;
                        attempt = TryCreate(description);
                    }
                    else
                    {
                        attempt.Message = "System Protection could not be turned on (" + enable.Describe() + ").";
                    }
                }

                attempt.EnabledProtection = enabled;
                return attempt;
            }
            finally
            {
                if (overrideApplied)
                {
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(SystemRestoreKey, true);
                        if (hadFrequency && originalFrequency != null) key.SetValue(FrequencyValue, originalFrequency, originalKind);
                        else key.DeleteValue(FrequencyValue, false);
                    }
                    catch
                    {
                        // Leaving the frequency at 0 is harmless; it only allows more restore points.
                    }
                }
            }
        }

        private static RestorePointResult TryCreate(string description)
        {
            string script =
                "$desc = " + CommandRunner.PsQuote(description) + "; " +
                "$before = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue | ForEach-Object { [string]$_.SequenceNumber }); " +
                "Checkpoint-Computer -Description $desc -RestorePointType MODIFY_SETTINGS; " +
                "Start-Sleep -Seconds 2; " +
                "$after = @(Get-ComputerRestorePoint | Where-Object { $_.Description -eq $desc -and ($before -notcontains [string]$_.SequenceNumber) }); " +
                "if ($after.Count -gt 0) { Write-Output ('VERIFIED:' + $after[0].SequenceNumber); exit 0 } " +
                "else { [Console]::Error.WriteLine('Windows did not report a new restore point after the request.'); exit 2 }";

            var r = CommandRunner.RunPowerShell(script, 240_000);

            if (r.Success && r.Output.Contains("VERIFIED:"))
            {
                return new RestorePointResult { Verified = true, Message = "Restore point created and confirmed." };
            }

            string reason = r.Describe();
            return new RestorePointResult
            {
                Verified = false,
                Message = "A restore point could not be created or confirmed (" + reason + "). System Protection may be turned off, or Windows may be limiting restore points."
            };
        }
    }
}
