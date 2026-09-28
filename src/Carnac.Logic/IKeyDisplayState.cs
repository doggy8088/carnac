using System;

namespace Carnac.Logic
{
    /// <summary>
    /// The one place that knows whether Carnac is currently paused or in silent mode.
    /// The state is intentionally not persisted: Carnac always starts un-paused and not silent.
    /// The tray menu and the hotkeys change it, the key/message pipeline and the tray icon observe it.
    /// </summary>
    public interface IKeyDisplayState
    {
        /// <summary>While paused no key presses are turned into popups.</summary>
        bool IsPaused { get; }

        /// <summary>While silent (password mode) key presses are dropped before they reach the message pipeline.</summary>
        bool IsSilent { get; }

        void SetPaused(bool paused);

        void SetSilent(bool silent);

        /// <returns>The new value of <see cref="IsPaused"/>.</returns>
        bool TogglePaused();

        /// <returns>The new value of <see cref="IsSilent"/>.</returns>
        bool ToggleSilent();

        /// <summary>
        /// Raised after <see cref="IsPaused"/> or <see cref="IsSilent"/> changed. It can be raised on any thread
        /// (for example the keyboard hook's), so handlers that touch UI must marshal to the UI thread.
        /// </summary>
        event EventHandler Changed;
    }
}
