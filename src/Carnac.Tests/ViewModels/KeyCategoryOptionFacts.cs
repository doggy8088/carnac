using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Properties;
using Carnac.UI;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests.ViewModels
{
    public class KeyCategoryOptionFacts
    {
        readonly PopupSettings settings = new PopupSettings();

        PreferencesViewModel CreateViewModel()
        {
            var previousCulture = Resources.Culture;
            try
            {
                Resources.Culture = CultureInfo.InvariantCulture;
                var settingsProvider = Substitute.For<ISettingsProvider>();
                settingsProvider.GetSettings<PopupSettings>().Returns(settings);
                return new PreferencesViewModel(settingsProvider, Substitute.For<IScreenManager>());
            }
            finally
            {
                Resources.Culture = previousCulture;
            }
        }

        [Fact]
        public void preferences_offer_one_check_box_per_key_category()
        {
            var options = CreateViewModel().KeyCategoryOptions;

            Assert.Equal(8, options.Count);
            Assert.True(options.All(option => !string.IsNullOrWhiteSpace(option.Label) && !string.IsNullOrWhiteSpace(option.Description)));
            Assert.Equal(options.Select(option => option.Label).Distinct().Count(), options.Count);
        }

        [Fact]
        public void all_check_boxes_are_ticked_by_default()
        {
            var options = CreateViewModel().KeyCategoryOptions;

            Assert.True(options.All(option => option.IsSelected));
        }

        [Fact]
        public void unticking_a_box_clears_only_its_category()
        {
            var options = CreateViewModel().KeyCategoryOptions;
            var letters = options.Single(option => option.Label == "Letters");

            letters.IsSelected = false;

            Assert.Equal(KeyCategory.All & ~KeyCategory.Letters, settings.VisibleKeyCategories);
            Assert.False(letters.IsSelected);
            Assert.Equal(7, options.Count(option => option.IsSelected));
        }

        [Fact]
        public void ticking_a_box_again_restores_its_category()
        {
            var options = CreateViewModel().KeyCategoryOptions;
            var function = options.Single(option => option.Label == "Function keys");
            function.IsSelected = false;

            function.IsSelected = true;

            Assert.Equal(KeyCategory.All, settings.VisibleKeyCategories);
        }

        [Fact]
        public void unticking_every_box_hides_all_categories()
        {
            var options = CreateViewModel().KeyCategoryOptions;

            foreach (var option in options)
                option.IsSelected = false;

            Assert.Equal(KeyCategory.None, settings.VisibleKeyCategories);
        }

        [Fact]
        public void check_boxes_show_the_categories_that_are_saved()
        {
            settings.VisibleKeyCategories = KeyCategory.Function | KeyCategory.Navigation;

            var selected = CreateViewModel().KeyCategoryOptions.Where(option => option.IsSelected).Select(option => option.Label).ToArray();

            Assert.Equal(new[] { "Navigation", "Function keys" }, selected);
        }

        [Fact]
        public void changing_a_check_box_raises_property_changed()
        {
            var option = CreateViewModel().KeyCategoryOptions.First();
            var changed = new List<string>();
            option.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            option.IsSelected = false;

            Assert.Contains("IsSelected", changed);
        }
    }
}
