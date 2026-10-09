using System.Globalization;
using Carnac.Logic.Overlay;

namespace Carnac.Logic.Native
{
    public class DetailedScreen : NotifyPropertyChanged
    {
        /// <summary>The number shown in Preferences (1 based, in enumeration order).</summary>
        public int Index { get; set; }

        /// <summary>The GDI device name of the display, such as <c>\\.\DISPLAY2</c>; unlike <see cref="Index"/> it is what identifies the screen in the settings.</summary>
        public string DeviceName { get; set; }

        /// <summary>Whether this is the primary display, the one whose origin is the origin of the virtual desktop.</summary>
        public bool IsPrimary { get; set; }

        public string FriendlyName { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public double RelativeHeight { get; set; }
        public double RelativeWidth { get; set; }

        /// <summary>Where the screen is drawn in the monitor selector, relative to the top left of all screens.</summary>
        public double LayoutTop { get; set; }
        public double LayoutLeft { get; set; }

        /// <summary>The position and size in physical pixels, as the display driver reports them.</summary>
        public double Top { get; set; }
        public double Left { get; set; }

        /// <summary>
        /// The monitor rectangle in the coordinate space that the window APIs (<c>SetWindowPos</c>) of this process use.
        /// That is the physical rectangle unless the monitors have different scale factors: a process that is not
        /// per-monitor DPI aware then sees the other monitors scaled. Null when the operating system did not report
        /// it; <see cref="Left"/>, <see cref="Top"/>, <see cref="Width"/> and <see cref="Height"/> are used instead.
        /// </summary>
        public PixelRect? Bounds { get; set; }

        /// <summary>The line that describes the screen in Preferences, led by the number that is drawn on its tile.</summary>
        public string Description
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} ({2} x {3}{4})",
                    Index,
                    FriendlyName,
                    Width,
                    Height,
                    IsPrimary ? ", primary" : string.Empty);
            }
        }

        public bool NotificationPlacementTopLeft { get; set; }
        public bool NotificationPlacementBottomLeft { get; set; }
        public bool NotificationPlacementTopRight { get; set; }
        public bool NotificationPlacementBottomRight { get; set; }
    }
}