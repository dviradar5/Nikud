using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyInformationalVersion("0.2.14")]

namespace Nikud
{
    internal static class IssueReports
    {
        internal static void Open(bool wordMistake)
        {
            var template = wordMistake ? "word.yml" : "bug.yml";
            var url = "https://github.com/dviradar5/Nikud/issues/new?template=" + template
                + "&version=" + Uri.EscapeDataString(Application.ProductVersion);
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception)
            {
                MessageBox.Show("Could not open your browser. Visit github.com/dviradar5/Nikud/issues to file a report.", "Nikud");
            }
        }
    }

    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            bool first;
            using (var instance = new Mutex(true, @"Local\NikudTray", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Nikud is already running. Right-click its tray icon and choose Exit before opening a new version.", "Nikud");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                using (var tray = new Tray(Environment.GetCommandLineArgs().Contains("--tray"))) Application.Run(tray);
            }
        }
    }

    internal sealed class Tray : ApplicationContext
    {
        private readonly NotifyIcon icon;
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly Icon artwork = MakeIcon();
        private readonly Hotkey hotkey;
        private readonly Settings settings;
        private bool busy;
        private bool exiting;

        internal Tray(bool trayOnly)
        {
            hotkey = new Hotkey(VocalizeSelection);
            settings = new Settings(hotkey) { Icon = artwork };
            menu.Items.Add("Settings…", null, (sender, args) => OpenSettings());
            menu.Items.Add("Open text editor…", null, (sender, args) => OpenEditor());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Report a bug…", null, (sender, args) => IssueReports.Open(false));
            menu.Items.Add("Report a word mistake…", null, (sender, args) => IssueReports.Open(true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (sender, args) => { exiting = true; ExitThread(); });
            icon = new NotifyIcon { Icon = artwork, Text = "Nikud", ContextMenuStrip = menu, Visible = true };
            icon.DoubleClick += (sender, args) => OpenSettings();
            settings.OpenEditor += OpenEditor;
            if (!trayOnly) OpenSettings();
            Notify(settings.ShortcutStatus);
        }

        private void OpenEditor()
        {
            using (var dialog = new TextEditor(artwork)) dialog.ShowDialog(settings.Visible ? settings : null);
        }

        private void OpenSettings()
        {
            settings.Show();
            settings.WindowState = FormWindowState.Normal;
            settings.Activate();
        }

        private void Notify(string text)
        {
            if (exiting) return;
            settings.ShowActivity(text);
            icon.ShowBalloonTip(3500, "Nikud", text, ToolTipIcon.Info);
        }

        private async void VocalizeSelection()
        {
            if (busy || exiting || settings.CapturingShortcut && settings.ContainsFocus) return;
            busy = true;
            try
            {
                var target = SelectionTarget.Current();
                if (target.Window == IntPtr.Zero || target.Window == settings.Handle) return;
                for (int attempt = 0; attempt < 60 && hotkey.KeysHeld(); attempt++) await Task.Delay(25);
                if (exiting || hotkey.KeysHeld() || !target.IsCurrent) return;
                string source = await target.ReadAsync();
                if (string.IsNullOrWhiteSpace(source))
                {
                    Notify("Select editable Hebrew text in the other app and try again.");
                    return;
                }
                if (source.Length > 1500)
                {
                    Notify("Select up to 1,500 characters.");
                    return;
                }

                var words = await Engine.VocalizeAsync(source);
                if (exiting) return;
                if (!target.IsCurrent)
                {
                    Notify("The active app changed. Select the text and try again.");
                    return;
                }
                string result;
                if ((settings.ReviewBeforeApplying && !Engine.IsRemoval(source)) || words.Any(word => word.Readings.Count > 1 || word.Compound != null))
                {
                    using (var dialog = new ReadingsDialog(source, words, artwork, target.Window))
                    {
                        if (dialog.ShowDialog() != DialogResult.OK) return;
                        result = dialog.ResultText;
                    }
                    if (exiting) return;
                    if (!await target.ReturnFocusAsync())
                    {
                        Notify("Could not return to the original selection. Try the shortcut again.");
                        return;
                    }
                }
                else result = Engine.Render(source, words);
                if (source == result)
                {
                    Notify("The selection already matches the result.");
                    return;
                }
                if (!await target.ReplaceAsync(source, result))
                    Notify("Could not replace the original selection. Select editable text and try again.");
                else settings.ShowActivity("Updated the selection in the original app.");
            }
            catch (Exception ex) { Notify(ex.Message); }
            finally { busy = false; }
        }

        private static Icon MakeIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphic = Graphics.FromImage(bitmap))
            using (var fill = new SolidBrush(Color.FromArgb(151, 51, 97)))
            using (var font = new Font("Segoe UI", 22, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphic.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphic.FillEllipse(fill, 1, 1, 30, 30);
                graphic.DrawString("נִ", font, Brushes.White, new RectangleF(0, -2, 32, 32), format);
                var handle = bitmap.GetHicon();
                try
                {
                    using (var temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exiting = true;
                icon.Visible = false;
                icon.Dispose();
                menu.Dispose();
                hotkey.Dispose();
                settings.Dispose();
                artwork.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
