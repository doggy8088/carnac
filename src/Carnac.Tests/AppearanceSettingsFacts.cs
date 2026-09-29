using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Carnac.Logic;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.UI;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class AppearanceSettingsFacts
    {
        readonly InMemorySettingsStorage storage = new InMemorySettingsStorage();

        SettingsProvider CreateProvider()
        {
            return new SettingsProvider(storage);
        }

        static PreferencesViewModel CreatePreferencesViewModel(ISettingsProvider settingsProvider)
        {
            var screenManager = Substitute.For<IScreenManager>();
            screenManager.GetScreens().Returns(new[] { new DetailedScreen { Index = 1, Width = 1920, Height = 1080 } });
            return new PreferencesViewModel(settingsProvider, screenManager);
        }

        [Fact]
        public void DefaultsReproduceTheCurrentLookOfThePopup()
        {
            var settings = CreateProvider().GetSettings<PopupSettings>();

            // Empty is the system font, which is what the popup used before there was a setting.
            Assert.Equal(string.Empty, settings.FontFamily);
            Assert.Equal(15, settings.ItemCornerRadius);
            Assert.Equal(3, settings.ItemPadding);
            Assert.Equal(new CornerRadius(15), settings.ItemBorderCornerRadius);
            Assert.Equal(new Thickness(3), settings.ItemBorderPadding);
        }

        [Fact]
        public void SettingsFileWrittenByAnOlderVersionGetsTheDefaults()
        {
            var provider = CreateProvider();
            var settings = provider.GetSettings<PopupSettings>();
            settings.ItemMaxWidth = 123;
            provider.SaveSettings(settings);
            storage.Remove("FontFamily");
            storage.Remove("ItemCornerRadius");
            storage.Remove("ItemPadding");

            var loaded = CreateProvider().GetSettings<PopupSettings>();

            Assert.Equal(123, loaded.ItemMaxWidth);
            Assert.Equal(string.Empty, loaded.FontFamily);
            Assert.Equal(15, loaded.ItemCornerRadius);
            Assert.Equal(3, loaded.ItemPadding);
        }

        [Fact]
        public void FontFamilyCornerRadiusAndPaddingArePersisted()
        {
            var provider = CreateProvider();
            var settings = provider.GetSettings<PopupSettings>();
            settings.FontFamily = "Consolas";
            settings.ItemCornerRadius = 4.5;
            settings.ItemPadding = 12;
            provider.SaveSettings(settings);

            var loaded = CreateProvider().GetSettings<PopupSettings>();

            Assert.Equal("Consolas", loaded.FontFamily);
            Assert.Equal(4.5, loaded.ItemCornerRadius);
            Assert.Equal(12, loaded.ItemPadding);
        }

        [Fact]
        public void ResetToDefaultsInPreferencesRestoresFontFamilyCornerRadiusAndPadding()
        {
            var provider = CreateProvider();
            var preferences = CreatePreferencesViewModel(provider);
            preferences.Settings.FontFamily = "Consolas";
            preferences.Settings.ItemCornerRadius = 30;
            preferences.Settings.ItemPadding = 10;
            preferences.SaveCommand.Execute(null);

            preferences.ResetToDefaultsCommand.Execute(null);

            Assert.Equal(string.Empty, preferences.Settings.FontFamily);
            Assert.Equal(15, preferences.Settings.ItemCornerRadius);
            Assert.Equal(3, preferences.Settings.ItemPadding);

            var reloaded = CreateProvider().GetSettings<PopupSettings>();
            Assert.Equal(string.Empty, reloaded.FontFamily);
            Assert.Equal(15, reloaded.ItemCornerRadius);
            Assert.Equal(3, reloaded.ItemPadding);
        }

        [Fact]
        public void CornerRadiusOfThePopupIsLimitedToTheSliderRange()
        {
            var settings = new PopupSettings();

            settings.ItemCornerRadius = 20;
            Assert.Equal(new CornerRadius(20), settings.ItemBorderCornerRadius);
            settings.ItemCornerRadius = -5;
            Assert.Equal(new CornerRadius(0), settings.ItemBorderCornerRadius);
            settings.ItemCornerRadius = double.NaN;
            Assert.Equal(new CornerRadius(0), settings.ItemBorderCornerRadius);
            settings.ItemCornerRadius = 1000;
            Assert.Equal(new CornerRadius(PopupSettings.MaxItemCornerRadius), settings.ItemBorderCornerRadius);
            settings.ItemCornerRadius = double.PositiveInfinity;
            Assert.Equal(new CornerRadius(PopupSettings.MaxItemCornerRadius), settings.ItemBorderCornerRadius);
        }

        [Fact]
        public void PaddingOfThePopupIsLimitedToTheSliderRange()
        {
            var settings = new PopupSettings();

            settings.ItemPadding = 8;
            Assert.Equal(new Thickness(8), settings.ItemBorderPadding);
            settings.ItemPadding = -1;
            Assert.Equal(new Thickness(0), settings.ItemBorderPadding);
            settings.ItemPadding = double.NaN;
            Assert.Equal(new Thickness(0), settings.ItemBorderPadding);
            settings.ItemPadding = 1000;
            Assert.Equal(new Thickness(PopupSettings.MaxItemPadding), settings.ItemBorderPadding);
        }

        [Fact]
        public void ChangingTheCornerRadiusNotifiesTheBoundBorderProperty()
        {
            var settings = new PopupSettings();
            var changed = new List<string>();
            settings.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            settings.ItemCornerRadius = 20;

            Assert.Contains("ItemCornerRadius", changed);
            Assert.Contains("ItemBorderCornerRadius", changed);
        }

        [Fact]
        public void ChangingThePaddingNotifiesTheBoundBorderProperty()
        {
            var settings = new PopupSettings();
            var changed = new List<string>();
            settings.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            settings.ItemPadding = 9;

            Assert.Contains("ItemPadding", changed);
            Assert.Contains("ItemBorderPadding", changed);
        }

        [Fact]
        public void ChangingTheFontFamilyNotifiesTheOverlay()
        {
            var settings = new PopupSettings();
            var changed = new List<string>();
            settings.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            settings.FontFamily = "Consolas";

            Assert.Contains("FontFamily", changed);
        }

        [Fact]
        public void SliderLimitsOfTheAppearanceTabMatchTheLimitsOfTheSettings()
        {
            Assert.Equal(50, PopupSettings.MaxItemCornerRadius);
            Assert.Equal(30, PopupSettings.MaxItemPadding);
        }

        [Fact]
        public void PreferencesOfferTheSystemDefaultFirstAndThenTheInstalledFonts()
        {
            var preferences = CreatePreferencesViewModel(CreateProvider());

            var fonts = preferences.AvailableFontFamilies;

            Assert.Equal(string.Empty, fonts.First().Name);
            Assert.Equal(SystemFonts.MessageFontFamily.Source, fonts.First().Family.Source);
            Assert.Equal(Fonts.SystemFontFamilies.Select(f => f.Source).Distinct(StringComparer.OrdinalIgnoreCase).Count(), fonts.Count - 1);
        }

        [Fact]
        public void EveryFontOfThePreferencesListRoundTripsThroughTheConverterOfTheOverlay()
        {
            var preferences = CreatePreferencesViewModel(CreateProvider());

            foreach (var font in preferences.AvailableFontFamilies.Skip(1))
            {
                var family = (FontFamily)Carnac.Utilities.FontFamilyNameConverter.Instance
                    .Convert(font.Name, typeof(FontFamily), null, CultureInfo.InvariantCulture);

                Assert.Equal(font.Name, family.Source);
            }
        }

        [Fact]
        public void FontListIsSortedByDisplayNameIgnoringCaseWithoutDuplicates()
        {
            var families = new[]
            {
                new FontFamily("Verdana"), new FontFamily("arial"), new FontFamily("Consolas"), new FontFamily("ARIAL")
            };

            var options = FontFamilyOption.CreateList(families, new FontFamily("Segoe UI"), "System default", CultureInfo.InvariantCulture);

            Assert.Equal(new[] { string.Empty, "arial", "Consolas", "Verdana" }, options.Select(o => o.Name).ToArray());
        }

        [Fact]
        public void SystemDefaultEntryUsesTheGivenTextAndFont()
        {
            var systemFont = new FontFamily("Segoe UI");

            var options = FontFamilyOption.CreateList(new FontFamily[0], systemFont, "System default", CultureInfo.InvariantCulture);

            var entry = Assert.Single(options);
            Assert.Equal(string.Empty, entry.Name);
            Assert.Equal("System default", entry.DisplayName);
            Assert.Same(systemFont, entry.Family);
        }

        [Fact]
        public void FontWithoutALocalizedNameIsShownWithItsFamilyName()
        {
            var options = FontFamilyOption.CreateList(
                new[] { new FontFamily("No Such Font (Carnac test)") }, new FontFamily("Segoe UI"), "System default", CultureInfo.InvariantCulture);

            Assert.Equal("No Such Font (Carnac test)", options[1].DisplayName);
            Assert.Equal("No Such Font (Carnac test)", options[1].Name);
        }

        [Fact]
        public void FontListRequiresItsArguments()
        {
            var font = new FontFamily("Segoe UI");

            Assert.Throws<ArgumentNullException>(() => FontFamilyOption.CreateList(null, font, "System default", CultureInfo.InvariantCulture));
            Assert.Throws<ArgumentNullException>(() => FontFamilyOption.CreateList(new FontFamily[0], null, "System default", CultureInfo.InvariantCulture));
            Assert.Throws<ArgumentNullException>(() => FontFamilyOption.CreateList(new FontFamily[0], font, "System default", null));
        }
    }
}
