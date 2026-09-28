using System;
using System.Collections.Generic;
using System.Linq;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;

namespace Carnac.Logic.Overlay
{
    /// <summary>
    /// Decides where the overlay window goes and how big it is. Everything here is plain arithmetic on
    /// rectangles so it can be unit tested; <c>KeyShowView</c> only feeds it and hands the result to <c>SetWindowPos</c>.
    /// </summary>
    /// <remarks>
    /// The overlay used to be a transparent, maximized, topmost window that covered its whole monitor.
    /// Windows, ShareX, Wallpaper Engine and friends treat such a window as a full screen application
    /// (Focus Assist switches on, an auto-hide taskbar cannot be revealed, ...). The overlay is now only as wide as the
    /// popups can be and always keeps a gap to every edge of its monitor, so it can never cover the monitor.
    /// </remarks>
    public static class OverlayPlacement
    {
        /// <summary>Pixels kept free between the overlay and every edge of its monitor (the taskbar edge included).</summary>
        public const int EdgeGapPixels = 1;

        /// <summary>Width in DIPs of the application icon column of a popup (20 wide plus a margin of 3 on both sides).</summary>
        public const int IconWidth = 26;

        /// <summary>
        /// Width in DIPs reserved for the borders and paddings around a popup (item border and padding, list box and
        /// list view chrome). Deliberately generous: a slightly too wide transparent window is harmless, a clipped popup is not.
        /// </summary>
        public const int ItemChromeWidth = 40;

        /// <summary>
        /// Width in DIPs of the widest area popups can occupy: the widest popup plus the left and right offsets
        /// that <c>KeyShowView</c> applies as the margin of its popup list.
        /// </summary>
        public static double GetContentWidth(int itemMaxWidth, int leftOffset, int rightOffset)
        {
            return Math.Max(0, itemMaxWidth) + IconWidth + ItemChromeWidth + Math.Max(0, leftOffset) + Math.Max(0, rightOffset);
        }

        /// <summary>
        /// Whether popups hug the left edge. Mirrors <see cref="PopupSettings.Alignment"/>, which aligns the popup list
        /// inside the overlay: everything that is not a left placement (including an unset placement) is right aligned.
        /// </summary>
        public static bool IsLeftAligned(NotificationPlacement placement)
        {
            return placement == NotificationPlacement.TopLeft || placement == NotificationPlacement.BottomLeft;
        }

        /// <summary>
        /// The window rectangle, in pixels, for an overlay whose popups need <paramref name="contentWidth"/> DIPs.
        /// </summary>
        /// <param name="screen">The monitor the overlay belongs on.</param>
        /// <param name="placement">The corner the popups are anchored to; only its left or right side matters for the window.</param>
        /// <param name="contentWidth">The width the popups need, in DIPs (see <see cref="GetContentWidth"/>).</param>
        /// <param name="dpiScale">Pixels per DIP (1.0 at 96 dpi). Values that are not positive are treated as 1.0.</param>
        /// <returns>
        /// A window that is anchored to the left or right side of the monitor, spans its height, and keeps
        /// <see cref="EdgeGapPixels"/> to every monitor edge, so it never covers the whole monitor.
        /// </returns>
        public static PixelRect Calculate(PixelRect screen, NotificationPlacement placement, double contentWidth, double dpiScale)
        {
            var scale = dpiScale > 0 && !double.IsInfinity(dpiScale) ? dpiScale : 1.0;
            var content = contentWidth > 0 ? contentWidth : 0.0;

            var availableWidth = Math.Max(1, screen.Width - 2 * EdgeGapPixels);
            var availableHeight = Math.Max(1, screen.Height - 2 * EdgeGapPixels);

            var width = (int)Math.Min(availableWidth, Math.Max(1.0, Math.Ceiling(content * scale)));

            var x = IsLeftAligned(placement)
                ? screen.X + EdgeGapPixels
                : screen.Right - EdgeGapPixels - width;

            return new PixelRect(x, screen.Y + EdgeGapPixels, width, availableHeight);
        }

        /// <summary>
        /// The window rectangle for the current settings on the screen the settings select,
        /// or null when there is no screen at all (nothing to place the overlay on).
        /// </summary>
        public static PixelRect? Resolve(IEnumerable<DetailedScreen> screens, PopupSettings settings, double dpiScale)
        {
            if (screens == null) throw new ArgumentNullException("screens");
            if (settings == null) throw new ArgumentNullException("settings");

            var screen = SelectScreen(screens, settings.ScreenDeviceName, settings.Screen);
            if (screen == null)
                return null;

            var contentWidth = GetContentWidth(settings.ItemMaxWidth, settings.LeftOffset, settings.RightOffset);
            return Calculate(ToPixelRect(screen), settings.Placement, contentWidth, dpiScale);
        }

        /// <summary>
        /// The screen that is selected by its device name (case insensitive); by its number only when there is no device name
        /// (settings saved by an earlier version). A screen that cannot be found, for example because its monitor is unplugged,
        /// is replaced by the primary screen, else by the first screen. Null when there are no screens.
        /// </summary>
        public static DetailedScreen SelectScreen(IEnumerable<DetailedScreen> screens, string deviceName, int index)
        {
            if (screens == null) throw new ArgumentNullException("screens");

            var all = screens.Where(s => s != null).ToList();
            var selected = string.IsNullOrEmpty(deviceName)
                ? all.FirstOrDefault(s => s.Index == index)
                : all.FirstOrDefault(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

            return selected
                ?? all.FirstOrDefault(s => s.IsPrimary)
                ?? all.FirstOrDefault(s => s.Left == 0 && s.Top == 0)
                ?? all.FirstOrDefault();
        }

        /// <summary>
        /// The screen rectangle in the coordinate space of <c>SetWindowPos</c>: the monitor bounds of the window APIs when
        /// they are known, else the physical rectangle of the display driver (the same thing unless the monitors scale differently).
        /// </summary>
        public static PixelRect ToPixelRect(DetailedScreen screen)
        {
            if (screen == null) throw new ArgumentNullException("screen");

            if (screen.Bounds.HasValue)
                return screen.Bounds.Value;

            return new PixelRect(
                (int)Math.Round(screen.Left),
                (int)Math.Round(screen.Top),
                (int)Math.Round(screen.Width),
                (int)Math.Round(screen.Height));
        }

        /// <summary>
        /// Whether a change of the named <see cref="PopupSettings"/> property means the overlay window has to be placed again.
        /// A null or empty name means that every property changed.
        /// </summary>
        public static bool AffectsPlacement(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return true;

            return AffectsScreenSelection(propertyName)
                || string.Equals(propertyName, "Placement", StringComparison.Ordinal)
                || string.Equals(propertyName, "ItemMaxWidth", StringComparison.Ordinal)
                || string.Equals(propertyName, "LeftOffset", StringComparison.Ordinal)
                || string.Equals(propertyName, "RightOffset", StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether a change of the named <see cref="PopupSettings"/> property can select another screen, so the list of
        /// screens has to be read again. A null or empty name means that every property changed.
        /// </summary>
        public static bool AffectsScreenSelection(string propertyName)
        {
            return string.IsNullOrEmpty(propertyName)
                || string.Equals(propertyName, "Screen", StringComparison.Ordinal)
                || string.Equals(propertyName, "ScreenDeviceName", StringComparison.Ordinal);
        }
    }
}
