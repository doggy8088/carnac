using System.Windows.Forms;
using Carnac.Logic.Enums;

namespace Carnac.Logic
{
    /// <summary>Classifies keys into the groups the user can show or hide.</summary>
    public static class KeyCategories
    {
        /// <summary>The single category of a key. Modifier flags in <paramref name="key"/> are ignored.</summary>
        public static KeyCategory For(Keys key)
        {
            key = key & Keys.KeyCode;

            if (key >= Keys.A && key <= Keys.Z)
                return KeyCategory.Letters;

            // NumPad0-NumPad9 sit directly before the keypad operators
            if ((key >= Keys.D0 && key <= Keys.D9) || (key >= Keys.NumPad0 && key <= Keys.NumPad9))
                return KeyCategory.Digits;

            // Multiply, Add, Separator, Subtract, Decimal and Divide on the numeric keypad
            if (key >= Keys.Multiply && key <= Keys.Divide)
                return KeyCategory.Punctuation;

            // Oem1 (;) .. Oem3 (`) also contains Oemplus, Oemcomma, OemMinus and OemPeriod;
            // Oem4 ([) .. Oem8 are the bracket, backslash and quote keys; Oem102 is the extra key of ISO layouts.
            if ((key >= Keys.Oem1 && key <= Keys.Oem3) || (key >= Keys.Oem4 && key <= Keys.Oem8) || key == Keys.Oem102)
                return KeyCategory.Punctuation;

            switch (key)
            {
                case Keys.Space:
                case Keys.Enter:
                case Keys.Tab:
                    return KeyCategory.Whitespace;
                case Keys.Back:
                case Keys.Delete:
                case Keys.Insert:
                case Keys.Escape:
                    return KeyCategory.Editing;
                case Keys.Up:
                case Keys.Down:
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return KeyCategory.Navigation;
            }

            if (key >= Keys.F1 && key <= Keys.F24)
                return KeyCategory.Function;

            return KeyCategory.Other;
        }
    }
}
