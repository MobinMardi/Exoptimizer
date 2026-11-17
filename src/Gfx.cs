using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>Small drawing helpers shared by the custom controls.</summary>
    internal static class Gfx
    {
        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>Linear blend of two opaque colors. t = 0 gives <paramref name="a"/>, t = 1 gives <paramref name="b"/>.</summary>
        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                255,
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        private static double Linear(byte v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static double Luminance(Color c)
        {
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        /// <summary>White or near-black, whichever reads better on the given fill.</summary>
        public static Color ReadableOn(Color fill)
        {
            double l = Luminance(fill);
            double white = 1.05 / (l + 0.05);
            double ink = (l + 0.05) / (Luminance(Theme.Ink) + 0.05);
            return white >= ink ? Color.White : Theme.Ink;
        }

        /// <summary>The solid color visible behind a control - used so anti-aliased rounded edges blend into the right background.</summary>
        public static Color Backdrop(Control control)
        {
            for (Control? p = control.Parent; p != null; p = p.Parent)
            {
                if (p is CardPanel card) return card.FillColor;
                if (p.BackColor.A == 255) return p.BackColor;
            }
            return Theme.Canvas;
        }

        public static void Smooth(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }
}
