using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Carnac.Logic.KeyMonitor;

namespace Carnac.Logic.Models
{
    public sealed class KeyPress : KeyPressDefinition
    {
        public KeyPress(ProcessInfo process, InterceptKeyEventArgs interceptKeyEventArgs, bool winkeyPressed, IEnumerable<string> input) :
            base(interceptKeyEventArgs.Key, winkeyPressed, interceptKeyEventArgs.ShiftPressed, interceptKeyEventArgs.AltPressed, interceptKeyEventArgs.ControlPressed)
        {
            Process = process;
            InterceptKeyEventArgs = interceptKeyEventArgs;
            Input = input;
        }

        public ProcessInfo Process { get; private set; }

        public InterceptKeyEventArgs InterceptKeyEventArgs { get; private set; }

        public IEnumerable<string> Input { get; private set; }

        /// <summary>A modifier key (Ctrl, Alt, Win) pressed on its own, shown when the user asked for it.</summary>
        public bool IsModifierOnly
        {
            get { return InterceptKeyEventArgs.IsModifier(); }
        }

        public bool HasModifierPressed
        {
            get
            {
                return InterceptKeyEventArgs.AltPressed
                    || InterceptKeyEventArgs.ControlPressed
                    || WinkeyPressed;
            }
        }

        /// <summary>
        /// True when the key press is a shortcut rather than typing: Alt, Ctrl or the Windows key is held, or Shift is held
        /// with a key that does not type a character (Shift+Enter, Shift+Tab, Shift+F5, Shift+Left...).
        /// Shift with a letter, digit or punctuation key is just a capital letter or symbol and does not count.
        /// </summary>
        public bool IsShortcutLike
        {
            get
            {
                return HasModifierPressed
                    || (InterceptKeyEventArgs.ShiftPressed && !InterceptKeyEventArgs.Key.ProducesCharacter());
            }
        }

        public IEnumerable<string> GetTextParts()
        {
            return GetTextParts(KeyLabels.Culture);
        }

        /// <summary>
        /// The text to display for the key press. Key names are translated for the language of the culture
        /// (see <see cref="KeyLabels"/>); null keeps the canonical English names.
        /// </summary>
        public IEnumerable<string> GetTextParts(CultureInfo culture)
        {
            var isFirst = true;
            foreach (var text in Input)
            {
                if (!isFirst)
                {
                    yield return " + ";
                }
                else
                {
                    isFirst = false;
                }
                yield return Format(text, HasModifierPressed, culture);
            }
        }

        static string Format(string text, bool isShortcut, CultureInfo culture)
        {
            if (text == "Left")
                return GetString(8592);
            if (text == "Up")
                return GetString(8593);
            if (text == "Right")
                return GetString(8594);
            if (text == "Down")
                return GetString(8595);

            // If the space is part of a shortcut sequence
            // present it as a primitive key. E.g. Ctrl+Space.
            // Otherwise we want to preserve a space as part of
            // what is probably a sentence.
            if (text == " " && isShortcut)
                return KeyLabels.Localize("Space", culture);

            return KeyLabels.Localize(text, culture);
        }

        static string GetString(int decimalValue)
        {
            return new string(new[] { (char)decimalValue });
        }

        #region Equality overides

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((KeyPress)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (Process != null ? Process.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InterceptKeyEventArgs != null ? InterceptKeyEventArgs.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Input != null ? Input.GetHashCode() : 0);
                return hashCode;
            }
        }

        bool Equals(KeyPress other)
        {
            return base.Equals(other)
                && Equals(Process, other.Process)
                && Equals(InterceptKeyEventArgs, other.InterceptKeyEventArgs)
                && Input.SequenceEqual(other.Input);
        }
        #endregion
    }
}