using System;
using System.Globalization;
using System.Linq;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class UiLanguagesFacts
    {
        static string Resolve(string configuredLanguage, string windowsCulture)
        {
            return UiLanguages.Resolve(configuredLanguage, CultureInfo.GetCultureInfo(windowsCulture)).Name;
        }

        [Fact]
        public void ConfiguredLanguageWinsOverTheLanguageOfWindows()
        {
            Assert.Equal("zh-CN", Resolve("zh-CN", "en-US"));
            Assert.Equal("zh-TW", Resolve("zh-TW", "de-DE"));
            Assert.Equal("en", Resolve("en", "zh-TW"));
        }

        [Fact]
        public void ConfiguredLanguageIgnoresCase()
        {
            Assert.Equal("zh-TW", Resolve("ZH-tw", "en-US"));
            Assert.Equal("en", Resolve("EN", "zh-CN"));
        }

        [Fact]
        public void EmptyLanguageFollowsWindows()
        {
            Assert.Equal("de-DE", Resolve(UiLanguages.SystemDefault, "de-DE"));
            Assert.Equal("fr-FR", Resolve(null, "fr-FR"));
            Assert.Equal("zh-TW", Resolve(string.Empty, "zh-TW"));
        }

        [Fact]
        public void LanguageThatIsNotOfferedFollowsWindows()
        {
            // For example a hand-edited settings file.
            Assert.Equal("de-DE", Resolve("klingon", "de-DE"));
            Assert.Equal("zh-CN", Resolve("ja-JP", "zh-CN"));
        }

        [Fact]
        public void TraditionalChineseVariantsUseTheTraditionalTranslation()
        {
            Assert.Equal("zh-TW", Resolve(null, "zh-TW"));
            Assert.Equal("zh-TW", Resolve(null, "zh-HK"));
            Assert.Equal("zh-TW", Resolve(null, "zh-MO"));
            Assert.Equal("zh-TW", Resolve(null, "zh-CHT"));
        }

        [Fact]
        public void SimplifiedChineseVariantsUseTheSimplifiedTranslation()
        {
            Assert.Equal("zh-CN", Resolve(null, "zh-CN"));
            Assert.Equal("zh-CN", Resolve(null, "zh-SG"));
            Assert.Equal("zh-CN", Resolve(null, "zh-CHS"));
            Assert.Equal("zh-CN", Resolve(null, "zh"));
        }

        [Fact]
        public void OtherLanguagesAreKept()
        {
            Assert.Equal("en-US", Resolve(null, "en-US"));
            Assert.Equal("de-AT", Resolve(null, "de-AT"));
            Assert.Equal("ja-JP", Resolve(null, "ja-JP"));
            Assert.Equal(string.Empty, UiLanguages.Resolve(null, CultureInfo.InvariantCulture).Name);
        }

        [Fact]
        public void LanguageOfWindowsIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => UiLanguages.Resolve("en", null));
        }

        [Fact]
        public void OfferedLanguagesAreEnglishAndTheTranslations()
        {
            Assert.Equal(new[] { "en", "zh-TW", "zh-CN" }, UiLanguages.Codes.ToArray());
            Assert.Equal(UiLanguages.Codes.Count, UiLanguages.Codes.Select(c => CultureInfo.GetCultureInfo(c).Name).Distinct().Count());
        }
    }
}
