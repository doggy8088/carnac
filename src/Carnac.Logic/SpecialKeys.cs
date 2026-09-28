using System;
using System.Collections.Generic;

namespace Carnac.Logic
{
    /// <summary>
    /// The keys which are drawn as a key cap in the overlay instead of as plain text.
    /// The names are the strings the key stream produces for these keys (see <see cref="ReplaceKey.Sanitise"/>);
    /// keys whose enum members share a value (CapsLock/Capital, PrintScreen/Snapshot, ...) are pinned there.
    /// </summary>
    public static class SpecialKeys
    {
        static readonly HashSet<string> Names = CreateNames();

        static HashSet<string> CreateNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                "Alt", "Ctrl", "Shift",
                "Back", "Escape", "Tab",
                "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
                "CapsLock", "NumLock", "ScrollLock", "PrintScreen", "Pause", "Apps"
            };

            for (var i = 1; i <= 24; i++)
                names.Add("F" + i);

            return names;
        }

        public static bool IsSpecialKey(string text)
        {
            return text != null && Names.Contains(text);
        }
    }
}
