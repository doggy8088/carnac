using System;
using System.Collections.Generic;
using System.Linq;

namespace Carnac.Logic
{
    /// <summary>
    /// The keys the user asked Carnac to never show ("W,A,S,D", "Ctrl+Alt+Delete"). The text is the
    /// <see cref="Models.PopupSettings.IgnoredKeys"/> setting; it is parsed again only when it changes, so it can be
    /// edited while Carnac runs. Entries use the keymap key syntax and must match the modifiers exactly: "W" does not
    /// ignore Ctrl+W or Shift+W.
    /// </summary>
    public class KeyIgnoreList
    {
        static readonly KeyPressDefinition[] Nothing = new KeyPressDefinition[0];

        readonly object gate = new object();
        readonly KeyCombinationParser parser;
        string parsedText;
        KeyPressDefinition[] ignored = Nothing;

        /// <param name="warn">Receives a message for every entry that is not understood; null writes to the trace listeners.</param>
        public KeyIgnoreList(Action<string> warn = null)
        {
            parser = new KeyCombinationParser(warn);
        }

        /// <summary>True when <paramref name="key"/> (key and modifiers) is listed in <paramref name="ignoredKeys"/>.</summary>
        public bool IsIgnored(string ignoredKeys, KeyPressDefinition key)
        {
            if (key == null)
                throw new ArgumentNullException("key");

            KeyPressDefinition[] current;
            lock (gate)
            {
                if (!string.Equals(parsedText, ignoredKeys, StringComparison.Ordinal))
                {
                    parsedText = ignoredKeys;
                    ignored = string.IsNullOrWhiteSpace(ignoredKeys)
                        ? Nothing
                        : parser.ParseList(ignoredKeys, "Ignored keys").ToArray();
                }
                current = ignored;
            }

            // Compare as the base type: only key and modifiers count, not the process or the text of a KeyPress.
            return current.Any(definition => definition.Equals(key));
        }
    }
}
