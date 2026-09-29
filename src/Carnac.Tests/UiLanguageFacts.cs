using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Carnac.Logic;
using Carnac.Logic.Models;
using Carnac.Properties;
using Carnac.UI;
using Carnac.Utilities;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class UiLanguageFacts
    {
        static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        /// <summary>The language is process wide state: restore it so that other tests are not affected.</summary>
        static void WithRestoredLanguage(Action test)
        {
            var resourcesCulture = Resources.Culture;
            var keyLabelsCulture = KeyLabels.Culture;
            var defaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
            var threadCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                test();
            }
            finally
            {
                Resources.Culture = resourcesCulture;
                KeyLabels.Culture = keyLabelsCulture;
                CultureInfo.DefaultThreadCurrentUICulture = defaultCulture;
                Thread.CurrentThread.CurrentUICulture = threadCulture;
            }
        }

        static PreferencesViewModel CreatePreferencesViewModel(ISettingsProvider settingsProvider)
        {
            var screenManager = Substitute.For<IScreenManager>();
            return new PreferencesViewModel(settingsProvider, screenManager);
        }

        [Fact]
        public void ApplyingTraditionalChineseTranslatesTheResources()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("zh-TW", English);

                Assert.Equal("zh-TW", Resources.Culture.Name);
                Assert.Equal("一般", Resources.Preferences_TabGeneral);
                Assert.Equal("結束", Resources.ShellView_Exit);
            });
        }

        [Fact]
        public void ApplyingSimplifiedChineseTranslatesTheResources()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("zh-CN", English);

                Assert.Equal("常规", Resources.Preferences_TabGeneral);
                Assert.Equal("退出", Resources.ShellView_Exit);
            });
        }

        [Fact]
        public void EmptyLanguageFollowsWindowsAndUnsupportedLanguagesUseEnglish()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply(string.Empty, CultureInfo.GetCultureInfo("zh-HK"));
                Assert.Equal("zh-TW", Resources.Culture.Name);
                Assert.Equal("一般", Resources.Preferences_TabGeneral);

                UiLanguage.Apply(string.Empty, CultureInfo.GetCultureInfo("fr-FR"));
                Assert.Equal("fr-FR", Resources.Culture.Name);
                Assert.Equal("General", Resources.Preferences_TabGeneral);
            });
        }

        [Fact]
        public void EnglishCanBeChosenOnAChineseSystem()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("en", CultureInfo.GetCultureInfo("zh-TW"));

                Assert.Equal("General", Resources.Preferences_TabGeneral);
                Assert.Equal("Exit", Resources.ShellView_Exit);
            });
        }

        [Fact]
        public void ApplyingALanguageSetsTheCultureOfTheKeyLabelsAndThreads()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply(string.Empty, CultureInfo.GetCultureInfo("de-DE"));

                Assert.Equal("de-DE", KeyLabels.Culture.Name);
                Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentUICulture.Name);
                Assert.Equal("de-DE", Thread.CurrentThread.CurrentUICulture.Name);
                Assert.Equal("Strg", KeyLabels.Localize("Ctrl", KeyLabels.Culture));

                UiLanguage.Apply("en", CultureInfo.GetCultureInfo("de-DE"));
                Assert.Equal("Ctrl", KeyLabels.Localize("Ctrl", KeyLabels.Culture));
            });
        }

        [Fact]
        public void TheResourceCultureAppliesToOtherThreadsToo()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("zh-CN", English);
                string onOtherThread = null;
                var thread = new Thread(() => onOtherThread = Resources.Preferences_TabGeneral);
                thread.Start();
                thread.Join();

                Assert.Equal("常规", onOtherThread);
            });
        }

        [Fact]
        public void FollowingTheSettingAppliesItAtOnceAndWhenItChanges()
        {
            WithRestoredLanguage(() =>
            {
                var settings = new PopupSettings { Language = "zh-TW" };
                var changes = 0;

                UiLanguage.Follow(settings, () => changes++);

                Assert.Equal("一般", Resources.Preferences_TabGeneral);
                Assert.Equal(0, changes);

                settings.Language = "zh-CN";
                Assert.Equal("常规", Resources.Preferences_TabGeneral);
                Assert.Equal(1, changes);

                settings.Language = "en";
                Assert.Equal("General", Resources.Preferences_TabGeneral);
                Assert.Equal(2, changes);
            });
        }

        [Fact]
        public void OtherSettingsDoNotChangeTheLanguage()
        {
            WithRestoredLanguage(() =>
            {
                var settings = new PopupSettings { Language = "zh-TW" };
                var changes = 0;
                UiLanguage.Follow(settings, () => changes++);

                settings.ItemOpacity = 0.3;
                settings.FontSize = 12;

                Assert.Equal(0, changes);
                Assert.Equal("zh-TW", Resources.Culture.Name);
            });
        }

        [Fact]
        public void FollowingWithoutACallbackIsAllowed()
        {
            WithRestoredLanguage(() =>
            {
                var settings = new PopupSettings { Language = "en" };
                UiLanguage.Follow(settings, null);

                settings.Language = "zh-TW";

                Assert.Equal("一般", Resources.Preferences_TabGeneral);
            });
        }

        [Fact]
        public void FollowingRequiresTheSettings()
        {
            Assert.Throws<ArgumentNullException>(() => UiLanguage.Follow(null, null));
        }

        [Fact]
        public void LanguageOfANewSettingsFileFollowsWindows()
        {
            var settings = new SettingsProvider(new InMemorySettingsStorage()).GetSettings<PopupSettings>();

            Assert.Equal(UiLanguages.SystemDefault, settings.Language);
        }

        [Fact]
        public void LanguageIsPersistedAndResetToDefaultsFollowsWindowsAgain()
        {
            WithRestoredLanguage(() =>
            {
                var storage = new InMemorySettingsStorage();
                var provider = new SettingsProvider(storage);
                var preferences = CreatePreferencesViewModel(provider);
                var appliedLanguages = 0;
                UiLanguage.Follow(preferences.Settings, () => appliedLanguages++);

                preferences.Settings.Language = "zh-TW";
                provider.SaveSettings(preferences.Settings);
                Assert.Equal("zh-TW", new SettingsProvider(storage).GetSettings<PopupSettings>().Language);
                Assert.Equal("zh-TW", Resources.Culture.Name);

                preferences.ResetToDefaultsCommand.Execute(null);

                Assert.Equal(UiLanguages.SystemDefault, preferences.Settings.Language);
                Assert.Equal(UiLanguages.SystemDefault, new SettingsProvider(storage).GetSettings<PopupSettings>().Language);
                Assert.Equal(2, appliedLanguages);
            });
        }

        [Fact]
        public void PreferencesOfferFollowingWindowsAndEveryLanguage()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("zh-CN", English);

                var languages = CreatePreferencesViewModel(new SettingsProvider(new InMemorySettingsStorage())).AvailableLanguages;

                Assert.Equal(new[] { UiLanguages.SystemDefault }.Concat(UiLanguages.Codes).ToArray(), languages.Select(l => l.Code).ToArray());
                Assert.Equal("系统默认值", languages.First().DisplayName);
                Assert.Equal("English", languages.Single(l => l.Code == "en").DisplayName);
                Assert.Equal("繁體中文", languages.Single(l => l.Code == "zh-TW").DisplayName);
                Assert.Equal("简体中文", languages.Single(l => l.Code == "zh-CN").DisplayName);
            });
        }

        [Fact]
        public void PreferencesNameTheSystemDefaultFontInTheCurrentLanguage()
        {
            WithRestoredLanguage(() =>
            {
                UiLanguage.Apply("zh-TW", English);

                var fonts = CreatePreferencesViewModel(new SettingsProvider(new InMemorySettingsStorage())).AvailableFontFamilies;

                Assert.Equal("系統預設字型", fonts.First().DisplayName);
            });
        }
    }
}
