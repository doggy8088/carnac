using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Carnac.Logic;

namespace Carnac.UI
{
    /// <summary>
    /// An entry of the language drop-down of the General tab.
    /// </summary>
    public class LanguageOption
    {
        public LanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        /// <summary>The value stored in <c>PopupSettings.Language</c>. Empty follows the language of Windows.</summary>
        public string Code { get; private set; }

        public string DisplayName { get; private set; }

        /// <summary>
        /// The entries of the drop-down: follow Windows first, then every language of <see cref="UiLanguages.Codes"/>.
        /// </summary>
        public static IList<LanguageOption> CreateList()
        {
            var options = new List<LanguageOption>
            {
                new LanguageOption(UiLanguages.SystemDefault, Properties.Resources.Preferences_LanguageSystemDefault)
            };
            options.AddRange(UiLanguages.Codes.Select(code => new LanguageOption(code, GetNativeName(code))));
            return options;
        }

        // Every language is listed in its own language, so that it can be found when the current one is not understood.
        static string GetNativeName(string code)
        {
            switch (code)
            {
                case "en":
                    return "English";
                case "zh-TW":
                    return "繁體中文"; // Traditional Chinese
                case "zh-CN":
                    return "简体中文"; // Simplified Chinese
                default:
                    return CultureInfo.GetCultureInfo(code).NativeName;
            }
        }
    }
}
