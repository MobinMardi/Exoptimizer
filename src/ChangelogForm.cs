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
    /// Shows docs/CHANGELOG.md inside the app, one tab per "## Version X.Y.Z"
    /// section (newest first, matching the file itself). The changelog is
    /// embedded into the assembly at build time (see Exoptimizer.csproj), so
    /// this always works regardless of whether CHANGELOG.md happens to be
    /// sitting next to the installed exe.
    /// </summary>
    internal sealed class ChangelogForm : Form
    {
        private static readonly Regex VersionHeaderPattern = new(@"^##\s*Version\s+(\S+)\s*$", RegexOptions.Compiled);
        private static readonly Regex BoldPattern = new(@"(\*\*[^*]+\*\*)", RegexOptions.Compiled);

        public ChangelogForm(Color cardColor, Color backgroundColor, Color textPrimary, Color textSecondary, Color accentColor, Color borderColor)
        {
            Text = "Exoptimizer - Changelog";
            Size = new Size(720, 560);
            MinimumSize = new Size(560, 400);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = backgroundColor;
            Font = new Font("Segoe UI", 9F);

            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(16, 6)
            };

            var sections = ParseChangelog(LoadChangelogText());

            if (sections.Count == 0)
            {
                var emptyTab = new TabPage("Changelog");
                emptyTab.Controls.Add(new Label
                {
                    Text = "No changelog information could be loaded.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    ForeColor = textSecondary
                });
                tabControl.TabPages.Add(emptyTab);
            }
            else
            {
                foreach (var (version, body) in sections)
                {
                    var tabPage = new TabPage($"v{version}") { BackColor = cardColor };

                    var richTextBox = new RichTextBox
                    {
                        Dock = DockStyle.Fill,
                        ReadOnly = true,
                        BorderStyle = BorderStyle.None,
                        BackColor = cardColor,
                        ForeColor = textPrimary,
                        Font = new Font("Segoe UI", 10F),
                        Margin = new Padding(12)
                    };

                    RenderMarkdownBody(richTextBox, body, textPrimary, accentColor);
                    richTextBox.Select(0, 0);
                    richTextBox.ScrollToCaret();

                    tabPage.Controls.Add(richTextBox);
                    tabControl.TabPages.Add(tabPage);
                }
            }

            var closeButton = new Button
            {
                Text = "Close",
                Dock = DockStyle.Bottom,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = accentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Click += (s, e) => Close();
            closeButton.Paint += (s, e) =>
            {
                using var pen = new Pen(borderColor, 1);
                e.Graphics.DrawLine(pen, 0, 0, closeButton.Width, 0);
            };

            Controls.Add(tabControl);
            Controls.Add(closeButton);
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
