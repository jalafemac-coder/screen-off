using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ScreenOff
{
    // Display power control via the shell's monitor-power system command.
    internal static class Display
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam,
                                                        IntPtr lParam, uint flags, uint timeoutMs,
                                                        out UIntPtr result);

        private const int HWND_BROADCAST = 0xFFFF;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MONITOR_POWER_OFF = 2;

        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private const uint BROADCAST_TIMEOUT_MS = 2000;

        // settleMs lets a triggering key release or click finish first. An input
        // event arriving right after the broadcast wakes the display back up, so
        // a hotkey needs a pause here while a timer expiring does not.
        public static void PowerOff(int settleMs)
        {
            if (settleMs > 0)
                Thread.Sleep(settleMs);

            Broadcast(MONITOR_POWER_OFF);
        }

        private static void Broadcast(int state)
        {
            // A plain SendMessage to HWND_BROADCAST blocks forever on the first
            // unresponsive top-level window, which leaves the caller alive long
            // after the display changed state. The timeout variant skips those.
            UIntPtr result;
            SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND,
                               (IntPtr)SC_MONITORPOWER, (IntPtr)state,
                               SMTO_ABORTIFHUNG, BROADCAST_TIMEOUT_MS, out result);
        }
    }
}
