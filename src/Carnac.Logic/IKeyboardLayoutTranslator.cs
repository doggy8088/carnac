using System.Windows.Forms;

namespace Carnac.Logic
{
    public interface IKeyboardLayoutTranslator
    {
        /// <summary>
        /// The text a key types on the keyboard layout of the window that has the focus. Null when there is no answer
        /// (the key is not a character key, the layout cannot say): the caller names the key as it always did. Empty
        /// when the key types nothing that can be seen on its own: a dead key, whose accent comes with the key after
        /// it, or a joiner. Otherwise the text.
        /// </summary>
        /// <param name="controlAlt">
        /// Ctrl and Alt are both down. Windows reports AltGr like that, and it is also what a Ctrl+Alt shortcut is:
        /// the translator says whether it was AltGr, by returning the character or null.
        /// </param>
        string GetText(Keys key, bool shift, bool controlAlt);
    }
}
