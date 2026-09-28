namespace Carnac.Logic.Overlay
{
    /// <summary>The part of the overlay window that <see cref="OverlayPlacementController"/> needs; implemented by the WPF window.</summary>
    public interface IOverlayWindow
    {
        /// <summary>Pixels per DIP (1.0 at 96 dpi) of the content of the window.</summary>
        double DpiScale { get; }

        /// <summary>Moves and resizes the window to a rectangle in the pixels of <c>SetWindowPos</c>; false when that failed.</summary>
        bool SetBounds(PixelRect bounds);
    }
}
