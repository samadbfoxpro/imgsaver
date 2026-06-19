using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace imgsaver
{
    public static class InputSimulator
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        public static readonly IntPtr ExtraInfoMagic = (IntPtr)0x42424242;

        public static void SimulateTextEntry(string text)
        {
            foreach (char c in text)
            {
                SendChar(c);
            }
        }

        private static void SendChar(char c)
        {
            INPUT[] inputs = new INPUT[2];

            // Key Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = 0;
            inputs[0].u.ki.wScan = c;
            inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
            inputs[0].u.ki.dwExtraInfo = IntPtr.Zero; // From Remote Server: Let the hook see it

            // Key Up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = 0;
            inputs[1].u.ki.wScan = c;
            inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
            inputs[1].u.ki.dwExtraInfo = IntPtr.Zero;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateBackspace(int count)
        {
            for (int i = 0; i < count; i++)
            {
                INPUT[] inputs = new INPUT[2];
                ushort vkBackspace = 0x08; // VK_BACK

                // Key Down
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = vkBackspace;
                inputs[0].u.ki.dwFlags = 0;
                inputs[0].u.ki.dwExtraInfo = ExtraInfoMagic; // Internal: Hook should ignore

                // Key Up
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = vkBackspace;
                inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
                inputs[1].u.ki.dwExtraInfo = ExtraInfoMagic;

                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
                Thread.Sleep(10); // Small delay for safety
            }
        }
        
        public static void SimulateEnter()
        {
            SimulateKeyPress(0x0D); // VK_RETURN
        }

        public static void SimulateUndo()
        {
            SimulateShortcut(0x11, 0x5A); // Ctrl + Z
        }

        public static void SimulateRedo()
        {
            SimulateShortcut(0x11, 0x59); // Ctrl + Y
        }

        public static void SimulateCopy()
        {
            SimulateShortcut(0x11, 0x43); // Ctrl + C
        }

        public static void SimulateCut()
        {
            SimulateShortcut(0x11, 0x58); // Ctrl + X
        }

        public static void SimulatePaste()
        {
            SimulateShortcut(0x11, 0x56); // Ctrl + V
        }

        public static void SimulateSelectAll()
        {
            SimulateShortcut(0x11, 0x41); // Ctrl + A
        }

        public static void SimulateCtrlE()
        {
            SimulateShortcut(0x11, 0x45); // Ctrl + E
        }

        public static void SimulateTab()
        {
            SimulateKeyPress(0x09); // VK_TAB
        }

        public static void SimulateWinKey()
        {
            SimulateKeyPress(0x5B); // VK_LWIN
        }

        public static void SimulateCtrlKey()
        {
            SimulateKeyPress(0x11); // VK_CONTROL
        }

        private static void SimulateShortcut(ushort modifier, ushort key)
        {
            INPUT[] inputs = new INPUT[4];

            // Modifier Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = modifier;
            inputs[0].u.ki.dwFlags = 0;
            inputs[0].u.ki.dwExtraInfo = ExtraInfoMagic;

            // Key Down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = key;
            inputs[1].u.ki.dwFlags = 0;
            inputs[1].u.ki.dwExtraInfo = ExtraInfoMagic;

            // Key Up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = key;
            inputs[2].u.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[2].u.ki.dwExtraInfo = ExtraInfoMagic;

            // Modifier Up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = modifier;
            inputs[3].u.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[3].u.ki.dwExtraInfo = ExtraInfoMagic;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateKeyPress(ushort vk)
        {
            INPUT[] inputs = new INPUT[2];

            // Key Down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = vk;
            inputs[0].u.ki.dwFlags = 0;
            inputs[0].u.ki.dwExtraInfo = ExtraInfoMagic;

            // Key Up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vk;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].u.ki.dwExtraInfo = ExtraInfoMagic;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}
