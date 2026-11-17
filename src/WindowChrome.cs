using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// Small Windows-only touches that make the app feel native in both themes:
    /// a dark (or light) title bar, a title bar tinted to match the window on
    /// Windows 11, and dark scrollbars. All of it is best-effort - on a Windows
    /// build that doesn't support a call, the call is simply ignored.
    /// </summary>
    internal static class WindowChrome
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string? subAppName, string? subIdList);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwaBorderColor = 34;
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;
        private const int DwmwaUseImmersiveDarkMode = 20;

        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        private static int ToColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

        private static void SetInt(IntPtr handle, int attribute, int value)
        {
            DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }

        /// <summary>Matches the title bar to the current theme. Safe to call repeatedly, e.g. after a theme switch.</summary>
        public static void Apply(Form form)
        {
            if (!form.IsHandleCreated) return;

            try
            {
                SetInt(form.Handle, DwmwaUseImmersiveDarkMode, Theme.IsDark ? 1 : 0);
                SetInt(form.Handle, DwmwaCaptionColor, ToColorRef(Theme.Canvas));
                SetInt(form.Handle, DwmwaTextColor, ToColorRef(Theme.TextPrimary));
                SetInt(form.Handle, DwmwaBorderColor, ToColorRef(Theme.Border));

                // Forces the non-client area (title bar) to repaint with the new colors.
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
                    SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
            }
            catch
            {
                // Not supported on this Windows build - the standard title bar is fine.
            }
        }

        /// <summary>Asks Windows 11 for rounded corners on a borderless window (ignored elsewhere).</summary>
        public static void RoundCorners(Form form)
        {
            if (!form.IsHandleCreated) return;

            try
            {
                SetInt(form.Handle, DwmwaWindowCornerPreference, 2); // DWMWCP_ROUND
            }
            catch
            {
            }
        }

        /// <summary>Gives a control's scrollbars the dark (or light) Explorer look.</summary>
        public static void ApplyScrollbarTheme(Control control)
        {
            if (!control.IsHandleCreated) return;

            try
            {
                SetWindowTheme(control.Handle, Theme.IsDark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch
            {
            }
        }
    }

    /// <summary>Loads the app logo (embedded in the exe) at the sizes the UI needs.</summary>
    internal static class AppAssets
    {
        private const string LogoResourceName = "Exoptimizer.icon.png";

        private static Bitmap? source;
        private static bool sourceLoaded;
        private static readonly Dictionary<int, Bitmap> Scaled = new();

        private static Bitmap? LoadSource()
        {
            if (sourceLoaded) return source;
            sourceLoaded = true;

            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogoResourceName);
                if (stream != null)
                {
                    using var raw = new Bitmap(stream);
                    source = new Bitmap(raw); // copy, so the stream can be released
                    return source;
                }
            }
            catch
            {
            }

            try
            {
                // Fallback: the icon of the running exe.
                using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) source = icon.ToBitmap();
            }
            catch
            {
            }

            return source;
        }

        /// <summary>The logo scaled to a square of the given size, or null if no logo could be loaded.</summary>
        public static Bitmap? Logo(int size)
        {
            if (Scaled.TryGetValue(size, out var cached)) return cached;

            var src = LoadSource();
            if (src == null) return null;

            var scaled = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(scaled))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, size, size));
            }

            Scaled[size] = scaled;
            return scaled;
        }
    }
}
