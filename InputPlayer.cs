using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;

namespace imgsaver
{
    public class InputPlayer
    {
        private List<InputEvent> _events = new List<InputEvent>();
        private CancellationTokenSource? _cts;
        private double _speed = 1.0;
        public bool IsPlaying { get; private set; } = false;

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private LowLevelMouseProc _mouseProc;
        private LowLevelKeyboardProc _kbProc;
        private DateTime _playStartTime = DateTime.MinValue;

        private static readonly IntPtr Magic = (IntPtr)0x42424242;

        public InputPlayer()
        {
            _mouseProc = MouseHookCallback;
            _kbProc = KeyboardHookCallback;
        }

        public void SetEvents(IEnumerable<InputEvent> events) { _events = new List<InputEvent>(events); }
        public void SetSpeed(double speed) { _speed = Math.Max(0.1, speed); }

        public async Task PlayAsync(bool loop = false)
        {
            if (_events == null || _events.Count == 0) return;
            _cts = new CancellationTokenSource();
            IsPlaying = true;
            _playStartTime = DateTime.Now;
            SetHooks();
            try
            {
                do
                {
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < _events.Count; i++)
                    {
                        if (_cts.Token.IsCancellationRequested) break;
                        var ev = _events[i];
                        long target = (long)(ev.T / _speed);
                        long delay = target - sw.ElapsedMilliseconds;
                        if (delay > 0) await Task.Delay((int)delay, _cts.Token);
                        DispatchEvent(ev);
                    }
                }
                while (loop && !_cts.Token.IsCancellationRequested);
            }
            catch (TaskCanceledException) { }
            finally { IsPlaying = false; UnsetHooks(); }
        }

        public void Stop() { _cts?.Cancel(); UnsetHooks(); }

        private void DispatchEvent(InputEvent ev)
        {
            int finalX = ev.X;
            int finalY = ev.Y;

            if (ev.IsRelative)
            {
                IntPtr targetHwnd = GetProcessWindow();
                if (targetHwnd != IntPtr.Zero)
                {
                    RECT rect = new RECT();
                    if (GetWindowRect(targetHwnd, ref rect))
                    {
                        finalX = rect.Left + ev.X;
                        finalY = rect.Top + ev.Y;
                    }
                }
            }

            try
            {
                switch (ev.Type)
                {
                    case InputEventType.MouseMove: SendMouseMove(finalX, finalY); break;
                    case InputEventType.MouseDown:
                    case InputEventType.MouseUp: SendMouseClick(ev, finalX, finalY); break;
                    case InputEventType.MouseWheel: SendMouseWheel(ev.WheelDelta, finalX, finalY); break;
                    case InputEventType.KeyDown: SendKey(ev.KeyCode, true); break;
                    case InputEventType.KeyUp: SendKey(ev.KeyCode, false); break;
                }
            }
            catch { }
        }

        private IntPtr GetProcessWindow()
        {
            IntPtr found = IntPtr.Zero;
            uint pid = (uint)Process.GetCurrentProcess().Id;
            EnumWindows((hWnd, lParam) => {
                uint windowPid;
                GetWindowThreadProcessId(hWnd, out windowPid);
                if (windowPid == pid && IsWindowVisible(hWnd))
                {
                    if (GetAncestor(hWnd, 2) == hWnd)
                    {
                        found = hWnd;
                        if (!IsRecorderWindow(hWnd)) return false;
                    }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private bool IsRecorderWindow(IntPtr hWnd)
        {
            const int nChars = 256;
            System.Text.StringBuilder buff = new System.Text.StringBuilder(nChars);
            if (GetWindowText(hWnd, buff, nChars) > 0)
            {
                return buff.ToString().Contains("Input Recorder");
            }
            return false;
        }

        private void SendMouseMove(int x, int y)
        {
            int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            int screenLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int screenTop = GetSystemMetrics(SM_YVIRTUALSCREEN);

            INPUT input = new INPUT { type = INPUT_MOUSE };
            input.u.mi = new MOUSEINPUT
            {
                dx = ((x - screenLeft) * 65536) / screenWidth,
                dy = ((y - screenTop) * 65536) / screenHeight,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                dwExtraInfo = Magic
            };
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SendMouseClick(InputEvent ev, int x, int y)
        {
            uint flags = 0;
            if (ev.Button == "Left") flags = (ev.Type == InputEventType.MouseDown) ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else if (ev.Button == "Right") flags = (ev.Type == InputEventType.MouseDown) ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            else flags = (ev.Type == InputEventType.MouseDown) ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;

            SendMouseMove(x, y);
            INPUT input = new INPUT { type = INPUT_MOUSE };
            input.u.mi = new MOUSEINPUT { dwFlags = flags, dwExtraInfo = Magic };
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SendMouseWheel(int delta, int x, int y)
        {
            SendMouseMove(x, y);
            INPUT input = new INPUT { type = INPUT_MOUSE };
            input.u.mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_WHEEL, mouseData = (uint)delta, dwExtraInfo = Magic };
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        private void SendKey(int keyCode, bool down)
        {
            INPUT input = new INPUT { type = INPUT_KEYBOARD };
            input.u.ki = new KEYBDINPUT { wVk = (ushort)keyCode, dwFlags = down ? 0u : KEYEVENTF_KEYUP, dwExtraInfo = Magic };
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        #region Intervention Hooks
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Only stop on actual clicks (Down events), not moves or releases
                if (wParam == (IntPtr)0x0201 || wParam == (IntPtr)0x0204 || wParam == (IntPtr)0x0207)
                {
                    var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    if (data.dwExtraInfo != Magic)
                    {
                        // Ignore intervention for first 300ms to allow trigger key/mouse release
                        if ((DateTime.Now - _playStartTime).TotalMilliseconds > 300) Stop();
                    }
                }
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Only stop on KeyDown or SysKeyDown
                if (wParam == (IntPtr)0x0100 || wParam == (IntPtr)0x0104)
                {
                    var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    if (data.dwExtraInfo != Magic)
                    {
                        // Ignore intervention for first 300ms to allow trigger key release (Ctrl+E)
                        if ((DateTime.Now - _playStartTime).TotalMilliseconds > 300) Stop();
                    }
                }
            }
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private void SetHooks()
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr hMod = GetModuleHandle(curModule.ModuleName);
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, hMod, 0);
            }
        }

        private void UnsetHooks()
        {
            if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
            if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        }
        #endregion

        #region PInvoke
        private const int WH_MOUSE_LL = 14;
        private const int WH_KEYBOARD_LL = 13;
        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion u; }
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        #endregion
    }
}
