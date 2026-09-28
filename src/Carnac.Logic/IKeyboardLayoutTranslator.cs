using System.Windows.Forms;

namespace Carnac.Logic
{
    public interface IKeyboardLayoutTranslator
    {
        /// <summary>
        /// The text a key types on the keyboard layout of the window that has the focus, or null when the key
        /// is not a character key or the layout cannot say. A dead key returns its accent.
        /// </summary>
        /// <param name="altGr">AltGr, which Windows reports as Ctrl+Alt, on layouts that have it.</param>
        string GetText(Keys key, bool shift, bool altGr);
    }
}
