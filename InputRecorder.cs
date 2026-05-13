using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace imgsaver
{
    public class InputRecorder : IDisposable
    {
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private LowLevelMouseProc _mouseProc;
        private LowLevelKeyboardProc _kbProc;

        private Stopwatch _stopwatch = new Stopwatch();
        private List<InputEvent> _events = new List<InputEvent>();
        private bool _isRecording = false;

        private int _sampleMouseEveryMs = 15;
        private System.Threading.Timer? _mouseSampleTimer;
        private int _lastMouseX;
        private int _lastMouseY;
        private static uint _currentProcessId = (uint)Process.GetCurrentProcess().Id;

        private static readonly IntPtr Magic = (IntPtr)0x42424242;

        public bool IsRecording => _isRecording;
        public event Action? OnStopRequested;

        public InputRecorder()
        {
            _mouseProc = MouseHookCallback;
            _kbProc = KeyboardHookCallback;
        }

        public void Start()
        {
            if (_isRecording) return;
            _events.Clear();
            _stopwatch.Restart();
            _mouseHook = SetHook(WH_MOUSE_LL, _mouseProc);
            _keyboardHook = SetHook(WH_KEYBOARD_LL, _kbProc);
            _lastMouseX = Cursor.Position.X;
            _lastMouseY = Cursor.Position.Y;
            _mouseSampleTimer = new System.Threading.Timer(_ => SampleMouse(), null, 0, _sampleMouseEveryMs);
            _isRecording = true;
        }

        public void Stop()
        {
            if (!_isRecording) return;
            _mouseSampleTimer?.Dispose();
            UnhookWindowsHookEx(_mouseHook);
            UnhookWindowsHookEx(_keyboardHook);
            _mouseHook = IntPtr.Zero;
            _keyboardHook = IntPtr.Zero;
            _stopwatch.Stop();
            _isRecording = false;
        }

        private void SampleMouse()
        {
            try
            {
                var p = Cursor.Position;
                if (Math.Abs(p.X - _lastMouseX) >= 1 || Math.Abs(p.Y - _lastMouseY) >= 1)
                {
                    AddEventWithRelativity(InputEventType.MouseMove, p.X, p.Y);
                    _lastMouseX = p.X; _lastMouseY = p.Y;
                }
            }
            catch { }
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                if (data.dwExtraInfo == Magic) return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

                int msg = wParam.ToInt32();
                switch (msg)
                {
                    case WM_LBUTTONDOWN: AddEventWithRelativity(InputEventType.MouseDown, data.pt.x, data.pt.y, "Left"); break;
                    case WM_LBUTTONUP: AddEventWithRelativity(InputEventType.MouseUp, data.pt.x, data.pt.y, "Left"); break;
                    case WM_RBUTTONDOWN: AddEventWithRelativity(InputEventType.MouseDown, data.pt.x, data.pt.y, "Right"); break;
                    case WM_RBUTTONUP: AddEventWithRelativity(InputEventType.MouseUp, data.pt.x, data.pt.y, "Right"); break;
                    case WM_MBUTTONDOWN: AddEventWithRelativity(InputEventType.MouseDown, data.pt.x, data.pt.y, "Middle"); break;
                    case WM_MBUTTONUP: AddEventWithRelativity(InputEventType.MouseUp, data.pt.x, data.pt.y, "Middle"); break;
                    case WM_MOUSEWHEEL:
                        int delta = (short)((data.mouseData >> 16) & 0xffff);
                        AddEventWithRelativity(InputEventType.MouseWheel, data.pt.x, data.pt.y, wheelDelta: delta);
                        break;
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void AddEventWithRelativity(InputEventType type, int x, int y, string button = "", int wheelDelta = 0)
        {
            var ev = new InputEvent { T = _stopwatch.ElapsedMilliseconds, Type = type, Button = button, WheelDelta = wheelDelta };

            IntPtr hWnd = WindowFromPoint(new POINTSTRUCT { x = x, y = y });
            if (hWnd != IntPtr.Zero)
            {
                uint windowPid;
                GetWindowThreadProcessId(hWnd, out windowPid);

                if (windowPid == _currentProcessId)
                {
                    // Always use the Root window for relativity to ensure consistency with the Player
                    IntPtr rootHwnd = GetAncestor(hWnd, GA_ROOT);
                    if (rootHwnd != IntPtr.Zero)
                    {
                        RECT rect = new RECT();
                        GetWindowRect(rootHwnd, ref rect);
                        ev.X = x - rect.Left;
                        ev.Y = y - rect.Top;
                        ev.IsRelative = true;
                        lock (_events) { _events.Add(ev); }
                        return;
                    }
                }
            }

            ev.X = x;
            ev.Y = y;
            ev.IsRelative = false;
            lock (_events) { _events.Add(ev); }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (kb.dwExtraInfo == Magic) return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);

                int msg = wParam.ToInt32();
                long t = _stopwatch.ElapsedMilliseconds;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (kb.vkCode == 0x1B) { OnStopRequested?.Invoke(); return CallNextHookEx(_keyboardHook, nCode, wParam, lParam); }
                    lock (_events) { _events.Add(new InputEvent { T = t, Type = InputEventType.KeyDown, KeyCode = (int)kb.vkCode }); }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (kb.vkCode != 0x1B)
                        lock (_events) { _events.Add(new InputEvent { T = t, Type = InputEventType.KeyUp, KeyCode = (int)kb.vkCode }); }
                }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        public IReadOnlyList<InputEvent> GetEvents() => _events.AsReadOnly();

        public async Task SaveAsync(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                using (var fs = File.Create(path)) await JsonSerializer.SerializeAsync(fs, _events, opts);
            }
            catch { }
        }

        public async Task<bool> LoadAsync(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    var loaded = await JsonSerializer.DeserializeAsync<List<InputEvent>>(fs);
                    if (loaded != null) { _events = loaded; return true; }
                }
            }
            catch { }
            return false;
        }

        public void Dispose() => Stop();

        #region Win32 Hook PInvoke

        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;
        private const uint GA_ROOT = 2;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTSTRUCT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINTSTRUCT pt; public int mouseData; public int flags; public int time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINTSTRUCT Point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        private IntPtr SetHook(int idHook, LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                return SetWindowsHookEx(idHook, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private IntPtr SetHook(int idHook, LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                return SetWindowsHookEx(idHook, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        #endregion
    }
}
