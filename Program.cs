using System;
using System.Windows.Forms;

namespace RememberMyDisplay
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            // Use the ApplicationContext to run the application with our system tray icon
            Application.Run(new WindowTrackerContext());
        }
    }
}
