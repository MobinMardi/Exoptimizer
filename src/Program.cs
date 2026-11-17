using System;
using System.Windows.Forms;

namespace Exoptimizer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var settings = AppSettings.LoadOrDefault();
            Theme.Apply(settings.DarkModeEnabled);

            // When the app is set to start minimized in the system tray there is no
            // window to introduce, so the splash screen is only shown for a normal start.
            if (!settings.SystemTrayEnabled)
            {
                try
                {
                    using var splash = new SplashForm();
                    splash.ShowDialog();
                }
                catch
                {
                    // A splash problem must never stop the app from starting.
                }
            }

            Application.Run(new MainForm());
        }
    }
}
