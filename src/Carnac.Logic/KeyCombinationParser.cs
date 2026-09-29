using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Parses the key notation used by keymap files into <see cref="KeyPressDefinition"/>s.
    /// <list type="bullet">
    /// <item>A combination is modifiers joined with '+' followed by exactly one key: <c>Ctrl+Shift+N</c>.
    /// Modifiers are <c>Ctrl</c> (or <c>Control</c>), <c>Alt</c>, <c>Shift</c> and <c>Win</c> (or <c>Windows</c>).
    /// The key is a <see cref="Keys"/> name (<c>Enter</c>, <c>F5</c>, <c>PageDown</c>), a single digit,
    /// or a character such as <c>,</c> <c>.</c> <c>-</c> <c>/</c>. A character that is typed with Shift on a US keyboard
    /// (<c>+</c> <c>!</c> <c>?</c> <c>_</c> <c>{</c> ...) means that shifted key: <c>Ctrl++</c> is Ctrl+Shift+Oemplus, while
    /// <c>Ctrl+=</c> and <c>Ctrl+Oemplus</c> are the unshifted key.</item>
    /// <item>A sequence ("chord") is combinations separated by commas: <c>Ctrl+K,Ctrl+C</c>. Whitespace around the
    /// commas is ignored. A comma that directly follows a '+' is the comma key itself (<c>Ctrl+,</c>); on its own
    /// the comma key is written <c>Oemcomma</c>.</item>
    /// </list>
    /// The static methods are pure and report why a text is not valid. The instance methods additionally report a
    /// warning through the injected sink (default: <see cref="TraceWarning"/>) so bad entries are never dropped silently.
    /// </summary>
    public class KeyCombinationParser
    {
        // Names people commonly write that are not System.Windows.Forms.Keys names.
        static readonly Dictionary<string, string> KeyNameHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"Up Arrow", "Up"},
            {"Down Arrow", "Down"},
            {"Left Arrow", "Left"},
            {"Right Arrow", "Right"},
            {"Esc", "Escape"},
            {"Backspace", "Back"},
            {"Bkspce", "Back"},
            {"PgUp", "PageUp"},
            {"PgDn", "PageDown"},
            {"Break", "Pause"}
        };

        // The physical modifier keys. Carnac reports Ctrl, Alt, Shift and Win through the modifiers of the key that is
        // pressed with them and never as a key press of their own (KeyProvider filters these events), so an entry
        // ending in one of them could never match.
        static readonly HashSet<Keys> ModifierKeys = new HashSet<Keys>
        {
            Keys.ControlKey, Keys.LControlKey, Keys.RControlKey,
            Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey,
            Keys.Menu, Keys.LMenu, Keys.RMenu,
            Keys.LWin, Keys.RWin
        };

        readonly Action<string> warn;

        public KeyCombinationParser() : this(null)
        {
        }

        /// <param name="warningSink">Receives one message per rejected text; null selects <see cref="TraceWarning"/>.</param>
        public KeyCombinationParser(Action<string> warningSink)
        {
            warn = warningSink ?? TraceWarning;
        }

        /// <summary>Default warning sink: writes to the trace listeners (and so to the debugger output).</summary>
        public static void TraceWarning(string message)
        {
            Trace.TraceWarning("Carnac: " + message);
        }

        /// <summary>
        /// Parses a comma separated sequence of combinations. On failure warns (prefixed with <paramref name="context"/>,
        /// for example the keymap file and shortcut name) and returns null.
        /// </summary>
        public IList<KeyPressDefinition> ParseSequence(string text, string context)
        {
            IList<KeyPressDefinition> sequence;
            string error;
            if (TryParseSequence(text, out sequence, out error))
                return sequence;

            Warn(context, text, error);
            return null;
        }

        /// <summary>
        /// Parses a single combination. On failure warns (prefixed with <paramref name="context"/>) and returns null.
        /// </summary>
        public KeyPressDefinition ParseCombination(string text, string context)
        {
            KeyPressDefinition definition;
            string error;
            if (TryParse(text, out definition, out error))
                return definition;

            Warn(context, text, error);
            return null;
        }

        void Warn(string context, string text, string error)
        {
            var prefix = string.IsNullOrEmpty(context) ? string.Empty : context + ": ";
            warn(prefix + "cannot use key entry '" + text + "': " + error);
        }

        /// <summary>Parses one combination such as <c>Ctrl+Shift+N</c>.</summary>
        public static bool TryParse(string text, out KeyPressDefinition definition, out string error)
        {
            definition = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "the entry is empty";
                return false;
            }

            var combination = text.Trim();
            string modifierText;
            string keyText;
            if (combination.EndsWith("+", StringComparison.Ordinal))
            {
                // Only "+" itself ("+", "Ctrl++") can end with a plus; it is the plus key, not a separator.
                if (combination.Length == 1)
                {
                    modifierText = string.Empty;
                }
                else if (combination[combination.Length - 2] == '+')
                {
                    modifierText = combination.Substring(0, combination.Length - 2);
                    if (modifierText.Trim().Length == 0)
                    {
                        error = "a modifier is missing before the first '+' (the plus key on its own is written '+')";
                        return false;
                    }
                }
                else
                {
                    error = "a key is missing after the last '+' (write the plus key itself as 'Ctrl++')";
                    return false;
                }
                keyText = "+";
            }
            else
            {
                var lastPlus = combination.LastIndexOf('+');
                modifierText = lastPlus < 0 ? string.Empty : combination.Substring(0, lastPlus);
                keyText = combination.Substring(lastPlus + 1).Trim();
                if (lastPlus >= 0 && modifierText.Trim().Length == 0)
                {
                    error = "a modifier is missing before the first '+'";
                    return false;
                }
            }

            bool ctrl = false, alt = false, shift = false, win = false;
            if (modifierText.Trim().Length > 0)
            {
                foreach (var rawModifier in modifierText.Split('+'))
                {
                    var modifier = rawModifier.Trim();
                    bool alreadySet;
                    switch (modifier.ToLowerInvariant())
                    {
                        case "ctrl":
                        case "control":
                            alreadySet = ctrl;
                            ctrl = true;
                            break;
                        case "alt":
                            alreadySet = alt;
                            alt = true;
                            break;
                        case "shift":
                            alreadySet = shift;
                            shift = true;
                            break;
                        case "win":
                        case "windows":
                            alreadySet = win;
                            win = true;
                            break;
                        default:
                            error = modifier.Length == 0
                                ? "an empty modifier between two '+'"
                                : "'" + modifier + "' is not a modifier (use Ctrl, Alt, Shift or Win); only the last part may be a key";
                            return false;
                    }
                    if (alreadySet)
                    {
                        error = "the modifier '" + modifier + "' is used twice";
                        return false;
                    }
                }
            }

            Keys key;
            bool impliesShift;
            if (!TryResolveKey(keyText, out key, out impliesShift, out error))
                return false;

            definition = new KeyPressDefinition(key, winkeyPressed: win, shiftPressed: shift || impliesShift, altPressed: alt, controlPressed: ctrl);
            return true;
        }

        /// <summary>Parses a comma separated sequence of combinations such as <c>Ctrl+K,Ctrl+C</c>.</summary>
        public static bool TryParseSequence(string text, out IList<KeyPressDefinition> sequence, out string error)
        {
            sequence = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "the entry is empty";
                return false;
            }

            var parts = new List<string>(SplitSequence(text));
            var result = new List<KeyPressDefinition>();
            foreach (var part in parts)
            {
                KeyPressDefinition definition;
                if (!TryParse(part, out definition, out error))
                {
                    if (parts.Count > 1)
                        error = "in '" + part.Trim() + "': " + error;
                    return false;
                }
                result.Add(definition);
            }

            sequence = result;
            return true;
        }

        /// <summary>
        /// Splits a sequence on commas. A comma that directly follows a '+' waiting for its key is the comma key and
        /// stays part of the combination ("Ctrl+K,Ctrl+," is Ctrl+K followed by Ctrl and the comma key), while the
        /// comma after the plus key itself separates ("Ctrl++,A" is Ctrl and the plus key followed by A).
        /// </summary>
        static IEnumerable<string> SplitSequence(string text)
        {
            var current = new StringBuilder();
            foreach (var c in text)
            {
                if (c == ',' && !IsCommaKey(current))
                {
                    yield return current.ToString();
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            yield return current.ToString();
        }

        // A comma is the comma key only when the text so far ends in a '+' that still waits for its key ("Ctrl+" then ',').
        // A '+' that follows another '+' ("Ctrl++") or stands alone ("+") is already the plus key, so the comma separates.
        static bool IsCommaKey(StringBuilder currentCombination)
        {
            var i = currentCombination.Length - 1;
            while (i >= 0 && char.IsWhiteSpace(currentCombination[i]))
                i--;
            if (i < 0 || currentCombination[i] != '+')
                return false;

            i--;
            while (i >= 0 && char.IsWhiteSpace(currentCombination[i]))
                i--;
            return i >= 0 && currentCombination[i] != '+';
        }

        static bool TryResolveKey(string keyText, out Keys key, out bool impliesShift, out string error)
        {
            key = Keys.None;
            impliesShift = false;
            error = null;

            if (string.IsNullOrEmpty(keyText))
            {
                error = "the key is missing";
                return false;
            }

            // Enum parsing treats commas as flag separators and ORs the values ("A,B" would become the key C). Commas belong
            // between the steps of a chord, which TryParseSequence splits before it gets here; only the comma key is a comma.
            if (keyText.IndexOf(',') >= 0 && keyText != ",")
            {
                error = "a key cannot contain a comma; commas only separate the steps of a chord (Ctrl+K,Ctrl+C) and the comma key is written Ctrl+, or Oemcomma";
                return false;
            }

            // Enum parsing would read "1" as the numeric key code 1 (the left mouse button) instead of the '1' key.
            if (keyText.Length == 1 && keyText[0] >= '0' && keyText[0] <= '9')
            {
                key = Keys.D0 + (keyText[0] - '0');
                return true;
            }

            switch (keyText.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                case "alt":
                case "shift":
                case "win":
                case "windows":
                    error = "'" + keyText + "' is a modifier, not a key; add the key after it (for example 'Ctrl+S')";
                    return false;
            }

            var isNumber = true;
            foreach (var c in keyText)
                isNumber &= char.IsDigit(c);
            var resolved = isNumber ? null : ReplaceKey.ToKey(keyText);
            if (resolved == null)
            {
                string suggestion;
                error = "unknown key '" + keyText + "'" + (KeyNameHints.TryGetValue(keyText, out suggestion)
                    ? " (did you mean '" + suggestion + "'?)"
                    : " (use a name such as Enter, Up, F5, PageDown, or a single character)");
                return false;
            }

            key = resolved.Value;
            impliesShift = ReplaceKey.IsShiftedCharacter(keyText);
            if (key == Keys.None || (key & Keys.Modifiers) != Keys.None || ModifierKeys.Contains(key))
            {
                error = "'" + keyText + "' is a modifier, not a key; add the key after it (for example 'Ctrl+S')";
                return false;
            }
            return true;
        }
    }
}
