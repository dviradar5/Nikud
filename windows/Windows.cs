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
            ClientSize = new Size(460, Math.Min(510, 175 + (ambiguous.Count + words.Count(word => word.Compound != null)) * 65));
            if (target != IntPtr.Zero)
            {
                var area = Screen.FromHandle(target).WorkingArea;
                Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                                     area.Top + Math.Max(0, (area.Height - Height) / 2));
            }
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
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
            preview.AccessibleName = "Proposed text";
            layout.Controls.Add(preview, 0, 2);
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
                edit.Text = preview.ReadOnly ? "Edit nikud…" : "Use suggestions";
                UpdateApply();
                if (!preview.ReadOnly) preview.Focus();
            };
            preview.TextChanged += (sender, args) =>
            {
                if (preview.ReadOnly) return;
                apply.Enabled = Engine.SameSpelling(source, preview.Text);
                instruction.Text = apply.Enabled ? "Edit the nikud, then select Apply." : "Keep the original letters, spacing, and punctuation.";
            };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(apply);
            buttons.Controls.Add(edit);
            layout.Controls.Add(buttons, 0, 3);
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
}
