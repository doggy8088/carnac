using System.Collections.Generic;
using Carnac.Logic.Models;
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
    }
}
