using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Exoptimizer
{
    /// <summary>
    /// Shows docs/CHANGELOG.md inside the app, one entry per "## Version X.Y.Z"
    /// section (newest first, matching the file itself). The changelog is
    /// embedded into the assembly at build time (see Exoptimizer.csproj), so
    /// this always works regardless of whether CHANGELOG.md happens to be
    /// sitting next to the installed exe.
    /// </summary>
    internal sealed class ChangelogForm : Form
    {
        private static readonly Regex VersionHeaderPattern = new(@"^##\s*Version\s+(\S+)\s*$", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new(@"(\*\*[^*]+\*\*)", RegexOptions.Compiled);

        private readonly List<NavItem> versionItems = new();
        private RichTextBox? reader;

        public ChangelogForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            Text = "Exoptimizer - Changelog";
            ClientSize = new Size(780, 560);
            MinimumSize = new Size(640, 440);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Canvas;
            Font = Theme.Font(9f);
            ShowInTaskbar = false;

            var sections = ParseChangelog(LoadChangelogText());

            // Right-hand side: the reading area, with a footer holding the Close button.
            var main = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Theme.Canvas, Padding = new Padding(0, 20, 20, 0) };

            var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 68, BackColor = Theme.Canvas };
            var closeButton = new ModernButton
            {
                Text = "Close",
                Kind = ButtonKind.Filled,
                Size = new Size(110, 38),
                DialogResult = DialogResult.Cancel
            };
            closeButton.Location = new Point(footer.Width - 20 - closeButton.Width, 16);
            footer.Controls.Add(closeButton);
            footer.Resize += (s, e) => closeButton.Left = footer.Width - 20 - closeButton.Width;
            CancelButton = closeButton;

            var card = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(24, 20, 14, 20) };

            if (sections.Count == 0)
            {
                card.Controls.Add(new Label
                {
                    Text = "No changelog information could be loaded.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = Theme.TextSecondary,
                    BackColor = Theme.Surface,
                    Font = Theme.Font(10f)
                });
            }
            else
            {
                reader = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    BackColor = Theme.Surface,
                    ForeColor = Theme.TextPrimary,
                    Font = Theme.Font(10f),
                    DetectUrls = false
                };
                reader.HandleCreated += (s, e) => WindowChrome.ApplyScrollbarTheme(reader);
                card.Controls.Add(reader);
            }

            main.Controls.Add(card);
            main.Controls.Add(footer);

            // Left-hand side: one entry per version, newest first (the order of the file).
            var rail = new BufferedPanel
            {
                Dock = DockStyle.Left,
                Width = 200,
                BackColor = Theme.Canvas,
                AutoScroll = true,
                Padding = new Padding(16, 20, 8, 16)
            };
            rail.Paint += (s, e) =>
            {
                using var pen = new Pen(Theme.Border, 1f);
                e.Graphics.DrawLine(pen, rail.Width - 1, 0, rail.Width - 1, rail.Height);
            };

            rail.Controls.Add(new Label
            {
                Text = "Changelog",
                Font = Theme.Font(14f, true),
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = false,
                Bounds = new Rectangle(20, 20, 160, 28)
            });

            int y = 64;
            bool first = true;
            foreach (var (version, body) in sections)
            {
                string versionBody = body;
                var item = new NavItem
                {
                    Text = $"v{version}",
                    Location = new Point(16, y),
                    Size = new Size(168, 38),
                    Selected = first
                };
                item.Click += (s, e) => ShowVersion(item, versionBody);
                rail.Controls.Add(item);
                versionItems.Add(item);

                if (first && reader != null) ShowBody(versionBody);

                first = false;
                y += 42;
            }

            // Docking: the fill panel is added first so the rail claims its edge before it.
            Controls.Add(main);
            Controls.Add(rail);
        }

        private void ShowVersion(NavItem selected, string body)
        {
            foreach (var item in versionItems) item.Selected = item == selected;
            ShowBody(body);
        }

        private void ShowBody(string body)
        {
            if (reader == null) return;

            RenderMarkdownBody(reader, body, Theme.TextPrimary, Theme.AccentText);
            reader.Select(0, 0);
            reader.ScrollToCaret();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WindowChrome.Apply(this);
        }

        private static string LoadChangelogText()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("Exoptimizer.CHANGELOG.md");
                if (stream == null) return string.Empty;

                using var reader = new StreamReader(stream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>Splits the changelog into (version, body) pairs, in the order the headings appear in the file.</summary>
        private static List<(string Version, string Body)> ParseChangelog(string changelogText)
        {
            var sections = new List<(string, string)>();
            if (string.IsNullOrWhiteSpace(changelogText)) return sections;

            string[] lines = changelogText.Replace("\r\n", "\n").Split('\n');
            string? currentVersion = null;
            var currentBody = new StringBuilder();

            void FlushCurrent()
            {
                if (currentVersion != null)
                {
                    sections.Add((currentVersion, currentBody.ToString().Trim('\r', '\n')));
                }
            }

            foreach (string line in lines)
            {
                var match = VersionHeaderPattern.Match(line.TrimEnd());
                if (match.Success)
                {
                    FlushCurrent();
                    currentVersion = match.Groups[1].Value;
                    currentBody.Clear();
                    continue;
                }

                if (currentVersion != null)
                {
                    currentBody.AppendLine(line);
                }
            }

            FlushCurrent();
            return sections;
        }

        /// <summary>Lightweight Markdown-ish renderer: "### " headings, "- " bullets, and inline "**bold**" - enough for our own changelog without pulling in a full Markdown library.</summary>
        private static void RenderMarkdownBody(RichTextBox richTextBox, string body, Color textColor, Color headingColor)
        {
            richTextBox.Clear();
            string[] lines = body.Replace("\r\n", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                if (rawLine.StartsWith("### "))
                {
                    AppendRun(richTextBox, rawLine.Substring(4), headingColor, bold: true, sizeDelta: 1F);
                    richTextBox.AppendText(Environment.NewLine);
                    continue;
                }

                string trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith("- "))
                {
                    int indent = rawLine.Length - trimmed.Length;
                    AppendRun(richTextBox, new string(' ', indent) + "•  ", textColor, bold: false, sizeDelta: 0F);
                    AppendInlineBold(richTextBox, trimmed.Substring(2), textColor);
                    richTextBox.AppendText(Environment.NewLine);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    richTextBox.AppendText(Environment.NewLine);
                    continue;
                }

                AppendInlineBold(richTextBox, rawLine, textColor);
                richTextBox.AppendText(Environment.NewLine);
            }
        }

        private static void AppendInlineBold(RichTextBox richTextBox, string text, Color normalColor)
        {
            foreach (string part in BoldPattern.Split(text))
            {
                if (part.Length == 0) continue;

                if (part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4)
                {
                    AppendRun(richTextBox, part.Substring(2, part.Length - 4), normalColor, bold: true, sizeDelta: 0F);
                }
                else
                {
                    AppendRun(richTextBox, part, normalColor, bold: false, sizeDelta: 0F);
                }
            }
        }

        private static void AppendRun(RichTextBox richTextBox, string text, Color color, bool bold, float sizeDelta)
        {
            richTextBox.SelectionStart = richTextBox.TextLength;
            richTextBox.SelectionLength = 0;
            richTextBox.SelectionColor = color;
            richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, richTextBox.Font.Size + sizeDelta, bold ? FontStyle.Bold : FontStyle.Regular);
            richTextBox.AppendText(text);
        }
    }
}
