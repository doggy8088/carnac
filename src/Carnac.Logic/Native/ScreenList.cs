using System;
using System.Collections.Generic;
using Carnac.Logic.Overlay;

namespace Carnac.Logic.Native
{
    /// <summary>Turns what the operating system reports about the displays into the list of screens the app works with.</summary>
    public static class ScreenList
    {
        /// <summary>
        /// One <see cref="DetailedScreen"/> per usable display.
        /// </summary>
        /// <param name="displays">The displays that have a monitor, in enumeration order. Each one takes the next screen number,
        /// also when it is skipped because it has no current mode, so numbers saved earlier keep their meaning.</param>
        /// <param name="monitorBounds">The monitor rectangles in window coordinates by device name (case insensitive),
        /// or null when they are not known.</param>
        /// <remarks>
        /// The list is in enumeration order, not sorted by position: the position of a screen is data
        /// (<see cref="DetailedScreen.Left"/>, <see cref="DetailedScreen.Top"/>), and the number of a screen must not depend on it.
        /// </remarks>
        public static List<DetailedScreen> Build(IEnumerable<DisplayInfo> displays, IEnumerable<KeyValuePair<string, PixelRect>> monitorBounds)
        {
            if (displays == null) throw new ArgumentNullException("displays");

            var screens = new List<DetailedScreen>();
            var index = 1;
            foreach (var display in displays)
            {
                if (display == null)
                    continue;

                var screen = new DetailedScreen
                {
                    Index = index++,
                    DeviceName = display.DeviceName,
                    IsPrimary = display.IsPrimary,
                    FriendlyName = display.MonitorName,
                    Left = display.Left,
                    Top = display.Top,
                    Width = display.Width,
                    Height = display.Height
                };

                // skip this value if it doesn't appear to be a valid screen
                if (screen.Width <= 0 || screen.Height <= 0)
                    continue;

                PixelRect bounds;
                if (TryFindBounds(monitorBounds, display.DeviceName, out bounds))
                    screen.Bounds = bounds;

                screens.Add(screen);
            }

            return screens;
        }

        static bool TryFindBounds(IEnumerable<KeyValuePair<string, PixelRect>> monitorBounds, string deviceName, out PixelRect bounds)
        {
            bounds = default(PixelRect);
            if (monitorBounds == null || string.IsNullOrEmpty(deviceName))
                return false;

            foreach (var pair in monitorBounds)
            {
                if (string.Equals(pair.Key, deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    bounds = pair.Value;
                    return true;
                }
            }

            return false;
        }
    }
}
