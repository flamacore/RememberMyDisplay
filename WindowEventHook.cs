using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace RememberMyDisplay
{
    public static class WindowEventHook
    {
        // Window event constants
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // Delegates for events
        public delegate void WindowEventHandler(IntPtr hwnd, string title, string processName);
        public static event WindowEventHandler WindowCreated;
        public static event WindowEventHandler WindowMoved;
        public static event WindowEventHandler WindowDestroyed;

        // Store hook handles for cleanup
        private static List<IntPtr> _hookHandles = new List<IntPtr>();
        private static WinEventDelegate _winEventProc;

        // Define the callback delegate
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static void Initialize()
        {
            // Store delegate to prevent garbage collection
            _winEventProc = new WinEventDelegate(WinEventCallback);
            
            // Set hooks for each event we want to monitor
            _hookHandles.Add(SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_CREATE, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT));
            _hookHandles.Add(SetWinEventHook(EVENT_SYSTEM_MOVESIZEEND, EVENT_SYSTEM_MOVESIZEEND, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT));
            _hookHandles.Add(SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS));
            _hookHandles.Add(SetWinEventHook(EVENT_OBJECT_DESTROY, EVENT_OBJECT_DESTROY, IntPtr.Zero, _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT));
        }

        public static void Cleanup()
        {
            // Remove all hooks
            foreach (var handle in _hookHandles)
            {
                if (handle != IntPtr.Zero)
                {
                    UnhookWinEvent(handle);
                }
            }
            _hookHandles.Clear();
        }        private static void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Only track top-level windows (idObject == OBJID_WINDOW)
            if (hwnd == IntPtr.Zero || idObject != 0)
                return;

            try
            {
                // Get window title
                StringBuilder title = new StringBuilder(256);
                GetWindowText(hwnd, title, title.Capacity);
                
                // Skip windows with empty titles
                if (title.Length == 0)
                    return;

                // Get process information
                uint processId = 0;
                GetWindowThreadProcessId(hwnd, out processId);

                // Skip if we couldn't get a process ID
                if (processId == 0)
                    return;

                string processName = "";
                try
                {
                    using (Process process = Process.GetProcessById((int)processId))
                    {
                        processName = process.ProcessName;
                    }
                }
                catch 
                { 
                    return; // Skip if we can't get process name
                }

                // Skip if process name is empty
                if (string.IsNullOrEmpty(processName))
                    return;

                // Throttle location change events (they fire very frequently)
                if (eventType == EVENT_OBJECT_LOCATIONCHANGE)
                {
                    // Skip excessive location change events by implementing a simple throttling mechanism
                    // We'll only process location change events every 500ms for the same window
                    if (!ShouldHandleLocationChange(hwnd))
                        return;
                }
                
                switch (eventType)
                {
                    case EVENT_OBJECT_CREATE:
                        if (WindowCreated != null)
                        {
                            try {
                                WindowCreated(hwnd, title.ToString(), processName);
                            } 
                            catch (Exception ex) {
                                Debug.WriteLine($"Error in WindowCreated event: {ex.Message}");
                            }
                        }
                        break;
                    case EVENT_SYSTEM_MOVESIZEEND:
                    case EVENT_OBJECT_LOCATIONCHANGE:
                        if (WindowMoved != null)
                        {
                            try {
                                WindowMoved(hwnd, title.ToString(), processName);
                            }
                            catch (Exception ex) {
                                Debug.WriteLine($"Error in WindowMoved event: {ex.Message}");
                            }
                        }
                        break;
                    case EVENT_OBJECT_DESTROY:
                        if (WindowDestroyed != null)
                        {
                            try {
                                WindowDestroyed(hwnd, title.ToString(), processName);
                            }
                            catch (Exception ex) {
                                Debug.WriteLine($"Error in WindowDestroyed event: {ex.Message}");
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // Just log error and continue - we don't want to crash the hook handler
                Debug.WriteLine($"Error in window event hook: {ex.Message}");
            }
        }
        
        // Dictionary to keep track of when we last processed a location change for a specific window
        private static Dictionary<IntPtr, DateTime> lastLocationChangeEvents = new Dictionary<IntPtr, DateTime>();
        
        // Simple throttling mechanism for location change events
        private static bool ShouldHandleLocationChange(IntPtr hwnd)
        {
            DateTime now = DateTime.Now;
            
            if (!lastLocationChangeEvents.TryGetValue(hwnd, out DateTime lastTime) || 
                (now - lastTime).TotalMilliseconds > 500)  // 500ms throttling
            {
                lastLocationChangeEvents[hwnd] = now;
                return true;
            }
            
            return false;
        }
    }
}
