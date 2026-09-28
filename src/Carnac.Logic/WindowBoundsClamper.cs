using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Carnac.Logic
{
    /// <summary>
    /// Pure rectangle maths that keeps a restored window reachable after the monitor layout changed
    /// (monitor unplugged, resolution lowered, settings copied from another machine).
    /// All values are in the same coordinate space (device independent pixels for WPF windows).
    /// </summary>
    public static class WindowBoundsClamper
    {
        /// <summary>Minimum width of the window that has to lie on a monitor for the window to count as reachable.</summary>
        public const double MinimumVisibleWidth = 100;

        /// <summary>Minimum height of the window that has to lie on a monitor for the window to count as reachable.</summary>
        public const double MinimumVisibleHeight = 40;

        /// <summary>
        /// Returns bounds that are guaranteed to lie inside the virtual screen (the bounding box of all
        /// <paramref name="workAreas"/>) and to be at least <paramref name="minimumSize"/> big, unless
        /// the virtual screen itself is smaller than the minimum size.
        /// </summary>
        /// <param name="desired">The bounds the window currently has (for example restored from the settings).</param>
        /// <param name="minimumSize">The smallest size the window is designed for.</param>
        /// <param name="workAreas">The working area of every attached monitor.</param>
        /// <param name="primaryWorkArea">The working area of the primary monitor, used to centre a window that is lost.</param>
        /// <remarks>
        /// A window that has (almost) no part on any monitor is centred on the primary monitor. A window that is
        /// partially outside the virtual screen is moved back by the smallest possible distance. A window that is
        /// bigger than the virtual screen is shrunk to it. When no monitor is known the desired bounds are returned unchanged.
        /// </remarks>
        public static Rect Clamp(Rect desired, Size minimumSize, IEnumerable<Rect> workAreas, Rect primaryWorkArea)
        {
            if (workAreas == null)
                throw new ArgumentNullException("workAreas");

            var areas = workAreas.Where(IsUsableArea).ToList();
            if (areas.Count == 0)
                return desired;

            var virtualScreen = areas.Aggregate((a, b) => Rect.Union(a, b));
            var primary = IsUsableArea(primaryWorkArea) ? primaryWorkArea : areas[0];

            var width = ClampLength(desired.Width, minimumSize.Width, virtualScreen.Width);
            var height = ClampLength(desired.Height, minimumSize.Height, virtualScreen.Height);

            var x = desired.X;
            var y = desired.Y;
            var isPlaced = IsFinite(x) && IsFinite(y);

            if (!isPlaced || !IsReachable(new Rect(x, y, width, height), areas))
            {
                return CentreOn(primary, width, height, virtualScreen);
            }

            // Moving a reachable window towards the inside of the virtual screen never reduces its overlap
            // with a monitor, so the result is still reachable.
            return new Rect(
                MoveInto(x, width, virtualScreen.Left, virtualScreen.Right),
                MoveInto(y, height, virtualScreen.Top, virtualScreen.Bottom),
                width,
                height);
        }

        static Rect CentreOn(Rect area, double width, double height, Rect virtualScreen)
        {
            var x = area.Left + (area.Width - width) / 2;
            var y = area.Top + (area.Height - height) / 2;

            return new Rect(
                MoveInto(x, width, virtualScreen.Left, virtualScreen.Right),
                MoveInto(y, height, virtualScreen.Top, virtualScreen.Bottom),
                width,
                height);
        }

        // Shifts [start, start + length] into [min, max]. When it does not fit, the start edge wins.
        static double MoveInto(double start, double length, double min, double max)
        {
            return Math.Max(min, Math.Min(start, max - length));
        }

        static double ClampLength(double length, double minimum, double maximum)
        {
            if (!IsFinite(length) || length <= 0)
                length = minimum;

            length = Math.Max(length, minimum);
            return Math.Min(length, Math.Max(maximum, minimum));
        }

        static bool IsReachable(Rect window, IEnumerable<Rect> areas)
        {
            var requiredWidth = Math.Min(window.Width, MinimumVisibleWidth);
            var requiredHeight = Math.Min(window.Height, MinimumVisibleHeight);

            return areas.Any(area =>
            {
                var overlap = Rect.Intersect(window, area);
                return !overlap.IsEmpty && overlap.Width >= requiredWidth && overlap.Height >= requiredHeight;
            });
        }

        static bool IsUsableArea(Rect area)
        {
            return !area.IsEmpty
                   && IsFinite(area.X) && IsFinite(area.Y)
                   && IsFinite(area.Width) && IsFinite(area.Height)
                   && area.Width > 0 && area.Height > 0;
        }

        static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
