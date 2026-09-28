using System.Collections.Generic;
using Carnac.Logic;
using Carnac.Logic.Models;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>The hotkey settings as the real SettingsProvider stores and loads them.</summary>
    public class HotkeySettingsFacts
    {
        readonly InMemoryStorage storage = new InMemoryStorage();

        PopupSettings Load()
        {
            return new SettingsProvider(storage).GetSettings<PopupSettings>();
        }

        [Fact]
        public void a_fresh_installation_has_ctrl_alt_p_and_no_pause_hotkey()
        {
            var settings = Load();

            Assert.Equal("Ctrl+Alt+P", settings.SilentModeHotkey);
            Assert.Equal(string.Empty, settings.PauseHotkey);
        }

        [Fact]
        public void a_settings_file_of_an_older_version_gets_the_defaults()
        {
            // written before the hotkeys existed; the FontSize proves that this file is really the one that is loaded
            storage.Seed("PopupSettings", new Dictionary<string, string> { { "FontSize", "12" } });

            var settings = Load();

            Assert.Equal(12, settings.FontSize);
            Assert.Equal("Ctrl+Alt+P", settings.SilentModeHotkey);
            Assert.Equal(string.Empty, settings.PauseHotkey);
        }

        [Fact]
        public void custom_hotkeys_survive_a_restart()
        {
            var provider = new SettingsProvider(storage);
            var settings = provider.GetSettings<PopupSettings>();
            settings.SilentModeHotkey = "Ctrl+Shift+F9";
            settings.PauseHotkey = "Ctrl+Alt+O";
            provider.SaveSettings(settings);

            var reloaded = Load();

            Assert.Equal("Ctrl+Shift+F9", reloaded.SilentModeHotkey);
            Assert.Equal("Ctrl+Alt+O", reloaded.PauseHotkey);
        }

        [Fact]
        public void a_cleared_silent_mode_hotkey_stays_off_after_a_restart()
        {
            var provider = new SettingsProvider(storage);
            var settings = provider.GetSettings<PopupSettings>();
            settings.SilentModeHotkey = string.Empty;
            provider.SaveSettings(settings);

            var reloaded = Load();

            Assert.Equal(string.Empty, reloaded.SilentModeHotkey);
            Assert.Null(ConfiguredHotkeys.ResolveSilentMode(reloaded.SilentModeHotkey));
        }

        [Fact]
        public void a_hand_edited_invalid_silent_mode_hotkey_still_resolves_to_the_default()
        {
            storage.Seed("PopupSettings", new Dictionary<string, string> { { "SilentModeHotkey", "\"Ctrl+Alt+NoSuchKey\"" } });

            var reloaded = Load();

            Assert.Equal("Ctrl+Alt+NoSuchKey", reloaded.SilentModeHotkey);
            Assert.Equal(ConfiguredHotkeys.ResolveSilentMode(ConfiguredHotkeys.DefaultSilentMode), ConfiguredHotkeys.ResolveSilentMode(reloaded.SilentModeHotkey));
        }

        [Fact]
        public void settings_notify_when_a_hotkey_changes()
        {
            var settings = new PopupSettings();
            var changed = new List<string>();
            settings.PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            settings.SilentModeHotkey = "Ctrl+Alt+Q";
            settings.PauseHotkey = "Ctrl+Alt+O";

            Assert.Contains("SilentModeHotkey", changed);
            Assert.Contains("PauseHotkey", changed);
        }
    }
}
