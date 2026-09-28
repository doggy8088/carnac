using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Carnac.Utilities
{
    /// <summary>
    /// The names of the font families that are installed on this machine.
    /// </summary>
    public static class InstalledFonts
    {
        // Reading the system font collection takes a moment, so it is done once and only when a named font has to be checked.
        static readonly Lazy<HashSet<string>> Names = new Lazy<HashSet<string>>(ReadNames);

        /// <summary>
        /// True if a font family with this name (in any of its localized names, ignoring case) is installed.
        /// </summary>
        public static bool Contains(string familyName)
        {
            return !string.IsNullOrWhiteSpace(familyName) && Names.Value.Contains(familyName.Trim());
        }

        static HashSet<string> ReadNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var family in Fonts.SystemFontFamilies)
            {
                names.Add(family.Source);
                foreach (var localizedName in family.FamilyNames.Values)
                {
                    names.Add(localizedName);
                }
            }

            return names;
        }
    }
}
