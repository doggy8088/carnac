using System;

namespace Carnac.Logic.Overlay
{
    /// <summary>
    /// The extended window styles that make the overlay behave like an overlay. Kept separate from the
    /// <c>GetWindowLong</c>/<c>SetWindowLong</c> calls so the decision can be unit tested.
    /// </summary>
    public static class OverlayWindowStyles
    {
        /// <summary>
        /// The title of the overlay window. It never changes, so OBS and other capture tools can select the
        /// window by title (the class name of a WPF window contains a new GUID with every start).
        /// </summary>
        public const string WindowTitle = "Carnac Overlay";

        /// <summary>
        /// The extended style for the overlay window, given the one it currently has: click-through
        /// (<c>WS_EX_TRANSPARENT</c>) and never activated, so it can never take the keyboard focus (<c>WS_EX_NOACTIVATE</c>).
        /// It is hidden from Alt+Tab and window lists (<c>WS_EX_TOOLWINDOW</c>) unless it should be capture friendly:
        /// capture tools such as OBS do not list tool windows.
        /// </summary>
        public static int Apply(int extendedStyle, bool captureFriendly)
        {
            var style = extendedStyle
                | Win32Methods.WS_EX_TRANSPARENT
                | Win32Methods.WS_EX_NOACTIVATE;

            return captureFriendly
                ? style & ~Win32Methods.WS_EX_TOOLWINDOW
                : style | Win32Methods.WS_EX_TOOLWINDOW;
        }

        /// <summary>
        /// Whether a change of the named <see cref="Models.PopupSettings"/> property means the window styles have to be applied again.
        /// A null or empty name means that every property changed.
        /// </summary>
        public static bool AffectsStyles(string propertyName)
        {
            return string.IsNullOrEmpty(propertyName)
                || string.Equals(propertyName, "CaptureFriendlyWindow", StringComparison.Ordinal);
        }
    }
}
