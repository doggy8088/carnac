using System;
using System.Collections.Generic;
using System.Globalization;

namespace Carnac.Logic
{
    /// <summary>
    /// Localizes the labels of keys when they are displayed. The keymaps and the messages keep the canonical English
    /// names (for example "Ctrl"); only <see cref="Models.KeyPress.GetTextParts()"/> translates them, and only for languages
    /// that have entries below. Add a language by adding its two-letter ISO code with the labels that differ.
    /// </summary>
    public static class KeyLabels
    {
        static readonly Dictionary<string, Dictionary<string, string>> Translations =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "de", new Dictionary<string, string>
                    {
                        { "Ctrl", "Strg" },
                        { "Delete", "Entf" },
                        { "Insert", "Einfg" },
                        { "Home", "Pos1" },
                        { "End", "Ende" },
                        { "PageUp", "Bild↑" },
                        { "PageDown", "Bild↓" },
                        { "Escape", "Esc" },
                        { "Space", "Leertaste" }
                    }
                }
            };

        static volatile CultureInfo culture;

        /// <summary>
        /// The language of the labels that are displayed. Null (the default) displays the canonical English names.
        /// The application sets it to the user interface language it uses.
        /// </summary>
        public static CultureInfo Culture
        {
            get { return culture; }
            set { culture = value; }
        }

        /// <summary>
        /// Returns the label for the canonical key name in the language of the culture, or the name itself when the
        /// language has no entry for it.
        /// </summary>
        public static string Localize(string canonicalName, CultureInfo uiCulture)
        {
            if (canonicalName == null || uiCulture == null)
                return canonicalName;

            Dictionary<string, string> labels;
            string label;
            return Translations.TryGetValue(uiCulture.TwoLetterISOLanguageName, out labels) && labels.TryGetValue(canonicalName, out label)
                ? label
                : canonicalName;
        }
    }
}
