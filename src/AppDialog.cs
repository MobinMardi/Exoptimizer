using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// Themed replacement for MessageBox. Same call shape and same DialogResult
    /// values, so every confirmation in the app keeps working - it just matches
    /// the light/dark theme instead of flashing a bright system dialog.
    /// </summary>
    internal sealed class AppDialog : Form
    {
        private const int DialogWidth = 500;
        private const int Pad = 28;
        private const int BadgeSize = 40;
        private const int MaxTextHeight = 340;

        public static DialogResult Show(string text)
            => Show(text, "Exoptimizer", MessageBoxButtons.OK, MessageBoxIcon.None);

        public static DialogResult Show(string text, string caption)
            => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
            => Show(text, caption, buttons, MessageBoxIcon.None);

        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using var dialog = new AppDialog(text, caption, buttons, icon);

            Form? owner = Form.ActiveForm;
            if (owner != null && owner.Visible && !owner.IsDisposed)
            {
                return dialog.ShowDialog(owner);
            }

            return dialog.ShowDialog();
        }

        private readonly MessageBoxIcon iconKind;

        private AppDialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            iconKind = icon;

            AutoScaleMode = AutoScaleMode.None;
            Text = caption;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Theme.Surface;
            Font = Theme.Font(9f);

            bool hasBadge = icon != MessageBoxIcon.None;
            int textLeft = hasBadge ? Pad + BadgeSize + 18 : Pad;
            int textWidth = DialogWidth - textLeft - Pad;

            // Measure the message so the dialog fits it (long messages scroll instead of growing off-screen).
            Font messageFont = Theme.Font(10f);
            Size measured = TextRenderer.MeasureText(text, messageFont, new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            int fullTextHeight = measured.Height + 6;
            bool scrolls = fullTextHeight > MaxTextHeight;
            int hostHeight = Math.Max(BadgeSize, Math.Min(fullTextHeight, MaxTextHeight));

            var host = new BufferedPanel
            {
                Location = new Point(textLeft, Pad),
                Size = new Size(textWidth, hostHeight),
                BackColor = Theme.Surface,
                AutoScroll = scrolls
            };

            var message = new Label
            {
                Text = text,
                Font = messageFont,
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.Surface,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(0, hasBadge && !scrolls ? Math.Max(0, (BadgeSize - fullTextHeight) / 2) : 0),
                Size = new Size(scrolls ? textWidth - SystemInformation.VerticalScrollBarWidth - 4 : textWidth, fullTextHeight)
            };
            host.Controls.Add(message);

            int footerTop = Pad + hostHeight + Pad;
            const int footerHeight = 70;

            var footer = new BufferedPanel
            {
                Location = new Point(0, footerTop),
                Size = new Size(DialogWidth, footerHeight),
                BackColor = Theme.Canvas
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            if (hasBadge)
            {
                var badge = new IconBadge(icon) { Location = new Point(Pad, Pad), Size = new Size(BadgeSize, BadgeSize) };
                Controls.Add(badge);
            }

            Controls.Add(host);
            Controls.Add(footer);

            ClientSize = new Size(DialogWidth, footerTop + footerHeight);
            StartPosition = FormStartPosition.CenterParent;

            // Buttons, right-aligned: primary first (left), secondary last (right) - same order as a system message box.
            ModernButton? primary = null;
            ModernButton? secondary = null;

            switch (buttons)
            {
                case MessageBoxButtons.YesNo:
                    primary = MakeButton("Yes", DialogResult.Yes, ButtonKind.Filled);
                    secondary = MakeButton("No", DialogResult.No, ButtonKind.Ghost);
                    break;
                case MessageBoxButtons.OKCancel:
                    primary = MakeButton("OK", DialogResult.OK, ButtonKind.Filled);
                    secondary = MakeButton("Cancel", DialogResult.Cancel, ButtonKind.Ghost);
                    break;
                case MessageBoxButtons.YesNoCancel:
                    primary = MakeButton("Yes", DialogResult.Yes, ButtonKind.Filled);
                    secondary = MakeButton("No", DialogResult.No, ButtonKind.Ghost);
                    break;
                default:
                    primary = MakeButton("OK", DialogResult.OK, ButtonKind.Filled);
                    break;
            }

            int right = DialogWidth - Pad;
            int buttonTop = (footerHeight - 38) / 2;

            if (secondary != null)
            {
                secondary.Location = new Point(right - secondary.Width, buttonTop);
                footer.Controls.Add(secondary);
                right = secondary.Left - 10;
            }

            primary.Location = new Point(right - primary.Width, buttonTop);
            footer.Controls.Add(primary);

            AcceptButton = primary;
            CancelButton = secondary ?? primary;
            ActiveControl = primary;
        }

        private static ModernButton MakeButton(string text, DialogResult result, ButtonKind kind)
        {
            return new ModernButton
            {
                Text = text,
                DialogResult = result,
                Kind = kind,
                Size = new Size(96, 38)
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                switch (iconKind)
                {
                    case MessageBoxIcon.Error: SystemSounds.Hand.Play(); break;
                    case MessageBoxIcon.Warning: SystemSounds.Exclamation.Play(); break;
                    case MessageBoxIcon.Question: SystemSounds.Question.Play(); break;
                    case MessageBoxIcon.Information: SystemSounds.Asterisk.Play(); break;
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>Round tinted badge with a one-character symbol, used by <see cref="AppDialog"/>.</summary>
    internal sealed class IconBadge : Control
    {
        private readonly MessageBoxIcon kind;

        public IconBadge(MessageBoxIcon kind)
        {
            this.kind = kind;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.Clear(Gfx.Backdrop(this));

            Color tone;
            string symbol;
            switch (kind)
            {
                case MessageBoxIcon.Error:
                    tone = Theme.Danger; symbol = "\u00D7"; break;
                case MessageBoxIcon.Warning:
                    tone = Theme.Warning; symbol = "!"; break;
                case MessageBoxIcon.Question:
                    tone = Theme.Accent; symbol = "?"; break;
                default:
                    tone = Theme.Accent; symbol = "i"; break;
            }

            Color backdrop = Gfx.Backdrop(this);
            using (var brush = new SolidBrush(Gfx.Mix(backdrop, tone, Theme.IsDark ? 0.22f : 0.13f)))
            {
                g.FillEllipse(brush, 0.5f, 0.5f, Width - 1f, Height - 1f);
            }

            Color symbolColor = Theme.IsDark ? Gfx.Mix(tone, Color.White, 0.25f) : tone;
            TextRenderer.DrawText(g, symbol, Theme.Font(17f, true), ClientRectangle, symbolColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>
    /// Small modeless progress window used while a batch of optimization steps
    /// (or the deep cleanup) is running. Callers keep driving it the same way
    /// as before: set a status line and a 0-100 value, and pump messages.
    /// </summary>
    internal sealed class ProgressDialog : Form
    {
        private readonly Label statusLabel;
        private readonly SlimProgress bar;
        private readonly ModernButton? closeButton;

        public ProgressDialog(string title, bool closable)
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = closable;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            Font = Theme.Font(9f);

            const int pad = 28;
            const int width = 480;

            statusLabel = new Label
            {
                Text = string.Empty,
                Font = Theme.Font(10f),
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.Surface,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(pad, pad),
                Size = new Size(width - pad * 2, 24)
            };

            bar = new SlimProgress
            {
                Location = new Point(pad, pad + 24 + 14),
                Size = new Size(width - pad * 2, 6)
            };

            Controls.Add(statusLabel);
            Controls.Add(bar);

            int height = pad + 24 + 14 + 6 + pad;

            if (closable)
            {
                var footer = new BufferedPanel
                {
                    Location = new Point(0, height - 6),
                    Size = new Size(width, 70),
                    BackColor = Theme.Canvas
                };
                footer.Paint += (s, e) =>
                {
                    using var pen = new Pen(Theme.Border, 1f);
                    e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
                };

                closeButton = new ModernButton
                {
                    Text = "Close",
                    Kind = ButtonKind.Ghost,
                    Size = new Size(96, 38),
                    Enabled = false,
                    Location = new Point(width - pad - 96, 16)
                };
                closeButton.Click += (s, e) => Close();
                footer.Controls.Add(closeButton);
                Controls.Add(footer);

                height = height - 6 + 70;
            }

            ClientSize = new Size(width, height);
        }

        public string Status
        {
            get => statusLabel.Text;
            set
            {
                if (IsDisposed) return;
                statusLabel.Text = value;
            }
        }

        public int Value
        {
            get => bar.Value;
            set
            {
                if (IsDisposed) return;
                bar.Value = value;
            }
        }

        /// <summary>Lets the user dismiss the window once the work is done.</summary>
        public void EnableClose()
        {
            if (IsDisposed || closeButton == null) return;
            closeButton.Enabled = true;
            closeButton.Kind = ButtonKind.Filled;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this);
        }
    }
}
