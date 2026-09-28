using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Markup;
using System.Windows.Media;

namespace Carnac.UI
{
    /// <summary>
    /// An entry of the font family drop-down of the Appearance tab.
    /// </summary>
    public class FontFamilyOption
    {
        public FontFamilyOption(string name, string displayName, FontFamily family)
        {
            Name = name;
            DisplayName = displayName;
            Family = family;
        }

        /// <summary>The value stored in <c>PopupSettings.FontFamily</c>. Empty for the system default font.</summary>
        public string Name { get; private set; }

        /// <summary>The text of the entry: the name of the family in the language of the user interface if it has one.</summary>
        public string DisplayName { get; private set; }

        /// <summary>The font the entry is rendered in.</summary>
        public FontFamily Family { get; private set; }

        /// <summary>
        /// Creates the entries of the drop-down: the system default first, then every family sorted by its display name.
        /// </summary>
        public static IList<FontFamilyOption> CreateList(IEnumerable<FontFamily> installedFamilies, FontFamily systemDefaultFamily,
            string systemDefaultDisplayName, CultureInfo uiCulture)
        {
            if (installedFamilies == null)
                throw new ArgumentNullException("installedFamilies");
            if (systemDefaultFamily == null)
                throw new ArgumentNullException("systemDefaultFamily");
            if (uiCulture == null)
                throw new ArgumentNullException("uiCulture");

            var language = XmlLanguage.GetLanguage(uiCulture.IetfLanguageTag);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var options = new List<FontFamilyOption>();

            foreach (var family in installedFamilies)
            {
                if (string.IsNullOrWhiteSpace(family.Source) || !names.Add(family.Source))
                    continue;

                string displayName;
                if (!family.FamilyNames.TryGetValue(language, out displayName) || string.IsNullOrWhiteSpace(displayName))
                    displayName = family.Source;

                options.Add(new FontFamilyOption(family.Source, displayName, family));
            }

            options.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            options.Insert(0, new FontFamilyOption(string.Empty, systemDefaultDisplayName, systemDefaultFamily));

            return options;
        }
    }
}
