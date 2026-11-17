using System;
using System.Collections.Generic;
using System.Drawing;

namespace Exoptimizer
{
    /// <summary>
    /// The single source of truth for Exoptimizer's look. Every custom control
    /// reads from here when it paints, so switching between the light and the
    /// dark theme is just a call to <see cref="Apply"/> followed by a repaint.
    ///
    /// Accent: Discord-style blurple (#5865F2). Light theme = pale, airy
    /// surfaces with deep indigo ink. Dark theme = near-black indigo surfaces
    /// with softly lit text.
    /// </summary>
    internal static class Theme
    {
        public static bool IsDark { get; private set; }

        // Surfaces
        public static Color Canvas { get; private set; }         // window + sidebar background
        public static Color Surface { get; private set; }        // cards
        public static Color Hover { get; private set; }          // hover wash on neutral items
        public static Color Border { get; private set; }         // hairlines
        public static Color BorderStrong { get; private set; }   // outlined buttons, inactive toggles

        // Text
        public static Color TextPrimary { get; private set; }
        public static Color TextSecondary { get; private set; }
        public static Color TextTertiary { get; private set; }

        // Accent (blurple)
        public static Color Accent { get; private set; }         // fills
        public static Color AccentSoft { get; private set; }     // tinted backgrounds
        public static Color AccentText { get; private set; }     // accent used as text / small icons

        // Semantic colors
        public static Color Success { get; private set; }
        public static Color SuccessSoft { get; private set; }
        public static Color Warning { get; private set; }
        public static Color WarningSoft { get; private set; }
        public static Color Danger { get; private set; }
        public static Color DangerSoft { get; private set; }

        /// <summary>Near-black used for text on light fills (e.g. mint buttons in the dark theme).</summary>
        public static readonly Color Ink = Color.FromArgb(14, 16, 30);

        static Theme()
        {
            Apply(false);
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;

            if (!dark)
            {
                Canvas = Color.FromArgb(245, 246, 251);
                Surface = Color.FromArgb(255, 255, 255);
                Hover = Color.FromArgb(236, 238, 248);
                Border = Color.FromArgb(227, 229, 240);
                BorderStrong = Color.FromArgb(205, 208, 226);

                TextPrimary = Color.FromArgb(21, 23, 46);
                TextSecondary = Color.FromArgb(95, 99, 130);
                TextTertiary = Color.FromArgb(142, 146, 174);

                Accent = Color.FromArgb(88, 101, 242);
                AccentSoft = Color.FromArgb(236, 238, 254);
                AccentText = Color.FromArgb(71, 82, 196);

                Success = Color.FromArgb(22, 138, 91);
                SuccessSoft = Color.FromArgb(228, 246, 238);
                Warning = Color.FromArgb(178, 106, 0);
                WarningSoft = Color.FromArgb(255, 243, 220);
                Danger = Color.FromArgb(217, 58, 63);
                DangerSoft = Color.FromArgb(253, 235, 236);
            }
            else
            {
                Canvas = Color.FromArgb(14, 15, 21);
                Surface = Color.FromArgb(22, 24, 36);
                Hover = Color.FromArgb(29, 32, 48);
                Border = Color.FromArgb(38, 42, 62);
                BorderStrong = Color.FromArgb(58, 63, 92);

                TextPrimary = Color.FromArgb(236, 238, 249);
                TextSecondary = Color.FromArgb(156, 161, 190);
                TextTertiary = Color.FromArgb(107, 112, 144);

                Accent = Color.FromArgb(88, 101, 242);
                AccentSoft = Color.FromArgb(34, 38, 86);
                AccentText = Color.FromArgb(154, 163, 255);

                Success = Color.FromArgb(52, 200, 140);
                SuccessSoft = Color.FromArgb(18, 48, 42);
                Warning = Color.FromArgb(240, 170, 60);
                WarningSoft = Color.FromArgb(58, 46, 22);
                Danger = Color.FromArgb(220, 70, 77);
                DangerSoft = Color.FromArgb(58, 26, 32);
            }
        }

        // ---- Fonts ---------------------------------------------------------------------

        private static readonly Dictionary<(float, bool), Font> FontCache = new();

        /// <summary>Segoe UI (regular or Semibold) at the given point size. Cached and shared - never dispose these.</summary>
        public static Font Font(float size, bool semibold = false)
        {
            var key = (size, semibold);
            if (!FontCache.TryGetValue(key, out var font))
            {
                font = new Font(semibold ? "Segoe UI Semibold" : "Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point);
                FontCache[key] = font;
            }
            return font;
        }
    }
}
