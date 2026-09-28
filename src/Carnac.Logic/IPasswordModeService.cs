using Carnac.Logic.KeyMonitor;

namespace Carnac.Logic
{
    public interface IPasswordModeService
    {
        /// <summary>
        /// Looks at a key press: the configured silent-mode and pause hotkeys switch the shared state.
        /// </summary>
        /// <returns>True when the key must not be shown: it is one of the hotkeys, or silent mode is on.</returns>
        bool CheckPasswordMode(InterceptKeyEventArgs key);
    }
}
