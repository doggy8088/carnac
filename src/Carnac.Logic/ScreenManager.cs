using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;

namespace Carnac.Logic
{
    public class ScreenManager : IScreenManager
    {
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("User32.dll")]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        public IEnumerable<DetailedScreen> GetScreens()
        {
            return ScreenList.Build(GetDisplays(), GetMonitorBounds());
        }

        static List<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            var d = new DISPLAY_DEVICE();
            d.cb = Marshal.SizeOf(d);
            try
            {
                for (uint id = 0; EnumDisplayDevices(null, id, ref d, 0); id++)
                {
                    d.cb = Marshal.SizeOf(d);

                    var x = new DISPLAY_DEVICE();
                    x.cb = Marshal.SizeOf(x);

                    //Get the actual monitor
                    EnumDisplayDevices(d.DeviceName, 0, ref x, 0);

                    if (string.IsNullOrEmpty(x.DeviceName) || string.IsNullOrEmpty(x.DeviceString))
                        continue;

                    var display = new DisplayInfo
                    {
                        DeviceName = d.DeviceName,
                        MonitorName = x.DeviceString,
                        IsPrimary = (d.StateFlags & DisplayDeviceStateFlags.PrimaryDevice) != 0
                    };

                    var mode = new DEVMODE();
                    mode.dmSize = (ushort)Marshal.SizeOf(mode);
                    if (EnumDisplaySettings(d.DeviceName, -1, ref mode))
                    {
                        display.Width = (int)mode.dmPelsWidth;
                        display.Height = (int)mode.dmPelsHeight;
                        display.Top = mode.dmPosition.y;
                        display.Left = mode.dmPosition.x;
                    }

                    displays.Add(display);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Carnac: could not enumerate all displays: {0}", ex);
            }

            return displays;
        }

        /// <summary>
        /// The monitor rectangles by device name, in the coordinate space of the window APIs of this process.
        /// The display driver reports physical pixels, but a process that is not per-monitor DPI aware gets scaled
        /// coordinates for monitors that do not have the scale factor of the system, and <c>SetWindowPos</c> expects those.
        /// </summary>
        static List<KeyValuePair<string, PixelRect>> GetMonitorBounds()
        {
            var bounds = new List<KeyValuePair<string, PixelRect>>();
            try
            {
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    var rectangle = screen.Bounds;
                    bounds.Add(new KeyValuePair<string, PixelRect>(
                        screen.DeviceName,
                        new PixelRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height)));
                }
            }
            catch (Exception ex)
            {
                // Without them the physical rectangles are used, which is right as long as all monitors scale alike.
                Trace.TraceWarning("Carnac: could not read the monitor rectangles: {0}", ex);
            }

            return bounds;
        }
    }
}
