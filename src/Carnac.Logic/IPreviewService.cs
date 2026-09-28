using System;

namespace Carnac.Logic
{
    /// <summary>
    /// Keeps sample popups on screen while something needs them, such as the Preferences window,
    /// so that changes to the settings can be seen without typing in another application.
    /// </summary>
    public interface IPreviewService
    {
        /// <summary>
        /// Shows the sample popups (they stay until they are hidden again, they never fade out) and returns a token
        /// that hides them again when disposed. The popups are shown once however many tokens are alive, and hidden when the last
        /// one is disposed. Disposing a token more than once is harmless.
        /// </summary>
        IDisposable Show();
    }
}
