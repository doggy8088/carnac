namespace Carnac.Logic.Native
{
    /// <summary>What the display driver reports about one display that has a monitor attached to it.</summary>
    public class DisplayInfo
    {
        /// <summary>The GDI device name, such as <c>\\.\DISPLAY2</c>.</summary>
        public string DeviceName { get; set; }

        /// <summary>The name of the monitor, such as <c>Generic PnP Monitor</c>.</summary>
        public string MonitorName { get; set; }

        public bool IsPrimary { get; set; }

        /// <summary>The current position and size in physical pixels; the size is 0 when the display has no current mode.</summary>
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
