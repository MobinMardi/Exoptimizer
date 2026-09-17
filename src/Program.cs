using System;
using System.Windows.Forms;

namespace Exoptimizer
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Per-Monitor V2 DPI awareness: without this, .NET Core/6 WinForms
            // apps default to a mode where Windows just bitmap-stretches the
            // whole window to match the display's scaling, which is what
            // causes blurry text and controls that don't line up correctly on
            // anything other than 100% scaling. This must be set before any
            // window is created.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
