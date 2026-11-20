using System;
using System.Threading;
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

            // One window at a time: two copies could otherwise change the system at once. The installer
            // uses the same name (AppMutex) to detect a running copy and ask for it to be closed.
            using var singleInstance = new Mutex(true, "ExoptimizerAppMutex", out bool isFirstInstance);
            if (!isFirstInstance)
            {
                AppDialog.Show("Exoptimizer is already running." + Environment.NewLine + Environment.NewLine +
                               "If you can't see its window, look for it in the system tray or the taskbar.",
                    "Exoptimizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

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
