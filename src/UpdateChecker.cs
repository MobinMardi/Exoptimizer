using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace Exoptimizer
{
    /// <summary>Result of an update check - always returned, never thrown, so callers never need a try/catch of their own.</summary>
    internal sealed class UpdateCheckResult
    {
        public bool Success { get; init; }
        public bool UpdateAvailable { get; init; }
        public string? LatestVersion { get; init; }
        /// <summary>Where the user is sent to download the new version: always the official website, never the GitHub releases page.</summary>
        public string? DownloadUrl { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Checks the GitHub "latest release" API for a newer version than the
    /// one currently running; the version data still comes from GitHub, but
    /// downloads are handled on the official website (see DownloadPageUrl).
    /// Used both automatically on startup (silently -
    /// see MainForm.CheckForUpdatesOnStartupAsync) and from the manual
    /// "Check for Updates" button in Settings (which shows the result either way).
    /// </summary>
    internal static class UpdateChecker
    {
        private const string ReleasesApiUrl = "https://api.github.com/repos/MobinMardi/Exoptimizer/releases/latest";
        // Where "download the update" leads. Only the version check reads GitHub.
        internal const string DownloadPageUrl = "https://mobinmardi.github.io/exoptimizer/";

        // A single shared, long-lived HttpClient is the documented best
        // practice (creating one per call can exhaust sockets under load).
        private static readonly HttpClient httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            // The GitHub API rejects requests with no User-Agent header.
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Exoptimizer", MainForm.AppVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        public static async Task<UpdateCheckResult> CheckForUpdateAsync()
        {
            try
            {
                using var response = await httpClient.GetAsync(ReleasesApiUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateCheckResult
                    {
                        Success = false,
                        ErrorMessage = $"GitHub returned HTTP {(int)response.StatusCode}."
                    };
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);

                string? tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() : null;

                if (string.IsNullOrWhiteSpace(tag))
                {
                    return new UpdateCheckResult { Success = false, ErrorMessage = "No release information was returned." };
                }

                string latestVersionText = tag.TrimStart('v', 'V');

                if (!TryParseVersion(latestVersionText, out var latestVersion) ||
                    !TryParseVersion(MainForm.AppVersion, out var currentVersion))
                {
                    return new UpdateCheckResult { Success = false, ErrorMessage = $"Couldn't compare version '{tag}'." };
                }

                return new UpdateCheckResult
                {
                    Success = true,
                    UpdateAvailable = latestVersion > currentVersion,
                    LatestVersion = latestVersionText,
                    DownloadUrl = DownloadPageUrl
                };
            }
            catch (TaskCanceledException)
            {
                return new UpdateCheckResult { Success = false, ErrorMessage = "The request timed out." };
            }
            catch (Exception ex)
            {
                return new UpdateCheckResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>System.Version needs at least a Major.Minor and only numeric parts (e.g. "3.2.0", not "3.2.0-beta"); this pads/guards for that.</summary>
        private static bool TryParseVersion(string text, out Version? version)
        {
            string candidate = text.Split('-', '+')[0].Trim(); // drop any "-beta"/"+build" suffix
            if (!candidate.Contains('.')) candidate += ".0";
            return Version.TryParse(candidate, out version);
        }
    }
}
