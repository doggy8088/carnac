using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Parses and formats hotkey strings such as <c>Ctrl+Alt+P</c>: any of Ctrl, Alt and Shift followed by exactly one key,
    /// separated by '+', case-insensitive. The key part is resolved with <see cref="ReplaceKey.ToKey"/>.
    /// A hotkey needs at least one modifier, otherwise it would trigger while typing.
    /// </summary>
    public static class HotkeyParser
    {
        // Keys that can never be the "main" key of a hotkey: the modifiers themselves and mouse buttons.
        static readonly HashSet<Keys> NotUsableAsKey = new HashSet<Keys>
        {
            Keys.None, Keys.LButton, Keys.RButton, Keys.MButton, Keys.XButton1, Keys.XButton2,
            Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
            Keys.Menu, Keys.LMenu, Keys.RMenu,
            Keys.LWin, Keys.RWin,
            Keys.KeyCode, Keys.Modifiers
        };

        static readonly HashSet<Keys> MouseButtons = new HashSet<Keys>
        {
            Keys.LButton, Keys.RButton, Keys.MButton, Keys.XButton1, Keys.XButton2
        };

        // Several names share one value (PageDown/Next, Enter/Return, ...): pick one name so that formatting is stable.
        static readonly Dictionary<Keys, string> KeyNames = new Dictionary<Keys, string>
        {
            {Keys.Enter, "Enter"},
            {Keys.PageUp, "PageUp"},
            {Keys.PageDown, "PageDown"},
            {Keys.CapsLock, "CapsLock"},
            {Keys.Oem1, "Oem1"},
            {Keys.Oem2, "Oem2"},
            {Keys.Oem3, "Oem3"},
            {Keys.Oem4, "Oem4"},
            {Keys.Oem5, "Oem5"},
            {Keys.Oem6, "Oem6"},
            {Keys.Oem7, "Oem7"},
            {Keys.Oem102, "Oem102"}
        };

        /// <summary>Parses <paramref name="text"/>; when it is not a valid hotkey <paramref name="error"/> says why.</summary>
        public static bool TryParse(string text, out KeyPressDefinition hotkey, out string error)
        {
            hotkey = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "No hotkey was entered.";
                return false;
            }

            var parts = text.Split('+').Select(part => part.Trim()).ToArray();
            if (parts.Any(part => part.Length == 0))
            {
                error = "A hotkey looks like Ctrl+Alt+P: modifiers and one key separated by '+', without empty parts.";
                return false;
            }

            bool control = false, alt = false, shift = false;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                bool isControl = IsName(part, "Ctrl") || IsName(part, "Control");
                bool isAlt = IsName(part, "Alt");
                bool isShift = IsName(part, "Shift");
                if (IsName(part, "Win") || IsName(part, "Windows"))
                {
                    error = "The Windows key can't be part of a hotkey.";
                    return false;
                }

                if (!isControl && !isAlt && !isShift)
                {
                    error = string.Format("'{0}' is not a modifier. Use Ctrl, Alt or Shift, followed by exactly one key.", part);
                    return false;
                }

                if ((isControl && control) || (isAlt && alt) || (isShift && shift))
                {
                    error = string.Format("'{0}' is listed twice.", part);
                    return false;
                }

                control |= isControl;
                alt |= isAlt;
                shift |= isShift;
            }

            var keyText = parts[parts.Length - 1];
            if (IsName(keyText, "Win") || IsName(keyText, "Windows"))
            {
                error = "The Windows key can't be part of a hotkey.";
                return false;
            }

            if (IsName(keyText, "Ctrl") || IsName(keyText, "Control") || IsName(keyText, "Alt") || IsName(keyText, "Shift"))
            {
                error = "Ctrl, Alt and Shift are modifiers: add one more key, for example Ctrl+Alt+P.";
                return false;
            }

            Keys key;
            if (!TryParseKey(keyText, out key))
            {
                error = string.Format("'{0}' is not a key Carnac knows.", keyText);
                return false;
            }

            return TryCreate(key, control, alt, shift, false, out hotkey, out error);
        }

        /// <summary>Validates a key combination that was captured from the keyboard.</summary>
        public static bool TryCreate(Keys key, bool control, bool alt, bool shift, bool windows, out KeyPressDefinition hotkey, out string error)
        {
            hotkey = null;
            if (windows)
            {
                error = "The Windows key can't be part of a hotkey.";
                return false;
            }

            if (MouseButtons.Contains(key))
            {
                error = "Mouse buttons can't be used in a hotkey.";
                return false;
            }

            if ((key & Keys.Modifiers) != 0 || NotUsableAsKey.Contains(key))
            {
                error = key == Keys.None
                    ? "That is not a key Carnac knows."
                    : "Ctrl, Alt, Shift and Windows are modifiers: add one more key, for example Ctrl+Alt+P.";
                return false;
            }

            if (!Enum.IsDefined(typeof(Keys), key))
            {
                error = "That is not a key Carnac knows.";
                return false;
            }

            if (!control && !alt && !shift)
            {
                error = "Add at least one modifier (Ctrl, Alt or Shift), otherwise the hotkey would trigger while typing.";
                return false;
            }

            hotkey = new KeyPressDefinition(key, false, shift, alt, control);
            error = null;
            return true;
        }

        /// <summary>Canonical text of a hotkey: modifiers in the order Ctrl, Alt, Shift, then the key.</summary>
        public static string Format(KeyPressDefinition hotkey)
        {
            if (hotkey == null)
                throw new ArgumentNullException("hotkey");

            var parts = new List<string>();
            if (hotkey.ControlPressed)
                parts.Add("Ctrl");
            if (hotkey.AltPressed)
                parts.Add("Alt");
            if (hotkey.ShiftPressed)
                parts.Add("Shift");
            if (hotkey.WinkeyPressed)
                parts.Add("Win");
            parts.Add(FormatKey(hotkey.Key));
            return string.Join("+", parts);
        }

        static string FormatKey(Keys key)
        {
            if (key >= Keys.D0 && key <= Keys.D9)
                return ((char)('0' + (key - Keys.D0))).ToString();

            string name;
            return KeyNames.TryGetValue(key, out name) ? name : key.ToString();
        }

        static bool TryParseKey(string keyText, out Keys key)
        {
            key = Keys.None;

            // Enum.TryParse would read "1" as the number 1 (a mouse button) and "A, B" as two flags or-ed together.
            if (keyText.Length == 1 && keyText[0] >= '0' && keyText[0] <= '9')
            {
                key = Keys.D0 + (keyText[0] - '0');
                return true;
            }

            // (a lone comma is fine, that is the comma key)
            if ((keyText.Length > 1 && keyText.IndexOf(',') >= 0) || keyText.All(char.IsDigit))
                return false;

            var parsed = ReplaceKey.ToKey(keyText);
            if (parsed == null)
                return false;

            key = parsed.Value;
            return true;
        }

        static bool IsName(string text, string name)
        {
            return string.Equals(text, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
