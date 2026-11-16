using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Nikud
{
    internal sealed class Settings : Form
    {
        private readonly Hotkey hotkey;
        private readonly TextBox shortcut = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
        private readonly ControlTaps captureTaps = new ControlTaps();
        private readonly Label status = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(380, 0) };
        private readonly Label activity = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, MaximumSize = new Size(380, 0) };
        private readonly string executable = "\"" + Application.ExecutablePath + "\" --tray";
        internal event Action OpenEditor;
        internal bool CapturingShortcut { get { return shortcut.Focused; } }
        internal bool ReviewBeforeApplying { get { return review.Checked; } }
        private readonly CheckBox review = new CheckBox { Text = "Review new nikud before applying", AutoSize = true };
        private const string Preferences = @"Software\Nikud";
        private const string Startup = @"Software\Microsoft\Windows\CurrentVersion\Run";

        internal Settings(Hotkey hotkey)
        {
            this.hotkey = hotkey;
            Text = "Nikud Settings";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(430, 350);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 17, 22, 14), ColumnCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            var title = new Label { Text = "Nikud " + Application.ProductVersion, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 3, 3, 9) };
            AddFullRow(layout, title, 0);
            layout.Controls.Add(new Label { Text = "Vocalize selection:", AutoSize = true, Anchor = AnchorStyles.Right }, 0, 1);
            shortcut.AccessibleName = "Vocalize selection shortcut";
            layout.Controls.Add(shortcut, 1, 1);
            var clear = new Button { Text = "Clear", Dock = DockStyle.Fill, AutoSize = true };
            clear.Click += (sender, args) => SaveShortcut(0, Keys.None);
            layout.Controls.Add(clear, 2, 1);
            AddFullRow(layout, new Label
            {
                Text = "Click the shortcut box and press an F-key, tap Ctrl twice, or press a combination with Ctrl, Alt, or Win.",
                AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 5, 3, 8),
            }, 2);
            AddFullRow(layout, status, 3);
            var startup = new CheckBox { Text = "Start with Windows", AutoSize = true, Margin = new Padding(3, 9, 3, 8) };
            startup.Checked = string.Equals(Registry.GetValue(@"HKEY_CURRENT_USER\" + Startup, "Nikud", "") as string, executable, StringComparison.OrdinalIgnoreCase);
            startup.CheckedChanged += (sender, args) =>
            {
                try
                {
                    using (var registry = Registry.CurrentUser.CreateSubKey(Startup))
                    {
                        if (startup.Checked) registry.SetValue("Nikud", executable);
                        else registry.DeleteValue("Nikud", false);
                    }
                }
                catch (Exception) { status.Text = "Windows could not update the startup setting."; }
            };
            AddFullRow(layout, startup, 4);
            AddFullRow(layout, new Label
            {
                Text = Engine.Available ? "Vocalizer: Nakdimon · offline" : "Extract the entire ZIP to install the offline vocalizer.",
                AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = SystemColors.GrayText,
                Margin = new Padding(3, 5, 3, 4),
            }, 5);
            AddFullRow(layout, activity, 6);
            var test = new Button { Text = "Open text editor…", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
            test.Click += (sender, args) => { if (OpenEditor != null) OpenEditor(); };
            AddFullRow(layout, test, 7);
            var reports = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            var bug = new Button { Text = "Report a bug…", AutoSize = true };
            var word = new Button { Text = "Report a word mistake…", AutoSize = true };
            bug.Click += (sender, args) => IssueReports.Open(false);
            word.Click += (sender, args) => IssueReports.Open(true);
            reports.Controls.Add(bug);
            reports.Controls.Add(word);
            AddFullRow(layout, reports, 8);
            review.Checked = (int?)Registry.GetValue(@"HKEY_CURRENT_USER\" + Preferences, "Review", 1) != 0;
            review.CheckedChanged += (sender, args) =>
            {
                try { Registry.SetValue(@"HKEY_CURRENT_USER\" + Preferences, "Review", review.Checked ? 1 : 0); }
                catch (Exception) { status.Text = "Windows could not save the review preference."; }
            };
            AddFullRow(layout, review, 9);
            Controls.Add(layout);

            shortcut.KeyDown += CaptureShortcut;
            shortcut.KeyUp += (sender, args) =>
            {
                if (args.KeyCode == Keys.ControlKey && captureTaps.Input(Keys.ControlKey, true, Environment.TickCount))
                    SaveShortcut(0, Hotkey.DoubleCtrl);
            };
            shortcut.Leave += (sender, args) => captureTaps.Cancel();
            FormClosing += (sender, args) =>
            {
                if (args.CloseReason == CloseReason.UserClosing) { args.Cancel = true; Hide(); }
            };
            var stored = Registry.GetValue(@"HKEY_CURRENT_USER\" + Preferences, "Shortcut", "6:74") as string;
            var parts = (stored ?? "").Split(':');
            uint modifiers;
            int code;
            bool loaded = parts.Length == 2 && uint.TryParse(parts[0], out modifiers)
                && int.TryParse(parts[1], out code) && hotkey.Set(modifiers, (Keys)code);
            RefreshShortcut();
            if (!loaded) status.Text = "The saved shortcut is unavailable. Choose another combination.";
        }

        internal string ShortcutStatus
        {
            get { return hotkey.Key == Keys.None ? "Open Nikud Settings and choose a shortcut." : "Select Hebrew text and press " + shortcut.Text + "."; }
        }

        internal void ShowActivity(string message)
        {
            activity.Text = message == ShortcutStatus ? "" : message;
        }

        private static void AddFullRow(TableLayoutPanel layout, Control control, int row)
        {
            layout.Controls.Add(control, 0, row);
            layout.SetColumnSpan(control, 3);
        }

        private void CaptureShortcut(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey && !e.Alt && !e.Shift && !Hotkey.Down(Keys.LWin) && !Hotkey.Down(Keys.RWin))
            {
                captureTaps.Input(Keys.ControlKey, false, Environment.TickCount);
                return;
            }
            captureTaps.Cancel();
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu || e.KeyCode == Keys.ShiftKey ||
                e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin) return;
            e.SuppressKeyPress = true;
            uint modifiers = 0;
            if (e.Control) modifiers |= 2;
            if (e.Alt) modifiers |= 1;
            if (e.Shift) modifiers |= 4;
            if (Hotkey.Down(Keys.LWin) || Hotkey.Down(Keys.RWin)) modifiers |= 8;
            if (!Hotkey.IsAllowed(modifiers, e.KeyCode))
            {
                status.Text = modifiers == 2 && (e.KeyCode == Keys.C || e.KeyCode == Keys.V)
                    ? "Ctrl+C and Ctrl+V are used to transfer the selection."
                    : "Choose an F-key, tap Ctrl twice, or use Ctrl, Alt, or Win in a combination.";
                return;
            }
            SaveShortcut(modifiers, e.KeyCode);
        }

        private void SaveShortcut(uint modifiers, Keys shortcutKey)
        {
            if (!hotkey.Set(modifiers, shortcutKey))
            {
                status.Text = "That shortcut is unavailable. Choose a different combination.";
                return;
            }
            RefreshShortcut();
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\" + Preferences, "Shortcut", modifiers + ":" + (int)shortcutKey);
            }
            catch (Exception) { status.Text = "Shortcut set for this session. Windows could not save it."; }
        }

        private void RefreshShortcut()
        {
            shortcut.Text = Hotkey.Describe(hotkey.Modifiers, hotkey.Key);
            status.Text = ShortcutStatus;
        }
    }

    internal sealed class TextEditor : Form
    {
        private readonly TextBox source = TextField(false);
        private readonly TextBox output = TextField(true);
        private readonly Button run = new Button { Text = "Add or remove nikud", AutoSize = true };
        private readonly Button copy = new Button { Text = "Copy result", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(490, 0) };

        internal TextEditor(Icon artwork)
        {
            Text = "Nikud — text editor";
            Icon = artwork;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(530, 330);
            MinimumSize = new Size(460, 310);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 7, ColumnCount = 1 };
            for (int row = 0; row < 7; row++)
                layout.RowStyles.Add(row == 1 || row == 4 ? new RowStyle(SizeType.Percent, 50) : new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "Hebrew text", AutoSize = true }, 0, 0);
            layout.Controls.Add(source, 0, 1);
            layout.Controls.Add(run, 0, 2);
            layout.Controls.Add(new Label { Text = "Result", AutoSize = true }, 0, 3);
            layout.Controls.Add(output, 0, 4);
            layout.Controls.Add(copy, 0, 5);
            layout.Controls.Add(status, 0, 6);
            Controls.Add(layout);
            source.MaxLength = 1500;
            source.TextChanged += (sender, args) => { output.Clear(); copy.Enabled = false; status.Text = ""; };
            run.Click += async (sender, args) =>
            {
                var text = source.Text;
                source.ReadOnly = true;
                run.Enabled = false;
                copy.Enabled = false;
                output.Clear();
                status.Text = "Processing text…";
                try
                {
                    var words = await Engine.VocalizeAsync(text);
                    if (IsDisposed) return;
                    if (!Engine.IsRemoval(text))
                    {
                        using (var dialog = new ReadingsDialog(text, words, artwork))
                        {
                            if (dialog.ShowDialog(this) != DialogResult.OK) { status.Text = "Cancelled."; return; }
                            output.Text = dialog.ResultText;
                        }
                    }
                    else output.Text = Engine.Render(text, words);
                    copy.Enabled = output.TextLength > 0;
                    status.Text = output.Text == text ? "The text already matches the result." : "Nikud updated.";
                }
                catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
                finally { if (!IsDisposed) { run.Enabled = true; source.ReadOnly = false; } }
            };
            copy.Click += (sender, args) =>
            {
                try { Clipboard.SetText(output.Text); status.Text = "Copied."; }
                catch (ExternalException) { status.Text = "The clipboard is busy. Try again."; }
            };
        }

        private static TextBox TextField(bool readOnly)
        {
            return new TextBox
            {
                Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical,
                RightToLeft = RightToLeft.Yes, ReadOnly = readOnly, Font = new Font("Segoe UI", 13),
            };
        }
    }

    internal sealed class ReadingsDialog : Form
    {
        private readonly Dictionary<Word, ComboBox> choices = new Dictionary<Word, ComboBox>();
        private readonly Button apply = new Button { Text = "Apply", AutoSize = true };
        internal string ResultText { get; private set; }
        private readonly TextBox preview = new TextBox
        {
            Multiline = true, Height = 64, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes,
            ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 13),
        };
        private readonly string source;
        private readonly List<Word> words;

        internal ReadingsDialog(string source, List<Word> words, Icon artwork, IntPtr target = default(IntPtr))
        {
            this.source = source;
            this.words = words;
            Text = "Nikud " + Application.ProductVersion + " — choose a reading";
            Icon = artwork;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            if (target != IntPtr.Zero)
            {
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
            }
            var ambiguous = words.Where(word => word.Readings.Count > 1).ToList();
            ClientSize = new Size(460, Math.Min(510, 185 + (ambiguous.Count + words.Count(word => word.Compound != null)) * 65));
            if (target != IntPtr.Zero)
            {
                var area = Screen.FromHandle(target).WorkingArea;
                Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                                     area.Top + Math.Max(0, (area.Height - Height) / 2));
            }
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var instruction = new Label { Text = "Review the suggested nikud before applying.", AutoSize = true };
            layout.Controls.Add(instruction, 0, 0);
            var list = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var rows = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = 1 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < words.Count; i++)
            {
                var word = words[i];
                if (word.Compound != null)
                {
                    var next = words[i + 1];
                    var original = source.Substring(word.Start, next.Start + next.Original.Length - word.Start);
                    rows.Controls.Add(new Label { Text = original + "  (צירוף)", RightToLeft = RightToLeft.Yes, AutoSize = true, Dock = DockStyle.Fill });
                    var phrase = new ComboBox
                    {
                        AccessibleName = "Phrase reading for " + original, RightToLeft = RightToLeft.Yes,
                        Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList,
                        Font = new Font("Segoe UI", 11),
                    };
                    phrase.Items.Add(string.Join(" ", word.Compound.Parts) + " — " + word.Compound.Meaning);
                    phrase.Items.Add("בחירת ניקוד לכל מילה בנפרד");
                    phrase.SelectedIndex = 0;
                    phrase.SelectedIndexChanged += (sender, args) =>
                    {
                        word.Compound.Selected = phrase.SelectedIndex == 0;
                        foreach (var member in new[] { word, next })
                        {
                            ComboBox memberChoice;
                            if (choices.TryGetValue(member, out memberChoice)) memberChoice.Enabled = !word.Compound.Selected;
                        }
                        UpdateApply();
                    };
                    rows.Controls.Add(phrase);
                }
                if (word.Readings.Count <= 1) continue;
                var start = Math.Max(0, word.Start - 17);
                var end = Math.Min(source.Length, word.Start + word.Original.Length + 17);
                var context = source.Substring(start, end - start).Replace('\r', ' ').Replace('\n', ' ');
                var label = new Label { Text = context.Trim() == word.Original ? word.Original : word.Original + "  (" + context + ")", RightToLeft = RightToLeft.Yes, AutoSize = true, Dock = DockStyle.Fill };
                rows.Controls.Add(label);
                var select = new ComboBox {
                    AccessibleName = "Reading for " + word.Original, RightToLeft = RightToLeft.Yes, Dock = DockStyle.Top,
                    DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 11),
                };
                foreach (var reading in word.Readings)
                    select.Items.Add(string.IsNullOrEmpty(reading.Meaning) ? reading.Text : reading.Text + " — " + reading.Meaning);
                select.SelectedIndex = word.Selected;
                select.SelectedIndexChanged += (sender, args) => UpdateApply();
                select.Enabled = word.Compound == null && (i == 0 || words[i - 1].Compound == null);
                choices.Add(word, select);
                rows.Controls.Add(select);
            }
            list.Controls.Add(rows);
            layout.Controls.Add(list, 0, 1);
            var palette = new NikudPalette(preview) { Visible = false };
            layout.Controls.Add(palette, 0, 2);
            preview.AccessibleName = "Proposed text";
            layout.Controls.Add(preview, 0, 3);
            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
            apply.Click += (sender, args) =>
            {
                ResultText = preview.ReadOnly ? Engine.Render(source, words) : preview.Text;
                DialogResult = DialogResult.OK;
            };
            var edit = new Button { Text = "Edit nikud…", AutoSize = true };
            edit.Click += (sender, args) =>
            {
                preview.ReadOnly = !preview.ReadOnly;
                list.Enabled = preview.ReadOnly;
                Height += preview.ReadOnly ? -70 : 70;
                palette.Visible = !preview.ReadOnly;
                instruction.Text = preview.ReadOnly ? "Review the suggested nikud before applying."
                    : "Place the cursor after a Hebrew letter, then choose a mark.";
                edit.Text = preview.ReadOnly ? "Edit nikud…" : "Use suggestions";
                UpdateApply();
                if (!preview.ReadOnly) preview.Focus();
            };
            preview.TextChanged += (sender, args) =>
            {
                if (preview.ReadOnly) return;
                apply.Enabled = Engine.SameSpelling(source, preview.Text);
                instruction.Text = apply.Enabled ? "Place the cursor after a Hebrew letter, then choose a mark."
                    : "Keep the original letters, spacing, and punctuation.";
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(apply);
            buttons.Controls.Add(edit);
            layout.Controls.Add(buttons, 0, 4);
            Controls.Add(layout);
            AcceptButton = apply;
            CancelButton = cancel;
            UpdateApply();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (TopMost) BeginInvoke(new Action(() =>
            {
                if (!Visible) return;
                BringToFront();
                Activate();
            }));
        }

        private void UpdateApply()
        {
            if (!preview.ReadOnly)
            {
                apply.Enabled = Engine.SameSpelling(source, preview.Text);
                return;
            }
            foreach (var choice in choices) choice.Key.Selected = choice.Value.SelectedIndex;
            preview.Text = Engine.Render(source, words);
            apply.Enabled = choices.Values.All(choice => !choice.Enabled || choice.SelectedIndex >= 0);
        }
    }

    internal sealed class NikudPalette : FlowLayoutPanel
    {
        private readonly TextBox editor;
        private readonly Dictionary<char, Button> buttons = new Dictionary<char, Button>();
        private readonly ToolTip tips = new ToolTip();
        private readonly Font markFont = new Font("Segoe UI", 12);
        internal const char Clear = '\0';
        internal const char Shuruq = '\u0001';
        internal const char HolamHaser = '\u05ba';
        internal const char HolamMale = '\u05b9';

        internal NikudPalette(TextBox editor)
        {
            this.editor = editor;
            AutoSize = true;
            Dock = DockStyle.Fill;
            FlowDirection = FlowDirection.RightToLeft;
            Add(HolamHaser, "חולם חסר — נקודה משמאל למעלה");
            Add(HolamMale, "חולם מלא — נקודה מעל ו בלבד");
            Add(Shuruq, "שורוק — נקודה באמצע ו");
            Add('\u05b0', "שווא");
            Add('\u05b1', "חטף סגול");
            Add('\u05b2', "חטף פתח");
            Add('\u05b3', "חטף קמץ");
            Add('\u05b4', "חיריק");
            Add('\u05b5', "צירי");
            Add('\u05b6', "סגול");
            Add('\u05b7', "פתח");
            Add('\u05b8', "קמץ");
            Add('\u05c7', "קמץ קטן");
            Add('\u05bb', "קובוץ");
            Add('\u05bc', "דגש או מפיק בה״א");
            Add('\u05c1', "נקודת שי״ן ימנית");
            Add('\u05c2', "נקודת שי״ן שמאלית");
            Add('\u05bd', "מתג");
            Add('\u05bf', "רפה");
            Add(Clear, "הסר ניקוד מהאות");
            editor.KeyUp += (sender, args) => RefreshButtons();
            editor.MouseUp += (sender, args) => RefreshButtons();
            editor.TextChanged += (sender, args) => RefreshButtons();
            editor.GotFocus += (sender, args) => RefreshButtons();
            VisibleChanged += (sender, args) => RefreshButtons();
        }

        private void Add(char mark, string name)
        {
            string baseLetter = DotIconBase(mark);
            var button = new Button
            {
                Text = baseLetter != null ? "" : mark == Clear ? "×" : "\u25cc" + mark,
                AccessibleName = name, Font = markFont, Size = new Size(33, 29),
                Margin = new Padding(2), Enabled = false, RightToLeft = RightToLeft.No,
            };
            if (baseLetter != null)
                button.Paint += (sender, args) => DrawDotIcon(button, args.Graphics, baseLetter, mark);
            tips.SetToolTip(button, name);
            button.Click += (sender, args) =>
            {
                int start, end;
                string text = editor.Text;
                if (!TryLetter(text, editor.SelectionStart, editor.SelectionLength, out start, out end)
                    || !CanApply(text[start], mark)) return;
                string pointed = ApplyMark(text.Substring(start, end - start), mark);
                editor.Select(start, end - start);
                editor.SelectedText = pointed;
                editor.Focus();
                editor.Select(start + pointed.Length, 0);
                RefreshButtons();
            };
            buttons.Add(mark, button);
            Controls.Add(button);
        }

        private static string DotIconBase(char mark)
        {
            if (mark == HolamHaser) return "ב";
            if (mark == HolamMale || mark == Shuruq) return "ו";
            if (mark == '\u05c1' || mark == '\u05c2') return "ש";
            return null;
        }

        private static void DrawDotIcon(Button button, Graphics graphics, string letter, char mark)
        {
            // Draw the dot independently so the preview does not depend on Hebrew
            // combining-mark placement or clipping in the Windows button renderer.
            int height = button.ClientSize.Height;
            int width = button.ClientSize.Width;
            int dotSize = Math.Max(3, (int)Math.Round(height * .12));
            double x = mark == HolamMale ? .50 : mark == '\u05c1' ? .69 : .31;
            int dotY = mark == Shuruq ? height * 3 / 5 : height / 5;
            Color color = button.Enabled ? SystemColors.ControlText : SystemColors.GrayText;
            TextRenderer.DrawText(graphics, letter, button.Font, new Rectangle(0, dotSize, width, height - dotSize),
                color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            using (var brush = new SolidBrush(color))
                graphics.FillEllipse(brush, (int)Math.Round(width * x) - dotSize / 2, dotY, dotSize, dotSize);
        }

        private void RefreshButtons()
        {
            int start, end;
            string text = editor.Text;
            bool found = TryLetter(text, editor.SelectionStart, editor.SelectionLength, out start, out end);
            foreach (var entry in buttons)
                entry.Value.Enabled = found && CanApply(text[start], entry.Key);
        }

        internal static bool TryLetter(string text, int caret, int selectionLength, out int start, out int end)
        {
            start = caret - 1;
            end = caret;
            if (selectionLength != 0) return false;
            while (start >= 0 && IsHebrewMark(text[start])) start--;
            if (start < 0 || text[start] < 'א' || text[start] > 'ת') return false;
            while (end < text.Length && IsHebrewMark(text[end])) end++;
            return true;
        }

        private static bool IsHebrewMark(char mark)
        {
            return (mark >= '\u0591' && mark <= '\u05af') || Group(mark) != 0 ||
                mark == '\u05c4' || mark == '\u05c5';
        }

        internal static bool CanApply(char letter, char mark)
        {
            if (mark == '\u05c1' || mark == '\u05c2') return letter == 'ש';
            if (mark == HolamMale || mark == Shuruq) return letter == 'ו';
            if (mark == '\u05bc') return "אחער".IndexOf(letter) < 0;
            return true;
        }

        private static int Group(char mark)
        {
            if ((mark >= '\u05b0' && mark <= '\u05bb') || mark == '\u05c7') return 1;
            if (mark == '\u05c1' || mark == '\u05c2') return 2;
            if (mark == '\u05bc' || mark == '\u05bf') return 3;
            return mark == '\u05bd' ? 4 : 0;
        }

        internal static string ApplyMark(string letter, char mark)
        {
            if (mark == Shuruq)
                return (letter[0] + new string(letter.Skip(1)
                    .Where(existing => Group(existing) != 1 && Group(existing) != 3).ToArray()) + "\u05bc").Normalize();
            bool replacesShuruq = letter[0] == 'ו' && (mark == HolamHaser || mark == HolamMale);
            // The special haser code point is only used on vav. Elsewhere ordinary
            // holam already renders above-left.
            if (mark == HolamHaser && letter[0] != 'ו') mark = '\u05b9';
            return (letter[0] + new string(letter.Skip(1)
                .Where(existing => !(replacesShuruq && existing == '\u05bc') &&
                    (Group(existing) == 0 || (mark != Clear && Group(existing) != Group(mark))))
                .ToArray()) + (mark == Clear ? "" : mark.ToString())).Normalize();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tips.Dispose(); markFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
