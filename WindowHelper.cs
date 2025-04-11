using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace RememberMyDisplay
{
    public static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, ref RECT rectangle);

        [DllImport("user32.dll")]
        private static extern bool MoveWindow(IntPtr handle, int x, int y, int width, int height, bool redraw);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd); // Is minimized

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd); // Is maximized

        private const int MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        public static bool GetWindowInfo(IntPtr hwnd, out WindowInfo info)
        {
            info = null;
            
            if (!IsWindowVisible(hwnd) || IsIconic(hwnd) || IsZoomed(hwnd))
                return false;

            try
            {
                RECT rect = new RECT();
                if (GetWindowRect(hwnd, ref rect))
                {
                    // Get display device name
                    IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                    MONITORINFOEX monitorInfo = new MONITORINFOEX();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    GetMonitorInfo(hMonitor, ref monitorInfo);

                    info = new WindowInfo
                    {
                        X = rect.Left,
                        Y = rect.Top,
                        Width = rect.Right - rect.Left,
                        Height = rect.Bottom - rect.Top,
                        DisplayDeviceName = monitorInfo.szDevice,
                        LastUpdated = DateTime.Now
                    };
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting window info: {ex.Message}");
            }

            return false;
        }

        public static bool SetWindowPosition(IntPtr hwnd, int x, int y, int width, int height, string displayName)
        {
            try
            {
                // First check if the specified monitor exists
                IntPtr targetMonitor = GetMonitorByName(displayName);
                
                if (targetMonitor != IntPtr.Zero)
                {
                    // Adjust coordinates for the target monitor
                    MONITORINFOEX monitorInfo = new MONITORINFOEX();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                    GetMonitorInfo(targetMonitor, ref monitorInfo);

                    // Move the window to the correct display
                    return MoveWindow(hwnd, x, y, width, height, true);
                }
                else
                {
                    // If the target monitor is not found, use the original position
                    return MoveWindow(hwnd, x, y, width, height, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting window position: {ex.Message}");
                return false;
            }
        }

        private static IntPtr GetMonitorByName(string displayName)
        {
            IntPtr result = IntPtr.Zero;

            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                    (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
                    {
                        MONITORINFOEX monitorInfo = new MONITORINFOEX();
                        monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));
                        GetMonitorInfo(hMonitor, ref monitorInfo);

                        if (monitorInfo.szDevice == displayName)
                        {
                            result = hMonitor;
                            return false; // Stop enumeration
                        }

                        return true; // Continue enumeration
                    }, IntPtr.Zero);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enumerating monitors: {ex.Message}");
            }

            return result;
        }
    }
}
