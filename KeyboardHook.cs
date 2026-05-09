using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace WinNumberGuide
{
    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private IntPtr _hookID = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;

        private bool _winKeyPressed = false;
        private bool _otherKeyPressed = false;
        private Stopwatch _stopwatch = new Stopwatch();
        private System.Threading.Timer _timer;

        public event EventHandler WinKeyLongPressed;
        public event EventHandler WinKeyReleased;

        public KeyboardHook()
        {
            _proc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            }

            _timer = new System.Threading.Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                    {
                        if (!_winKeyPressed)
                        {
                            _winKeyPressed = true;
                            _otherKeyPressed = false;
                            _stopwatch.Restart();
                            // Start timer to check for long press
                            _timer.Change(900, Timeout.Infinite);
                        }
                    }
                    else
                    {
                        if (_winKeyPressed)
                        {
                            _otherKeyPressed = true;
                            _timer.Change(Timeout.Infinite, Timeout.Infinite); // Cancel timer
                            System.Windows.Application.Current.Dispatcher.Invoke(() => WinKeyReleased?.Invoke(this, EventArgs.Empty));
                        }
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
                {
                    if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                    {
                        _winKeyPressed = false;
                        _timer.Change(Timeout.Infinite, Timeout.Infinite);
                        System.Windows.Application.Current.Dispatcher.Invoke(() => WinKeyReleased?.Invoke(this, EventArgs.Empty));
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void TimerCallback(object state)
        {
            if (_winKeyPressed && !_otherKeyPressed && _stopwatch.ElapsedMilliseconds >= 900)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => WinKeyLongPressed?.Invoke(this, EventArgs.Empty));
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            UnhookWindowsHookEx(_hookID);
        }
    }
}
