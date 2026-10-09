using System.Collections.Generic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class PopupSettingsFacts
    {
        sealed class InMemorySettingsStorage : ISettingsStorage
        {
            public Dictionary<string, string> Data = new Dictionary<string, string>();

            public void Save(string name, Dictionary<string, string> values)
            {
                Data = new Dictionary<string, string>(values);
            }

            public Dictionary<string, string> Load(string name)
            {
                return new Dictionary<string, string>(Data);
            }
        }

        // The real JSON serialisation of SettingsProviderNet, kept in memory instead of %APPDATA%.
        sealed class InMemoryJsonStore : JsonSettingsStoreBase
        {
            readonly Dictionary<string, string> files = new Dictionary<string, string>();

            protected override void WriteTextFile(string filename, string fileContents)
            {
                files[filename] = fileContents;
            }

            protected override string ReadTextFile(string filename)
            {
                string contents;
                return files.TryGetValue(filename, out contents) ? contents : null;
            }
        }

        [Fact]
        public void shortcut_descriptions_are_shown_by_default()
        {
            var sut = new SettingsProvider(new InMemorySettingsStorage()).GetSettings<PopupSettings>();

            Assert.True(sut.ShowShortcutDescription);
        }

        [Fact]
        public void settings_saved_before_the_option_existed_still_show_shortcut_descriptions()
        {
            // arrange: a settings file written by a version that had no ShowShortcutDescription
            var storage = new InMemorySettingsStorage();
            storage.Data["ItemMaxWidth"] = "500";

            // act
            var sut = new SettingsProvider(storage).GetSettings<PopupSettings>();

            // assert
            Assert.Equal(500, sut.ItemMaxWidth);
            Assert.True(sut.ShowShortcutDescription);
        }

        [Fact]
        public void turning_shortcut_descriptions_off_is_saved_and_restored()
        {
            // arrange
            var storage = new InMemorySettingsStorage();
            var provider = new SettingsProvider(storage);
            var settings = provider.GetSettings<PopupSettings>();

            // act
            settings.ShowShortcutDescription = false;
            provider.SaveSettings(settings);
            var restored = new SettingsProvider(storage).GetSettings<PopupSettings>();

            // assert
            Assert.False(restored.ShowShortcutDescription);
        }

        [Fact]
        public void changing_shortcut_descriptions_notifies_the_view()
        {
            // arrange: KeyShowView binds to this property, so the popups update without a restart
            var sut = new PopupSettings();
            var changed = new List<string>();
            sut.PropertyChanged += (sender, e) => changed.Add(e.PropertyName);

            // act
            sut.ShowShortcutDescription = true;
            sut.ShowShortcutDescription = false;

            // assert
            Assert.Contains("ShowShortcutDescription", changed);
        }

        [Fact]
        public void a_new_instance_shows_every_key_and_ignores_none()
        {
            var settings = new PopupSettings();

            Assert.Equal(KeyCategory.All, settings.VisibleKeyCategories);
            Assert.True(string.IsNullOrEmpty(settings.IgnoredKeys));
            Assert.False(settings.ShowOnlyModifiers);
            Assert.False(settings.DetectShortcutsOnly);
        }

        [Fact]
        public void the_settings_provider_starts_with_every_key_shown_and_nothing_ignored()
        {
            var provider = new SettingsProvider(new InMemoryJsonStore());

            var settings = provider.GetSettings<PopupSettings>();

            Assert.Equal(KeyCategory.All, settings.VisibleKeyCategories);
            Assert.True(string.IsNullOrEmpty(settings.IgnoredKeys));
        }

        [Fact]
        public void settings_saved_before_the_key_filters_existed_keep_showing_every_key()
        {
            var storage = Substitute.For<ISettingsStorage>();
            storage.Load(Arg.Any<string>()).Returns(new Dictionary<string, string>
            {
                { "ItemMaxWidth", "420" },
                { "ShowOnlyModifiers", "True" }
            });
            var provider = new SettingsProvider(storage);

            var settings = provider.GetSettings<PopupSettings>();

            Assert.Equal(420, settings.ItemMaxWidth);
            Assert.True(settings.ShowOnlyModifiers);
            Assert.Equal(KeyCategory.All, settings.VisibleKeyCategories);
            Assert.True(string.IsNullOrEmpty(settings.IgnoredKeys));
        }

        [Fact]
        public void the_key_filters_survive_saving_and_loading()
        {
            var store = new InMemoryJsonStore();
            var settings = new SettingsProvider(store).GetSettings<PopupSettings>();
            settings.VisibleKeyCategories = KeyCategory.Function | KeyCategory.Navigation;
            settings.IgnoredKeys = "W,A,S,D\r\nCtrl+Alt+Delete";

            new SettingsProvider(store).SaveSettings(settings);
            var loaded = new SettingsProvider(store).GetSettings<PopupSettings>();

            Assert.Equal(KeyCategory.Function | KeyCategory.Navigation, loaded.VisibleKeyCategories);
            Assert.Equal("W,A,S,D\r\nCtrl+Alt+Delete", loaded.IgnoredKeys);
        }

        [Fact]
        public void every_category_ticked_is_stored_and_loaded_as_all()
        {
            var store = new InMemoryJsonStore();
            var provider = new SettingsProvider(store);
            var settings = provider.GetSettings<PopupSettings>();
            settings.VisibleKeyCategories = KeyCategory.None;
            provider.SaveSettings(settings);
            Assert.Equal(KeyCategory.None, new SettingsProvider(store).GetSettings<PopupSettings>().VisibleKeyCategories);

            settings.VisibleKeyCategories = KeyCategory.All;
            provider.SaveSettings(settings);

            Assert.Equal(KeyCategory.All, new SettingsProvider(store).GetSettings<PopupSettings>().VisibleKeyCategories);
        }

        [Fact]
        public void reset_to_defaults_shows_every_key_again()
        {
            var provider = new SettingsProvider(new InMemoryJsonStore());
            var settings = provider.GetSettings<PopupSettings>();
            settings.VisibleKeyCategories = KeyCategory.Letters;
            settings.IgnoredKeys = "W";
            provider.SaveSettings(settings);

            var defaults = provider.ResetToDefaults<PopupSettings>();

            Assert.Equal(KeyCategory.All, defaults.VisibleKeyCategories);
            Assert.True(string.IsNullOrEmpty(defaults.IgnoredKeys));
        }

        [Fact]
        public void the_settings_provider_shares_one_instance_so_preferences_apply_to_the_running_filters()
        {
            // App gives one instance to the MessageProvider and Preferences edits the instance the provider returns.
            var provider = new SettingsProvider(new InMemoryJsonStore());

            Assert.Same(provider.GetSettings<PopupSettings>(), provider.GetSettings<PopupSettings>());
        }

        [Fact]
        public void changing_the_key_categories_raises_property_changed()
        {
            var settings = new PopupSettings();
            var changed = new List<string>();
            settings.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            settings.VisibleKeyCategories = KeyCategory.Letters;
            settings.IgnoredKeys = "W";

            Assert.Contains("VisibleKeyCategories", changed);
            Assert.Contains("IgnoredKeys", changed);
        }
    }
}
