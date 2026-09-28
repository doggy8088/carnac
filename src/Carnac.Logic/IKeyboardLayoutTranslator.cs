using System.Windows.Forms;

namespace Carnac.Logic
{
    public interface IKeyboardLayoutTranslator
    {
        /// <summary>
        /// The text a key types on the keyboard layout of the window that has the focus, or null when the key
        /// is not a character key or the layout cannot say. A dead key returns its accent.
        /// </summary>
        /// <param name="controlAlt">
        /// Ctrl and Alt are both down. Windows reports AltGr like that, and it is also what a Ctrl+Alt shortcut is:
        /// the translator says whether it was AltGr, by returning the character or null.
        /// </param>
        string GetText(Keys key, bool shift, bool controlAlt);
    }
}
