namespace Carnac.Logic.Overlay
{
    /// <summary>
    /// The extended window styles that make the overlay behave like an overlay. Kept separate from the
    /// <c>GetWindowLong</c>/<c>SetWindowLong</c> calls so the decision can be unit tested.
    /// </summary>
    public static class OverlayWindowStyles
    {
        /// <summary>
        /// The extended style for the overlay window, given the one it currently has: click-through
        /// (<c>WS_EX_TRANSPARENT</c>), hidden from Alt+Tab and window lists (<c>WS_EX_TOOLWINDOW</c>) and
        /// never activated, so it can never take the keyboard focus (<c>WS_EX_NOACTIVATE</c>).
        /// </summary>
        public static int Apply(int extendedStyle)
        {
            return extendedStyle
                | Win32Methods.WS_EX_TRANSPARENT
                | Win32Methods.WS_EX_TOOLWINDOW
                | Win32Methods.WS_EX_NOACTIVATE;
        }
    }
}
