using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Carnac.Logic
{
    /// <summary>
    /// The languages the user interface is translated into and how the language to use is chosen.
    /// </summary>
    public static class UiLanguages
    {
        /// <summary>The value of the language setting that follows the display language of Windows.</summary>
        public const string SystemDefault = "";

        /// <summary>
        /// The languages that can be chosen: "en" is the neutral (English) resources, every other code needs a
        /// <c>Resources.&lt;code&gt;.resx</c> next to <c>Resources.resx</c>. See CONTRIBUTING.md.
        /// </summary>
        public static readonly ReadOnlyCollection<string> Codes = new ReadOnlyCollection<string>(new[] { "en", "zh-TW", "zh-CN" });

        /// <summary>
        /// Returns the culture the user interface has to use.
        /// </summary>
        /// <param name="configuredLanguage">The language setting: one of <see cref="Codes"/> (ignoring case), or anything else
        /// (normally <see cref="SystemDefault"/>) to follow Windows.</param>
        /// <param name="systemUiCulture">The display language of Windows.</param>
        /// <remarks>
        /// Chinese variants without their own translation use the closest one, for example Hong Kong the Traditional Chinese
        /// translation. Other cultures are returned unchanged: their resources fall back to English, while the key labels
        /// can still be localized for them (see <see cref="KeyLabels"/>).
        /// </remarks>
        public static CultureInfo Resolve(string configuredLanguage, CultureInfo systemUiCulture)
        {
            if (systemUiCulture == null)
                throw new ArgumentNullException("systemUiCulture");

            var code = Codes.FirstOrDefault(c => string.Equals(c, configuredLanguage, StringComparison.OrdinalIgnoreCase));
            if (code != null)
                return CultureInfo.GetCultureInfo(code);

            return MapChinese(systemUiCulture);
        }

        static CultureInfo MapChinese(CultureInfo culture)
        {
            var name = culture.Name;
            if (!name.Equals("zh", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("zh-", StringComparison.OrdinalIgnoreCase))
                return culture;

            return CultureInfo.GetCultureInfo(IsTraditionalChinese(name) ? "zh-TW" : "zh-CN");
        }

        static bool IsTraditionalChinese(string cultureName)
        {
            return cultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
                   || cultureName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
                   || cultureName.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
                   || cultureName.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
                   || cultureName.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase);
        }
    }
}
