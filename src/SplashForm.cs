using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// Start-up splash: the app icon with the app name beneath it, shown on the
    /// current theme (pale blurple light / deep ink dark). It fades in, holds
    /// long enough to be read comfortably (about 2.6 seconds in total), then
    /// fades out and hands over to the main window.
    /// </summary>
    internal sealed class SplashForm : Form
    {
        private const int FadeInMs = 400;
        private const int HoldMs = 1850;
        private const int FadeOutMs = 380;
        private const int TotalMs = FadeInMs + HoldMs + FadeOutMs;

        private const int LogoSize = 120;

        private readonly Stopwatch clock = new();
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 15 };
        private readonly Bitmap? logo;

        public SplashForm()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(560, 380);
            BackColor = Theme.Canvas;
            Text = "Exoptimizer";
            Opacity = 0;

            logo = AppAssets.Logo(LogoSize);

            timer.Tick += (s, e) => OnTick();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.RoundCorners(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            clock.Start();
            timer.Start();
        }

        private static double EaseOut(double t) => 1 - Math.Pow(1 - t, 3);

        private static double EaseIn(double t) => t * t * t;

        private void OnTick()
        {
            long elapsed = clock.ElapsedMilliseconds;

            if (elapsed >= TotalMs)
            {
                timer.Stop();
                Opacity = 0;
                Close();
                return;
            }

            if (elapsed < FadeInMs)
            {
                Opacity = EaseOut(elapsed / (double)FadeInMs);
            }
            else if (elapsed > FadeInMs + HoldMs)
            {
                Opacity = 1 - EaseIn((elapsed - FadeInMs - HoldMs) / (double)FadeOutMs);
            }
            else
            {
                Opacity = 1;
            }

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Theme.Canvas);

            long elapsed = clock.ElapsedMilliseconds;
            double intro = EaseOut(Math.Min(1.0, elapsed / (double)(FadeInMs + 250)));
            float rise = (float)((1 - intro) * 12);

            // Soft blurple glow behind the icon.
            float cx = Width / 2f;
            float logoTop = 78f + rise;
            float cy = logoTop + LogoSize / 2f;
            using (var glowPath = new GraphicsPath())
            {
                glowPath.AddEllipse(cx - 220, cy - 170, 440, 340);
                using var glow = new PathGradientBrush(glowPath)
                {
                    CenterColor = Color.FromArgb(Theme.IsDark ? 70 : 48, Theme.Accent),
                    SurroundColors = new[] { Color.FromArgb(0, Theme.Accent) }
                };
                g.FillPath(glow, glowPath);
            }

            // Icon
            var logoRect = new RectangleF(cx - LogoSize / 2f, logoTop, LogoSize, LogoSize);
            if (logo != null)
            {
                g.DrawImage(logo, logoRect);
            }
            else
            {
                using var fallbackPath = Gfx.RoundedRect(logoRect, LogoSize * 0.1f);
                using var fallbackBrush = new SolidBrush(Theme.Accent);
                g.FillPath(fallbackBrush, fallbackPath);
                TextRenderer.DrawText(g, "E", Theme.Font(48f, true), Rectangle.Round(logoRect), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            // Hairline around the icon so the dark mark stays visible on the dark theme.
            using (var ring = Gfx.RoundedRect(RectangleF.Inflate(logoRect, 0.5f, 0.5f), LogoSize * 0.1f))
            using (var ringPen = new Pen(Color.FromArgb(Theme.IsDark ? 120 : 60, Theme.BorderStrong), 1f))
            {
                g.DrawPath(ringPen, ring);
            }

            const TextFormatFlags centered = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                             TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            // App name
            int nameTop = (int)(logoTop + LogoSize + 26);
            TextRenderer.DrawText(g, "Exoptimizer", Theme.Font(30f, true),
                new Rectangle(0, nameTop, Width, 46), Theme.TextPrimary, centered);

            TextRenderer.DrawText(g, "Gaming Optimization Tool", Theme.Font(11f),
                new Rectangle(0, nameTop + 46, Width, 24), Theme.TextSecondary, centered);

            // Slim progress line
            float barWidth = 168f;
            float barX = cx - barWidth / 2f;
            float barY = Height - 62f;
            double progress = Math.Max(0.0, Math.Min(1.0, (elapsed - 150) / (double)(TotalMs - FadeOutMs - 150)));

            using (var trackPath = Gfx.RoundedRect(new RectangleF(barX, barY, barWidth, 3f), 1.5f))
            using (var trackBrush = new SolidBrush(Theme.Border))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float fillWidth = (float)(barWidth * EaseOut(progress));
            if (fillWidth >= 3f)
            {
                using var fillPath = Gfx.RoundedRect(new RectangleF(barX, barY, fillWidth, 3f), 1.5f);
                using var fillBrush = new SolidBrush(Theme.Accent);
                g.FillPath(fillBrush, fillPath);
            }

            TextRenderer.DrawText(g, "v" + MainForm.AppVersion, Theme.Font(9f),
                new Rectangle(0, Height - 44, Width, 20), Theme.TextTertiary, centered);

            // Window outline
            using var borderPen = new Pen(Theme.Border, 1f);
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
