using System;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Shows balloon notifications on Carnac's notification-area (tray) icon.
    /// Errors, update notices and elevation hints all go through this one seam so that no other class
    /// has to touch the tray icon directly. Implementations marshal to the UI thread, so callers can use
    /// this from any thread.
    /// </summary>
    public interface ITrayNotifier
    {
        /// <param name="title">Balloon title (may be empty).</param>
        /// <param name="text">Balloon body; must not be empty.</param>
        /// <param name="icon">Icon shown next to the title.</param>
        /// <param name="onClick">Invoked on the UI thread when the user clicks the balloon; may be null.</param>
        void ShowBalloon(string title, string text, ToolTipIcon icon, Action onClick);
    }
}
