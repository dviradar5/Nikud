using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nikud
{
    internal sealed class Hotkey : NativeWindow, IDisposable
    {
        private readonly Action callback;
        private int id;
        internal uint Modifiers { get; private set; }
        internal Keys Key { get; private set; }

        internal Hotkey(Action callback)
        {
            this.callback = callback;
            CreateHandle(new CreateParams());
        }

        internal bool Set(uint modifiers, Keys key)
        {
            if (Modifiers == modifiers && Key == key) return true;
            int next = id == 1 ? 2 : 1;
            if (key != Keys.None && !RegisterHotKey(Handle, next, modifiers | 0x4000, (uint)key)) return false;
            if (id != 0) UnregisterHotKey(Handle, id);
            id = key == Keys.None ? 0 : next;
            Modifiers = modifiers;
            Key = key;
            return true;
        }

        internal static string Describe(uint modifiers, Keys key)
        {
            if (key == Keys.None) return "Not set";
            var parts = new List<string>();
            if ((modifiers & 2) != 0) parts.Add("Ctrl");
            if ((modifiers & 1) != 0) parts.Add("Alt");
            if ((modifiers & 4) != 0) parts.Add("Shift");
            if ((modifiers & 8) != 0) parts.Add("Win");
            parts.Add(new KeysConverter().ConvertToString(key));
            return string.Join("+", parts);
        }

        internal static bool Down(Keys key) { return GetAsyncKeyState((int)key) < 0; }

        internal bool KeysHeld()
        {
            return Down(Key) || ModifierHeld();
        }

        internal static bool ModifierHeld()
        {
            return Down(Keys.ControlKey) || Down(Keys.ShiftKey) || Down(Keys.Menu) || Down(Keys.LWin) || Down(Keys.RWin);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312 && id != 0 && message.WParam.ToInt32() == id) callback();
            base.WndProc(ref message);
        }

        public void Dispose()
        {
            if (id != 0) UnregisterHotKey(Handle, id);
            DestroyHandle();
        }

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    }

    internal sealed class SelectionTarget
    {
        internal readonly IntPtr Window;
        private readonly IntPtr focus;

        private SelectionTarget(IntPtr window, IntPtr focus)
        {
            Window = window;
            this.focus = focus;
        }

        internal static SelectionTarget Current()
        {
            var window = GetForegroundWindow();
            var info = new GuiThreadInfo { Size = Marshal.SizeOf(typeof(GuiThreadInfo)) };
            return new SelectionTarget(window, GetGUIThreadInfo(0, ref info) ? info.Focus : IntPtr.Zero);
        }

        internal bool IsCurrent
        {
            get
            {
                var current = Current();
                return current.Window == Window && current.focus == focus;
            }
        }

        internal async Task<bool> ReturnFocusAsync()
        {
            if (!IsCurrent && GetForegroundWindow() != Window) SetForegroundWindow(Window);
            for (int i = 0; i < 12 && !IsCurrent; i++) await Task.Delay(25);
            return IsCurrent;
        }

        internal async Task<string> ReadAsync()
        {
            if (!IsCurrent || Hotkey.ModifierHeld()) return null;
            var clipboard = new ClipboardSession();
            try { return await CopyAsync(clipboard); }
            finally { clipboard.Restore(); }
        }

        internal async Task<bool> ReplaceAsync(string expected, string replacement)
        {
            if (!IsCurrent || Hotkey.ModifierHeld()) return false;
            var clipboard = new ClipboardSession();
            try
            {
                if (await CopyAsync(clipboard) != expected || !IsCurrent || Hotkey.ModifierHeld()) return false;
                if (!clipboard.SetPasteText(replacement) || !IsCurrent || !SendShortcut(Keys.V)) return false;
                // Let the receiving app process Ctrl+V before restoring the clipboard.
                await Task.Delay(450);
                return true;
            }
            finally { clipboard.Restore(); }
        }

        private async Task<string> CopyAsync(ClipboardSession clipboard)
        {
            if (!IsCurrent || !SendShortcut(Keys.C)) return null;
            for (int i = 0; i < 55; i++)
            {
                await Task.Delay(30);
                if (!IsCurrent) return null;
                if (clipboard.TrackCopy())
                {
                    try
                    {
                        var text = Clipboard.GetText(TextDataFormat.UnicodeText);
                        if (clipboard.ConfirmRead()) return text;
                    }
                    catch (ExternalException) { }
                }
            }
            return null;
        }

        private static bool SendShortcut(Keys key)
        {
            var inputs = new[]
            {
                KeyInput(Keys.ControlKey, false), KeyInput(key, false),
                KeyInput(key, true), KeyInput(Keys.ControlKey, true),
            };
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Input))) == inputs.Length;
        }

        private static Input KeyInput(Keys key, bool release)
        {
            return new Input { Type = 1, Data = new InputData
                { Keyboard = new KeyboardInput { VirtualKey = (ushort)key, Flags = release ? 2u : 0u } } };
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GuiThreadInfo
        {
            internal int Size, Flags;
            internal IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret;
            internal int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input { internal uint Type; internal InputData Data; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputData
        {
            [FieldOffset(0)] internal KeyboardInput Keyboard;
            [FieldOffset(0)] internal MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            internal ushort VirtualKey, Scan;
            internal uint Flags, Time;
            internal UIntPtr Extra;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            internal int X, Y;
            internal uint MouseData, Flags, Time;
            internal UIntPtr Extra;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
        [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] input, int size);
    }

    internal sealed class ClipboardSession
    {
        private readonly DataObject previous = new DataObject();
        private readonly uint initial;
        private uint? changed;
        private IntPtr owner;

        internal ClipboardSession()
        {
            initial = GetClipboardSequenceNumber();
            var current = Clipboard.GetDataObject();
            if (current != null)
            {
                foreach (var format in new[] { DataFormats.UnicodeText, DataFormats.Text, DataFormats.Rtf, DataFormats.Html, DataFormats.CommaSeparatedValue, DataFormats.FileDrop })
                {
                    if (!current.GetDataPresent(format, false)) continue;
                    var data = current.GetData(format, false);
                    if (data is string || data is string[]) previous.SetData(format, false, data);
                }
                if (Clipboard.ContainsImage())
                {
                    using (var image = Clipboard.GetImage())
                        if (image != null) previous.SetImage(new Bitmap(image));
                }
            }
            if (initial != GetClipboardSequenceNumber())
                throw new InvalidOperationException("The clipboard changed. Try the shortcut again.");
        }

        internal bool TrackCopy()
        {
            var current = GetClipboardSequenceNumber();
            if (current == initial) return false;
            changed = current;
            owner = GetClipboardOwner();
            return true;
        }

        internal bool ConfirmRead()
        {
            var sequence = GetClipboardSequenceNumber();
            if (changed == sequence) return true;
            // Office can advertise a clipboard format before it renders the requested text.
            if (owner == IntPtr.Zero || owner != GetClipboardOwner()) return false;
            changed = sequence;
            return true;
        }

        internal bool SetPasteText(string text)
        {
            if (changed != GetClipboardSequenceNumber()) return false;
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            changed = GetClipboardSequenceNumber();
            return true;
        }

        internal void Restore()
        {
            if (changed != GetClipboardSequenceNumber()) return;
            if (previous.GetFormats(false).Length == 0) Clipboard.Clear();
            else Clipboard.SetDataObject(previous, true, 3, 30);
        }

        [DllImport("user32.dll")] private static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] private static extern IntPtr GetClipboardOwner();
    }
}
