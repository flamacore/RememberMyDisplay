using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RememberMyDisplay
{
    public class WindowInfo
    {
        public string ProcessName { get; set; }
        public string Title { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string DisplayDeviceName { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
