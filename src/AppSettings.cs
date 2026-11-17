using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace Exoptimizer
{
    // Settings class to store user preferences
    public class AppSettings
    {
        public bool SystemTrayEnabled { get; set; } = false;
        public bool DarkModeEnabled { get; set; } = false;

        public static string FilePath => Path.Combine(Application.StartupPath, "exoptimizer_settings.json");

        /// <summary>
        /// Reads the saved settings without ever throwing or showing a message -
        /// used at startup (before any window exists) to pick the splash theme.
        /// MainForm still does its own, user-visible load afterwards.
        /// </summary>
        public static AppSettings LoadOrDefault()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                    if (settings != null) return settings;
                }
            }
            catch
            {
            }

            return new AppSettings();
        }
    }
}
