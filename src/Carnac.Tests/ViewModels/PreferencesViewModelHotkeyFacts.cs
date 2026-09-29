using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.UI;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests.ViewModels
{
    public class PreferencesViewModelHotkeyFacts
    {
        readonly ISettingsProvider settingsProvider = Substitute.For<ISettingsProvider>();
        readonly IScreenManager screenManager = Substitute.For<IScreenManager>();
        readonly PopupSettings settings = new PopupSettings
        {
            FontColor = "White",
            ItemBackgroundColor = "Black",
            SilentModeHotkey = "Ctrl+Alt+P",
            PauseHotkey = string.Empty
        };

        PreferencesViewModel CreateViewModel()
        {
            settingsProvider.GetSettings<PopupSettings>().Returns(settings);
            screenManager.GetScreens().Returns(new List<DetailedScreen>
            {
                new DetailedScreen { Index = 0, Width = 1920, Height = 1080, NotificationPlacementBottomLeft = true }
            });
            return new PreferencesViewModel(settingsProvider, screenManager);
        }

        static void Save(PreferencesViewModel viewModel)
        {
            viewModel.SaveCommand.Execute(null);
        }

        [Fact]
        public void shows_the_configured_hotkeys()
        {
            settings.SilentModeHotkey = "ctrl+shift+f9";
            settings.PauseHotkey = "Ctrl+Alt+O";

            var sut = CreateViewModel();

            Assert.Equal("Ctrl+Shift+F9", sut.SilentModeHotkey);
            Assert.Equal("Ctrl+Alt+O", sut.PauseHotkey);
            Assert.Equal(string.Empty, sut.HotkeyMessage);
        }

        [Fact]
        public void shows_the_default_silent_mode_hotkey_and_no_pause_hotkey()
        {
            var sut = CreateViewModel();

            Assert.Equal("Ctrl+Alt+P", sut.SilentModeHotkey);
            Assert.Equal(string.Empty, sut.PauseHotkey);
        }

        [Fact]
        public void a_cleared_silent_mode_hotkey_stays_cleared()
        {
            settings.SilentModeHotkey = string.Empty;

            var sut = CreateViewModel();

            Assert.Equal(string.Empty, sut.SilentModeHotkey);
        }

        [Fact]
        public void capturing_a_key_combination_sets_the_hotkey()
        {
            var sut = CreateViewModel();

            sut.CaptureSilentModeHotkey(Keys.F9, true, false, true, false);
            sut.CapturePauseHotkey(Keys.O, true, true, false, false);

            Assert.Equal("Ctrl+Shift+F9", sut.SilentModeHotkey);
            Assert.Equal("Ctrl+Alt+O", sut.PauseHotkey);
            Assert.Equal(string.Empty, sut.HotkeyMessage);
        }

        [Fact]
        public void capturing_notifies_the_view()
        {
            var sut = CreateViewModel();
            var changed = new List<string>();
            ((INotifyPropertyChanged)sut).PropertyChanged += (sender, args) => changed.Add(args.PropertyName);

            sut.CapturePauseHotkey(Keys.O, true, true, false, false);

            Assert.Contains("PauseHotkey", changed);
        }

        [Fact]
        public void a_key_combination_without_a_modifier_is_refused_with_a_reason()
        {
            var sut = CreateViewModel();

            sut.CapturePauseHotkey(Keys.O, false, false, false, false);

            Assert.Equal(string.Empty, sut.PauseHotkey);
            Assert.Contains("modifier", sut.HotkeyMessage);
        }

        [Fact]
        public void the_windows_key_is_refused()
        {
            var sut = CreateViewModel();

            sut.CapturePauseHotkey(Keys.O, true, false, false, true);

            Assert.Equal(string.Empty, sut.PauseHotkey);
            Assert.Contains("Windows", sut.HotkeyMessage);
        }

        [Fact]
        public void a_refused_capture_keeps_the_previous_hotkey()
        {
            var sut = CreateViewModel();

            sut.CaptureSilentModeHotkey(Keys.A, false, false, false, false);

            Assert.Equal("Ctrl+Alt+P", sut.SilentModeHotkey);
        }

        [Fact]
        public void the_two_hotkeys_must_differ()
        {
            var sut = CreateViewModel();

            sut.CapturePauseHotkey(Keys.P, true, true, false, false);

            Assert.Equal(string.Empty, sut.PauseHotkey);
            Assert.Contains("already used", sut.HotkeyMessage);
        }

        [Fact]
        public void the_silent_mode_hotkey_must_differ_from_the_pause_hotkey_too()
        {
            settings.PauseHotkey = "Ctrl+Alt+O";
            var sut = CreateViewModel();

            sut.CaptureSilentModeHotkey(Keys.O, true, true, false, false);

            Assert.Equal("Ctrl+Alt+P", sut.SilentModeHotkey);
            Assert.Contains("already used", sut.HotkeyMessage);
        }

        [Fact]
        public void a_successful_capture_clears_the_message()
        {
            var sut = CreateViewModel();
            sut.CapturePauseHotkey(Keys.O, false, false, false, false);
            Assert.NotEqual(string.Empty, sut.HotkeyMessage);

            sut.CapturePauseHotkey(Keys.O, true, true, false, false);

            Assert.Equal(string.Empty, sut.HotkeyMessage);
        }

        [Fact]
        public void the_clear_buttons_remove_the_hotkeys()
        {
            settings.PauseHotkey = "Ctrl+Alt+O";
            var sut = CreateViewModel();

            sut.ClearSilentModeHotkeyCommand.Execute(null);
            sut.ClearPauseHotkeyCommand.Execute(null);

            Assert.Equal(string.Empty, sut.SilentModeHotkey);
            Assert.Equal(string.Empty, sut.PauseHotkey);
        }

        [Fact]
        public void nothing_is_applied_before_save()
        {
            var sut = CreateViewModel();

            sut.CaptureSilentModeHotkey(Keys.F9, true, false, true, false);
            sut.CapturePauseHotkey(Keys.O, true, true, false, false);

            Assert.Equal("Ctrl+Alt+P", settings.SilentModeHotkey);
            Assert.Equal(string.Empty, settings.PauseHotkey);
        }

        [Fact]
        public void save_applies_and_stores_the_hotkeys()
        {
            var sut = CreateViewModel();
            sut.CaptureSilentModeHotkey(Keys.F9, true, false, true, false);
            sut.CapturePauseHotkey(Keys.O, true, true, false, false);

            Save(sut);

            Assert.Equal("Ctrl+Shift+F9", settings.SilentModeHotkey);
            Assert.Equal("Ctrl+Alt+O", settings.PauseHotkey);
            settingsProvider.Received().SaveSettings(settings);
        }

        [Fact]
        public void save_stores_cleared_hotkeys_as_empty()
        {
            var sut = CreateViewModel();
            sut.ClearSilentModeHotkeyCommand.Execute(null);

            Save(sut);

            Assert.Equal(string.Empty, settings.SilentModeHotkey);
            Assert.Equal(string.Empty, settings.PauseHotkey);
        }

        [Fact]
        public void save_does_not_touch_the_hotkeys_of_a_refused_capture()
        {
            var sut = CreateViewModel();
            sut.CapturePauseHotkey(Keys.O, false, false, false, false);

            Save(sut);

            Assert.Equal(string.Empty, settings.PauseHotkey);
            Assert.Equal("Ctrl+Alt+P", settings.SilentModeHotkey);
        }

        [Fact]
        public void reset_to_defaults_shows_the_default_hotkeys_again()
        {
            var storage = new InMemoryStorage();
            var realProvider = new SettingsProvider(storage);
            var realSettings = realProvider.GetSettings<PopupSettings>();
            realSettings.FontColor = "White";
            realSettings.ItemBackgroundColor = "Black";
            realSettings.SilentModeHotkey = "Ctrl+Shift+F9";
            realSettings.PauseHotkey = "Ctrl+Alt+O";
            var screens = Substitute.For<IScreenManager>();
            screens.GetScreens().Returns(new List<DetailedScreen> { new DetailedScreen { Index = 0 } });
            var sut = new PreferencesViewModel(realProvider, screens);
            Assert.Equal("Ctrl+Shift+F9", sut.SilentModeHotkey);

            sut.ResetToDefaultsCommand.Execute(null);

            Assert.Equal("Ctrl+Alt+P", sut.SilentModeHotkey);
            Assert.Equal(string.Empty, sut.PauseHotkey);
            Assert.Equal("Ctrl+Alt+P", realSettings.SilentModeHotkey);
            Assert.Equal(string.Empty, realSettings.PauseHotkey);
        }
    }
}
