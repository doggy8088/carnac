using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    /// <summary>The hotkeys through the whole pipeline: key provider, password mode service, message provider.</summary>
    public class HotkeyPipelineFacts
    {
        readonly Subject<InterceptKeyEventArgs> source = new Subject<InterceptKeyEventArgs>();
        readonly KeyDisplayState displayState = new KeyDisplayState();
        readonly PopupSettings settings = new PopupSettings { SilentModeHotkey = "Ctrl+Alt+P", PauseHotkey = "Ctrl+Alt+O" };
        readonly List<Message> messages = new List<Message>();

        public HotkeyPipelineFacts()
        {
            var interceptKeys = Substitute.For<IInterceptKeys>();
            interceptKeys.GetKeyStream().Returns(source);
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(settings);
            var shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());

            var keyProvider = new KeyProvider(interceptKeys, new PasswordModeService(displayState, settings), desktopLockEventService, settingsProvider);
            new MessageProvider(shortcutProvider, keyProvider, settings, displayState).GetMessageStream().Subscribe(messages.Add);
        }

        void Press(Keys key, bool control = false, bool alt = false)
        {
            source.OnNext(new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, false));
            source.OnNext(new InterceptKeyEventArgs(key, KeyDirection.Up, alt, control, false));
        }

        [Fact]
        public void keys_are_shown_before_any_hotkey_is_used()
        {
            Press(Keys.A);

            Assert.Equal(1, messages.Count);
        }

        [Fact]
        public void the_pause_hotkey_stops_popups_until_it_is_pressed_again()
        {
            Press(Keys.A);
            Assert.Equal(1, messages.Count);

            Press(Keys.O, control: true, alt: true);
            Assert.True(displayState.IsPaused);
            Press(Keys.B);
            Press(Keys.C);
            Assert.Equal(1, messages.Count);

            Press(Keys.O, control: true, alt: true);
            Assert.False(displayState.IsPaused);
            Assert.Equal(1, messages.Count);

            Press(Keys.D);
            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public void the_silent_mode_hotkey_hides_the_keys_and_itself()
        {
            Press(Keys.A);

            Press(Keys.P, control: true, alt: true);
            Assert.True(displayState.IsSilent);
            Press(Keys.S);
            Assert.Equal(1, messages.Count);

            Press(Keys.P, control: true, alt: true);
            Assert.False(displayState.IsSilent);
            Assert.Equal(1, messages.Count);

            Press(Keys.D);
            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public void a_hotkey_that_is_changed_in_the_settings_works_at_once_and_the_old_one_stops_working()
        {
            settings.PauseHotkey = "Ctrl+Alt+Q";

            Press(Keys.O, control: true, alt: true);
            Assert.False(displayState.IsPaused);
            Assert.Equal(1, messages.Count);   // the old combination is just another key press now

            Press(Keys.Q, control: true, alt: true);
            Assert.True(displayState.IsPaused);
            Press(Keys.A);
            Assert.Equal(1, messages.Count);
        }

        [Fact]
        public void pausing_from_the_tray_and_resuming_with_the_hotkey_share_one_state()
        {
            displayState.TogglePaused();
            Press(Keys.A);
            Assert.Equal(0, messages.Count);

            Press(Keys.O, control: true, alt: true);
            Assert.False(displayState.IsPaused);

            Press(Keys.A);
            Assert.Equal(1, messages.Count);
        }
    }
}
