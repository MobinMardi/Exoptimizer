using System;
using System.Diagnostics;
using System.Text;

namespace Exoptimizer
{
    /// <summary>The outcome of running an external command: whether it started, how it ended, and what it printed.</summary>
    internal readonly struct CommandResult
    {
        public CommandResult(bool started, int exitCode, string output, string error, bool timedOut)
        {
            Started = started;
            ExitCode = exitCode;
            Output = output;
            Error = error;
            TimedOut = timedOut;
        }

        public bool Started { get; }
        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
        public bool TimedOut { get; }

        public bool Success => Started && !TimedOut && ExitCode == 0;

        public static CommandResult Failed(string error) => new CommandResult(false, -1, string.Empty, error, false);

        /// <summary>A short, human-readable reason for a failure (or "ok").</summary>
        public string Describe()
        {
            if (!Started) return string.IsNullOrWhiteSpace(Error) ? "the command could not be started" : FirstLine(Error);
            if (TimedOut) return "the command timed out";
            if (ExitCode == 0) return "ok";

            string detail = FirstLine(Error);
            if (string.IsNullOrEmpty(detail)) detail = FirstLine(Output);
            return string.IsNullOrEmpty(detail) ? $"exit code {ExitCode}" : $"exit code {ExitCode}: {detail}";
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0) return trimmed.Length > 200 ? trimmed.Substring(0, 200) + "..." : trimmed;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// The one place external commands are launched. Output and error streams are
    /// drained asynchronously (so a chatty command can never fill a pipe and hang),
    /// every call has a timeout, and the exit code is always reported.
    /// </summary>
    internal static class CommandRunner
    {
        /// <summary>Test hook: when set, commands are answered by this delegate instead of being launched.</summary>
        internal static Func<string, string, CommandResult>? Interceptor { get; set; }

        public static CommandResult Run(string fileName, string arguments, int timeoutMs = 120_000)
        {
            var hook = Interceptor;
            if (hook != null) return hook(fileName, arguments);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                using var process = new Process { StartInfo = psi };
                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };

                if (!process.Start()) return CommandResult.Failed("the process did not start");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(true); } catch { /* already gone */ }
                    try { process.WaitForExit(3000); } catch { }
                    return new CommandResult(true, -1, Snapshot(stdout), Snapshot(stderr), true);
                }

                // The parameterless overload waits until the async readers have received everything.
                process.WaitForExit();
                return new CommandResult(true, process.ExitCode, Snapshot(stdout), Snapshot(stderr), false);
            }
            catch (Exception ex)
            {
                return CommandResult.Failed(ex.Message);
            }
        }

        /// <summary>Runs a command line through cmd.exe (only for fixed command text, never for user-supplied values).</summary>
        public static CommandResult RunShell(string command, int timeoutMs = 120_000)
        {
            return Run("cmd.exe", "/c " + command, timeoutMs);
        }

        /// <summary>
        /// Runs a PowerShell script. The script is passed encoded, so quotes and special
        /// characters in it can never be reinterpreted by a command line.
        /// </summary>
        public static CommandResult RunPowerShell(string script, int timeoutMs = 180_000)
        {
            string prelude = "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue'; ";
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(prelude + script));
            return Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeoutMs);
        }

        /// <summary>Escapes text for use inside a single-quoted PowerShell string.</summary>
        public static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";

        private static string Snapshot(StringBuilder sb)
        {
            lock (sb) return sb.ToString();
        }
    }
}
