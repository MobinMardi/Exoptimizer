using System.ComponentModel;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>A plain panel with double buffering turned on (Panel.DoubleBuffered is protected).</summary>
    internal class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
        }
    }

    /// <summary>Rounded, softly outlined surface that groups related controls.</summary>
    internal sealed class CardPanel : Panel
    {
        private Color fillColor = Theme.Surface;
        private Color borderColor = Theme.Border;
        private int radius = 14;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color FillColor { get => fillColor; set { fillColor = value; Invalidate(); } }

        /// <summary>Use Color.Transparent for a card with no outline.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get => borderColor; set { borderColor = value; Invalidate(); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get => radius; set { radius = value; Invalidate(); } }

        public CardPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Canvas;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            Gfx.Smooth(g);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var path = Gfx.RoundedRect(rect, radius);
            using (var brush = new SolidBrush(fillColor))
            {
                g.FillPath(brush, path);
            }

            if (borderColor.A > 0)
            {
                using var pen = new Pen(borderColor, 1f);
                g.DrawPath(pen, path);
            }
        }
    }

    internal enum ButtonKind
    {
        /// <summary>Solid fill taken from BackColor.</summary>
        Filled,
        /// <summary>Quiet tinted fill (accent by default, or <see cref="ModernButton.Tint"/>).</summary>
        Soft,
        /// <summary>Outline only.</summary>
        Ghost
    }

    /// <summary>
    /// Flat, rounded button. For <see cref="ButtonKind.Filled"/> the fill is the
    /// control's BackColor, so code that recolors a button (for example turning
    /// it green once an optimization has been applied) keeps working, and the
    /// label color is picked automatically for readability.
    /// </summary>
    internal sealed class ModernButton : Button
    {
        private bool hover;
        private bool pressed;
        private ButtonKind kind = ButtonKind.Filled;
        private Color tint = Color.Empty;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ButtonKind Kind { get => kind; set { kind = value; Invalidate(); } }

        /// <summary>Optional semantic color (danger, warning...) for Soft and Ghost buttons.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Tint { get => tint; set { tint = value; Invalidate(); } }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Radius { get; set; } = 10;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Font = Theme.Font(9.5f, true);
            BackColor = Theme.Accent;
            Size = new Size(140, 40);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            hover = false;
            pressed = false;
            base.OnEnabledChanged(e);
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        private static Color Shade(Color c, float amount)
        {
            return Theme.IsDark ? Gfx.Mix(c, Color.White, amount) : Gfx.Mix(c, Color.Black, amount);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);

            Color backdrop = Gfx.Backdrop(this);
            g.Clear(backdrop);

            bool enabled = Enabled;
            Color fill;
            Color border = Color.Empty;
            Color text;

            switch (kind)
            {
                case ButtonKind.Filled:
                {
                    Color baseFill = BackColor;
                    text = Gfx.ReadableOn(baseFill);
                    if (!enabled)
                    {
                        fill = Gfx.Mix(baseFill, backdrop, 0.55f);
                        text = Gfx.Mix(text, fill, 0.30f);
                    }
                    else
                    {
                        fill = pressed ? Shade(baseFill, 0.14f) : hover ? Shade(baseFill, 0.08f) : baseFill;
                    }
                    break;
                }

                case ButtonKind.Soft:
                {
                    Color toneText = tint != Color.Empty ? tint : Theme.AccentText;
                    Color baseFill = tint != Color.Empty ? Gfx.Mix(backdrop, tint, 0.13f) : Theme.AccentSoft;
                    text = toneText;
                    if (!enabled)
                    {
                        fill = Gfx.Mix(baseFill, backdrop, 0.5f);
                        text = Gfx.Mix(toneText, backdrop, 0.55f);
                    }
                    else
                    {
                        fill = pressed ? Gfx.Mix(baseFill, toneText, 0.20f) : hover ? Gfx.Mix(baseFill, toneText, 0.10f) : baseFill;
                    }
                    break;
                }

                default: // Ghost
                {
                    Color toneText = tint != Color.Empty ? tint : Theme.TextPrimary;
                    Color toneBorder = tint != Color.Empty ? Gfx.Mix(backdrop, tint, 0.45f) : Theme.BorderStrong;
                    text = toneText;
                    border = toneBorder;
                    if (!enabled)
                    {
                        fill = backdrop;
                        text = Theme.TextTertiary;
                        border = Theme.Border;
                    }
                    else
                    {
                        fill = pressed ? Gfx.Mix(Theme.Hover, Theme.BorderStrong, 0.35f) : hover ? Theme.Hover : backdrop;
                    }
                    break;
                }
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = Gfx.RoundedRect(rect, Radius))
            {
                using (var brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }

                if (border != Color.Empty)
                {
                    using var pen = new Pen(border, 1f);
                    g.DrawPath(pen, path);
                }
            }

            if (enabled && Focused && ShowFocusCues)
            {
                var ringRect = RectangleF.Inflate(rect, -3f, -3f);
                using var ringPath = Gfx.RoundedRect(ringRect, Math.Max(2, Radius - 3));
                using var ringPen = new Pen(Color.FromArgb(180, text), 1.5f);
                g.DrawPath(ringPen, ringPath);
            }

            var textBounds = Rectangle.Inflate(ClientRectangle, -10, 0);
            TextRenderer.DrawText(g, Text, Font, textBounds, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// Pill-shaped on/off switch. It derives from CheckBox, so Checked and
    /// CheckedChanged behave exactly as before; only the drawing is different.
    /// </summary>
    internal sealed class ToggleSwitch : CheckBox
    {
        private float position;
        private readonly System.Windows.Forms.Timer animationTimer = new() { Interval = 15 };

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Text = string.Empty;
            Size = new Size(48, 28);
            Cursor = Cursors.Hand;
            animationTimer.Tick += (s, e) => Step();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!animationTimer.Enabled) position = Checked ? 1f : 0f;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);

            if (!IsHandleCreated)
            {
                position = Checked ? 1f : 0f;
                return;
            }

            animationTimer.Start();
        }

        private void Step()
        {
            float target = Checked ? 1f : 0f;
            if (position < target) position = Math.Min(target, position + 0.17f);
            else if (position > target) position = Math.Max(target, position - 0.17f);

            Invalidate();
            if (Math.Abs(position - target) < 0.001f) animationTimer.Stop();
        }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);

            Color backdrop = Gfx.Backdrop(this);
            g.Clear(backdrop);

            var track = new RectangleF(1f, (Height - 24f) / 2f, Width - 2f, 24f);
            Color trackColor = Gfx.Mix(Theme.BorderStrong, Theme.Accent, position);
            if (!Enabled) trackColor = Gfx.Mix(trackColor, backdrop, 0.55f);

            using (var path = Gfx.RoundedRect(track, 12f))
            using (var brush = new SolidBrush(trackColor))
            {
                g.FillPath(brush, path);
            }

            const float knobSize = 18f;
            float travel = track.Width - knobSize - 6f;
            float knobX = track.X + 3f + travel * position;
            var knob = new RectangleF(knobX, track.Y + (track.Height - knobSize) / 2f, knobSize, knobSize);

            using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
            {
                g.FillEllipse(shadow, knob.X, knob.Y + 1f, knob.Width, knob.Height);
            }

            using (var brush = new SolidBrush(Enabled ? Color.White : Gfx.Mix(Color.White, backdrop, 0.4f)))
            {
                g.FillEllipse(brush, knob);
            }

            if (Focused && ShowFocusCues)
            {
                var ring = RectangleF.Inflate(track, 2.5f, 2.5f);
                using var ringPath = Gfx.RoundedRect(ring, 14f);
                using var pen = new Pen(Theme.AccentText, 1.5f);
                g.DrawPath(pen, ringPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) animationTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Sidebar navigation entry: quiet by default, tinted with an accent bar when selected.</summary>
    internal sealed class NavItem : Control
    {
        private bool hover;
        private bool selected;
        private bool showDot;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Section { get; set; } = string.Empty;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Selected { get => selected; set { selected = value; Invalidate(); } }

        /// <summary>Small red dot used to flag an available update.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowDot { get => showDot; set { showDot = value; Invalidate(); } }

        public NavItem()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(212, 42);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnEnabledChanged(EventArgs e) { hover = false; base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.Clear(Gfx.Backdrop(this));

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);

            if (selected || (hover && Enabled))
            {
                using var path = Gfx.RoundedRect(rect, 10f);
                using var brush = new SolidBrush(selected ? Theme.AccentSoft : Theme.Hover);
                g.FillPath(brush, path);
            }

            if (selected)
            {
                var bar = new RectangleF(8f, Height / 2f - 8f, 3f, 16f);
                using var barPath = Gfx.RoundedRect(bar, 1.5f);
                using var barBrush = new SolidBrush(Theme.Accent);
                g.FillPath(barBrush, barPath);
            }

            Color textColor = !Enabled ? Theme.TextTertiary : selected ? Theme.AccentText : hover ? Theme.TextPrimary : Theme.TextSecondary;
            var textBounds = new Rectangle(26, 0, Width - 26 - (showDot ? 34 : 12), Height);
            TextRenderer.DrawText(g, Text, Theme.Font(10f, selected), textBounds, textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            if (showDot)
            {
                using var dot = new SolidBrush(Theme.Danger);
                g.FillEllipse(dot, Width - 24f, Height / 2f - 4f, 8f, 8f);
            }

            if (Focused && ShowFocusCues)
            {
                using var ringPath = Gfx.RoundedRect(RectangleF.Inflate(rect, -1f, -1f), 10f);
                using var pen = new Pen(Theme.AccentText, 1.5f);
                g.DrawPath(pen, ringPath);
            }
        }
    }

    /// <summary>A label with a leading status dot. The dot takes ForeColor; the text stays in the primary text color.</summary>
    internal sealed class StatusLabel : Label
    {
        public StatusLabel()
        {
            AutoSize = false;
            UseMnemonic = false;
            BackColor = Color.Transparent;
            ForeColor = Theme.TextSecondary;
            Font = Theme.Font(10f);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);

            const float dot = 8f;
            using (var brush = new SolidBrush(ForeColor))
            {
                g.FillEllipse(brush, 1f, Height / 2f - dot / 2f, dot, dot);
            }

            var bounds = new Rectangle(20, 0, Math.Max(0, Width - 20), Height);
            TextRenderer.DrawText(g, Text, Font, bounds, Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Self-contained live-reading card: a title, one large value, a caption and a thin gauge.</summary>
    internal sealed class MetricTile : Control
    {
        private string valueText = "-";
        private string caption = string.Empty;
        private double fraction;
        private Color tone = Theme.TextTertiary;

        public MetricTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(264, 132);
        }

        public void SetState(string value, string captionText, double gaugeFraction, Color toneColor)
        {
            valueText = value;
            caption = captionText;
            fraction = Math.Max(0, Math.Min(1, gaugeFraction));
            tone = toneColor;
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.Clear(Gfx.Backdrop(this));

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = Gfx.RoundedRect(rect, 14f))
            {
                using (var brush = new SolidBrush(Theme.Surface)) g.FillPath(brush, path);
                using (var pen = new Pen(Theme.Border, 1f)) g.DrawPath(pen, path);
            }

            using (var dot = new SolidBrush(tone))
            {
                g.FillEllipse(dot, 22f, 22f, 8f, 8f);
            }

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, Text, Theme.Font(9.5f), new Rectangle(38, 14, Width - 60, 24), Theme.TextSecondary, flags);
            TextRenderer.DrawText(g, valueText, Theme.Font(24f, true), new Rectangle(22, 40, Width - 44, 46), Theme.TextPrimary, flags);
            TextRenderer.DrawText(g, caption, Theme.Font(9f), new Rectangle(22, 85, Width - 44, 18), Theme.TextSecondary, flags);

            var track = new RectangleF(22f, Height - 26f, Width - 44f, 5f);
            using (var trackPath = Gfx.RoundedRect(track, 2.5f))
            using (var trackBrush = new SolidBrush(Theme.Border))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float fillWidth = (float)(track.Width * fraction);
            if (fillWidth >= 5f)
            {
                var fillRect = new RectangleF(track.X, track.Y, fillWidth, track.Height);
                using var fillPath = Gfx.RoundedRect(fillRect, 2.5f);
                using var fillBrush = new SolidBrush(tone);
                g.FillPath(fillBrush, fillPath);
            }
        }
    }

    /// <summary>Clickable card for launching a Windows tool: a name, a one-line description, and a hover state.</summary>
    internal sealed class ToolTile : Control
    {
        private bool hover;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Description { get; set; } = string.Empty;

        public ToolTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(254, 72);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }

        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }

        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || keyData == Keys.Space || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.Clear(Gfx.Backdrop(this));

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var path = Gfx.RoundedRect(rect, 12f))
            {
                using (var brush = new SolidBrush(hover ? Theme.AccentSoft : Theme.Canvas)) g.FillPath(brush, path);
                using var pen = new Pen(hover ? Gfx.Mix(Theme.Border, Theme.Accent, 0.55f) : Theme.Border, 1f);
                g.DrawPath(pen, path);
            }

            const TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                                          TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, Text, Theme.Font(10.5f, true), new Rectangle(18, 11, Width - 36, 26),
                hover ? Theme.AccentText : Theme.TextPrimary, flags);
            TextRenderer.DrawText(g, Description, Theme.Font(9f), new Rectangle(18, 40, Width - 36, 22),
                Theme.TextSecondary, flags);

            if (Focused && ShowFocusCues)
            {
                using var ringPath = Gfx.RoundedRect(RectangleF.Inflate(rect, -2f, -2f), 10f);
                using var ringPen = new Pen(Theme.AccentText, 1.5f);
                g.DrawPath(ringPen, ringPath);
            }
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
    }

    /// <summary>Thin rounded progress bar (0-100).</summary>
    internal sealed class SlimProgress : Control
    {
        private int value;

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int Value
        {
            get => value;
            set { this.value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public SlimProgress()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 6;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Gfx.Smooth(g);
            g.Clear(Gfx.Backdrop(this));

            var track = new RectangleF(0f, 0f, Width, Height);
            using (var path = Gfx.RoundedRect(track, Height / 2f))
            using (var brush = new SolidBrush(Theme.Border))
            {
                g.FillPath(brush, path);
            }

            float fillWidth = Width * (value / 100f);
            if (fillWidth >= Height)
            {
                using var fillPath = Gfx.RoundedRect(new RectangleF(0f, 0f, fillWidth, Height), Height / 2f);
                using var fillBrush = new SolidBrush(Theme.Accent);
                g.FillPath(fillBrush, fillPath);
            }
        }
    }
}
