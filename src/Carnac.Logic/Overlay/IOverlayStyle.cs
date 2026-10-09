namespace Carnac.Logic.Overlay
{
    /// <summary>The part of the overlay window that <see cref="OverlayStyleController"/> needs; implemented by the WPF window.</summary>
    public interface IOverlayStyle
    {
        /// <summary>
        /// Applies the window styles of the overlay. When <paramref name="captureFriendly"/> is true the window is listed by
        /// capture tools such as OBS (it is not a tool window); it stays click-through and is never activated either way.
        /// </summary>
        void SetCaptureFriendly(bool captureFriendly);
    }
}
