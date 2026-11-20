using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>Lets the user choose which cleanup categories to run, with the size of each shown up front.</summary>
    internal sealed class CleanupDialog : Form
    {
        private const int Width0 = 660;
        private const int Pad = 28;
        private const int RowHeight = 62;

        private readonly List<CleanupCategory> categories;
        private readonly ModernButton cleanButton;

        public CleanupDialog(List<CleanupCategory> categories)
        {
            this.categories = categories;

            AutoScaleMode = AutoScaleMode.None;
            Text = "Deep System Cleanup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Surface;
            Font = Theme.Font(9f);

            Controls.Add(new Label
            {
                Text = "Choose what to clean",
                Font = Theme.Font(14f, true),
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.Surface,
                AutoSize = false,
                UseMnemonic = false,
                Bounds = new Rectangle(Pad, 22, Width0 - Pad * 2, 30)
            });

            Controls.Add(new Label
            {
                Text = "Files that are in use are skipped. Logs and crash dumps are kept unless you select them.",
                Font = Theme.Font(9.5f),
                ForeColor = Theme.TextSecondary,
                BackColor = Theme.Surface,
                AutoSize = false,
                UseMnemonic = false,
                Bounds = new Rectangle(Pad, 54, Width0 - Pad * 2, 22)
            });

            int listTop = 92;
            int listWidth = Width0 - Pad * 2;

            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                int top = listTop + i * RowHeight;

                if (i > 0)
                {
                    Controls.Add(new Panel { Bounds = new Rectangle(Pad, top, listWidth, 1), BackColor = Theme.Border });
                }

                Controls.Add(new Label
                {
                    Text = category.Title,
                    Font = Theme.Font(10f, true),
                    ForeColor = Theme.TextPrimary,
                    BackColor = Theme.Surface,
                    AutoSize = false,
                    UseMnemonic = false,
                    Bounds = new Rectangle(Pad, top + 8, listWidth - 250, 22)
                });

                Controls.Add(new Label
                {
                    Text = category.Description,
                    Font = Theme.Font(9f),
                    ForeColor = Theme.TextSecondary,
                    BackColor = Theme.Surface,
                    AutoSize = false,
                    UseMnemonic = false,
                    Bounds = new Rectangle(Pad, top + 31, listWidth - 250, 22)
                });

                string size = category.Files == 0
                    ? "Nothing to clean"
                    : $"{CleanupService.FormatBytes(category.Bytes)} · {category.Files:N0} files";

                Controls.Add(new Label
                {
                    Text = size,
                    Font = Theme.Font(9.5f),
                    ForeColor = Theme.TextPrimary,
                    BackColor = Theme.Surface,
                    AutoSize = false,
                    UseMnemonic = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Bounds = new Rectangle(Pad + listWidth - 250 + 10, top + 16, 180, 24)
                });

                var toggle = new ToggleSwitch
                {
                    Location = new Point(Pad + listWidth - 48, top + 14),
                    Checked = category.Selected
                };
                toggle.CheckedChanged += (s, e) =>
                {
                    category.Selected = toggle.Checked;
                    UpdateButton();
                };
                Controls.Add(toggle);
            }

            int footerTop = listTop + categories.Count * RowHeight + 16;
            const int footerHeight = 72;

            var footer = new BufferedPanel
            {
                Location = new Point(0, footerTop),
                Size = new Size(Width0, footerHeight),
                BackColor = Theme.Canvas
            };
            footer.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border, 1f);
                e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
            };

            var cancel = new ModernButton
            {
                Text = "Cancel",
                Kind = ButtonKind.Ghost,
                DialogResult = DialogResult.Cancel,
                Size = new Size(110, 38),
                Location = new Point(Width0 - Pad - 110, 17)
            };

            cleanButton = new ModernButton
            {
                Text = "Clean",
                Kind = ButtonKind.Filled,
                DialogResult = DialogResult.OK,
                Size = new Size(190, 38)
            };
            cleanButton.Location = new Point(cancel.Left - 10 - cleanButton.Width, 17);

            footer.Controls.Add(cancel);
            footer.Controls.Add(cleanButton);
            Controls.Add(footer);

            AcceptButton = cleanButton;
            CancelButton = cancel;

            ClientSize = new Size(Width0, footerTop + footerHeight);
            UpdateButton();
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IReadOnlyList<CleanupCategory> SelectedCategories => categories.Where(c => c.Selected).ToList();

        private void UpdateButton()
        {
            var selected = categories.Where(c => c.Selected).ToList();
            cleanButton.Enabled = selected.Count > 0;
            cleanButton.Text = selected.Count == 0 ? "Select something to clean" : $"Clean {CleanupService.FormatBytes(selected.Sum(c => c.Bytes))}";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this);
        }
    }
}
