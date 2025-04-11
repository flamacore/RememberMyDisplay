using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace RememberMyDisplay
{
    public class WindowTrackerContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private Timer saveTimer;
        private const int SaveIntervalMs = 60000; // Save every minute
        private Dictionary<string, WindowInfo> windowPositions = new Dictionary<string, WindowInfo>();
        private readonly string dataFilePath;
        private bool isListening = true;

        public WindowTrackerContext()
        {
            dataFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RememberMyDisplay",
                "window_positions.json");

            // Create directory if it doesn't exist            Directory.CreateDirectory(Path.GetDirectoryName(dataFilePath));
              // Initialize tray icon with the application icon
            trayIcon = new NotifyIcon()
            {
                Icon = new Icon("AppIcon.ico"),
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "Remember My Display"
            };

            trayIcon.ContextMenuStrip.Items.Add("Enable", null, OnToggleEnable);
            trayIcon.ContextMenuStrip.Items.Add("Save now", null, OnSaveNow);
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);
            
            // Set initial checkmark
            ((ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[0]).Checked = true;

            // Load saved window positions
            LoadPositions();

            // Set up timer for periodic saving
            saveTimer = new Timer(SaveIntervalMs);
            saveTimer.Elapsed += (s, e) => SavePositions();
            saveTimer.Start();

            // Begin tracking windows
            StartWindowTracking();
        }

        private void OnToggleEnable(object sender, EventArgs e)
        {
            isListening = !isListening;
            var menuItem = (ToolStripMenuItem)trayIcon.ContextMenuStrip.Items[0];
            menuItem.Checked = isListening;
            menuItem.Text = isListening ? "Disable" : "Enable";
            
            if (isListening)
                trayIcon.ShowBalloonTip(3000, "Remember My Display", "Window position tracking enabled", ToolTipIcon.Info);
            else
                trayIcon.ShowBalloonTip(3000, "Remember My Display", "Window position tracking disabled", ToolTipIcon.Info);
        }

        private void OnSaveNow(object sender, EventArgs e)
        {
            SavePositions();
            trayIcon.ShowBalloonTip(2000, "Remember My Display", "Window positions saved", ToolTipIcon.Info);
        }        private void OnExit(object sender, EventArgs e)
        {
            try
            {
                // Save positions before exiting
                SavePositions();
                
                // Clean up window hooks
                WindowEventHook.Cleanup();
                
                // Stop timer
                saveTimer.Stop();
                saveTimer.Dispose();
                
                // Hide tray icon
                trayIcon.Visible = false;
                trayIcon.Dispose();
                
                // Exit application
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error while exiting: {ex.Message}", "Remember My Display", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        private void StartWindowTracking()
        {
            // Set up window message hook for window open/move/close events
            WindowEventHook.WindowCreated += OnWindowCreated;
            WindowEventHook.WindowMoved += OnWindowMoved;
            WindowEventHook.WindowDestroyed += OnWindowDestroyed;
            WindowEventHook.Initialize();
        }

        private void OnWindowCreated(IntPtr hwnd, string title, string processName)
        {
            if (!isListening || string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(title))
                return;

            string key = GetWindowKey(processName, title);
            
            if (windowPositions.TryGetValue(key, out WindowInfo info))
            {
                // Restore window position
                WindowHelper.SetWindowPosition(hwnd, info.X, info.Y, info.Width, info.Height, info.DisplayDeviceName);
            }
        }

        private void OnWindowMoved(IntPtr hwnd, string title, string processName)
        {
            if (!isListening || string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(title))
                return;

            // Get current window position and store it
            if (WindowHelper.GetWindowInfo(hwnd, out WindowInfo info))
            {
                info.ProcessName = processName;
                info.Title = title;
                
                string key = GetWindowKey(processName, title);
                windowPositions[key] = info;
            }
        }

        private void OnWindowDestroyed(IntPtr hwnd, string title, string processName)
        {
            if (!isListening || string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(title))
                return;

            // Save positions when a window closes
            SavePositions();
        }

        private string GetWindowKey(string processName, string title)
        {
            // Use a combination of process name and window title as key
            return $"{processName}_{title}";
        }

        private void LoadPositions()
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    string json = File.ReadAllText(dataFilePath);
                    windowPositions = JsonConvert.DeserializeObject<Dictionary<string, WindowInfo>>(json);
                }
            }
            catch (Exception ex)
            {
                // Log error or show in debug console
                Console.WriteLine($"Error loading window positions: {ex.Message}");
            }
        }

        private void SavePositions()
        {
            try
            {
                string json = JsonConvert.SerializeObject(windowPositions, Formatting.Indented);
                File.WriteAllText(dataFilePath, json);
            }
            catch (Exception ex)
            {
                // Log error or show in debug console
                Console.WriteLine($"Error saving window positions: {ex.Message}");
            }
        }
    }
}
