using System;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Xunit;

namespace Carnac.Tests
{
    public class PasswordModeServiceFacts
    {
        readonly KeyDisplayState displayState = new KeyDisplayState();
        readonly PopupSettings settings = new PopupSettings { SilentModeHotkey = "Ctrl+Alt+P", PauseHotkey = string.Empty };
        readonly PasswordModeService sut;

        public PasswordModeServiceFacts()
        {
            sut = new PasswordModeService(displayState, settings);
        }

        static InterceptKeyEventArgs Key(Keys key, bool alt = false, bool control = false, bool shift = false, KeyDirection direction = KeyDirection.Down)
        {
            return new InterceptKeyEventArgs(key, direction, alt, control, shift);
        }

        static InterceptKeyEventArgs CtrlAltP()
        {
            return Key(Keys.P, alt: true, control: true);
        }

        [Fact]
        public void constructor_requires_a_display_state()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PasswordModeService(null, settings));

            Assert.Equal("displayState", exception.ParamName);
        }

        [Fact]
        public void constructor_requires_settings()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PasswordModeService(displayState, null));

            Assert.Equal("settings", exception.ParamName);
        }

        [Fact]
        public void check_requires_a_key()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => sut.CheckPasswordMode(null));

            Assert.Equal("key", exception.ParamName);
        }

        [Fact]
        public void ordinary_keys_are_not_swallowed_while_not_silent()
        {
            Assert.False(sut.CheckPasswordMode(Key(Keys.A)));
            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void ctrl_alt_p_switches_silent_mode_on_in_the_shared_state()
        {
            var swallowed = sut.CheckPasswordMode(CtrlAltP());

            Assert.True(swallowed);
            Assert.True(displayState.IsSilent);
            Assert.False(displayState.IsPaused);
        }

        [Fact]
        public void ctrl_alt_p_again_switches_silent_mode_off_and_is_still_swallowed()
        {
            sut.CheckPasswordMode(CtrlAltP());

            var swallowed = sut.CheckPasswordMode(CtrlAltP());

            Assert.True(swallowed);
            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void swallows_every_key_while_silent()
        {
            sut.CheckPasswordMode(CtrlAltP());

            Assert.True(sut.CheckPasswordMode(Key(Keys.A)));
            Assert.True(sut.CheckPasswordMode(Key(Keys.D1, shift: true)));
        }

        [Fact]
        public void shows_keys_again_after_leaving_silent_mode()
        {
            sut.CheckPasswordMode(CtrlAltP());
            sut.CheckPasswordMode(CtrlAltP());

            Assert.False(sut.CheckPasswordMode(Key(Keys.A)));
        }

        [Fact]
        public void silent_mode_switched_on_from_elsewhere_swallows_keys()
        {
            // for example the tray menu's "Silent mode" item
            displayState.ToggleSilent();

            Assert.True(sut.CheckPasswordMode(Key(Keys.A)));
        }

        [Fact]
        public void silent_mode_switched_off_from_elsewhere_shows_keys_again()
        {
            sut.CheckPasswordMode(CtrlAltP());
            displayState.ToggleSilent();

            Assert.False(sut.CheckPasswordMode(Key(Keys.A)));
        }

        [Fact]
        public void the_hotkey_leaves_silent_mode_that_was_switched_on_from_the_tray()
        {
            displayState.ToggleSilent();

            var swallowed = sut.CheckPasswordMode(CtrlAltP());

            Assert.True(swallowed);
            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void partial_matches_do_not_toggle_anything()
        {
            Assert.False(sut.CheckPasswordMode(Key(Keys.P)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.P, control: true)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.P, alt: true)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.P, alt: true, control: true, shift: true)));

            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void only_the_key_down_of_the_hotkey_toggles()
        {
            sut.CheckPasswordMode(Key(Keys.P, alt: true, control: true, direction: KeyDirection.Up));

            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void the_default_constructor_uses_its_own_state_and_the_default_hotkey()
        {
            var standalone = new PasswordModeService();

            Assert.True(standalone.CheckPasswordMode(CtrlAltP()));
            Assert.True(standalone.CheckPasswordMode(Key(Keys.A)));
            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void settings_that_were_never_written_still_use_ctrl_alt_p()
        {
            var service = new PasswordModeService(displayState, new PopupSettings());

            Assert.True(service.CheckPasswordMode(CtrlAltP()));
            Assert.True(displayState.IsSilent);
        }

        // ---- custom silent mode hotkey ----

        [Fact]
        public void a_custom_silent_mode_hotkey_toggles_silent_mode()
        {
            settings.SilentModeHotkey = "Ctrl+Shift+F9";

            var swallowed = sut.CheckPasswordMode(Key(Keys.F9, control: true, shift: true));

            Assert.True(swallowed);
            Assert.True(displayState.IsSilent);
        }

        [Fact]
        public void changing_the_hotkey_takes_effect_without_a_restart_and_the_old_combination_no_longer_toggles()
        {
            sut.CheckPasswordMode(CtrlAltP());
            Assert.True(displayState.IsSilent);
            sut.CheckPasswordMode(CtrlAltP());
            Assert.False(displayState.IsSilent);

            settings.SilentModeHotkey = "Ctrl+Alt+Q";

            // the old combination is an ordinary key now
            Assert.False(sut.CheckPasswordMode(CtrlAltP()));
            Assert.False(displayState.IsSilent);

            Assert.True(sut.CheckPasswordMode(Key(Keys.Q, alt: true, control: true)));
            Assert.True(displayState.IsSilent);
        }

        [Fact]
        public void an_empty_silent_mode_hotkey_switches_the_hotkey_off()
        {
            settings.SilentModeHotkey = string.Empty;

            Assert.False(sut.CheckPasswordMode(CtrlAltP()));
            Assert.False(displayState.IsSilent);
        }

        [Fact]
        public void an_invalid_silent_mode_hotkey_keeps_the_default_working()
        {
            settings.SilentModeHotkey = "Ctrl+Alt+NoSuchKey";

            Assert.True(sut.CheckPasswordMode(CtrlAltP()));
            Assert.True(displayState.IsSilent);
        }

        [Fact]
        public void a_custom_hotkey_needs_all_of_its_modifiers()
        {
            settings.SilentModeHotkey = "Ctrl+Shift+F9";

            Assert.False(sut.CheckPasswordMode(Key(Keys.F9, control: true)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.F9, shift: true)));
            Assert.False(sut.CheckPasswordMode(Key(Keys.F9, control: true, shift: true, alt: true)));
            Assert.False(displayState.IsSilent);
        }

        // ---- pause hotkey ----

        [Fact]
        public void there_is_no_pause_hotkey_by_default()
        {
            Assert.False(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.False(displayState.IsPaused);
        }

        [Fact]
        public void the_pause_hotkey_pauses_and_resumes_the_shared_state_and_is_never_shown()
        {
            settings.PauseHotkey = "Ctrl+Alt+O";

            Assert.True(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.True(displayState.IsPaused);
            Assert.False(displayState.IsSilent);

            Assert.True(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.False(displayState.IsPaused);
        }

        [Fact]
        public void pausing_does_not_swallow_ordinary_keys_here_because_the_message_provider_drops_them()
        {
            settings.PauseHotkey = "Ctrl+Alt+O";
            sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true));

            Assert.False(sut.CheckPasswordMode(Key(Keys.A)));
        }

        [Fact]
        public void the_pause_hotkey_can_be_set_while_carnac_is_running()
        {
            Assert.False(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));

            settings.PauseHotkey = "Ctrl+Alt+O";
            Assert.True(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.True(displayState.IsPaused);

            settings.PauseHotkey = string.Empty;
            Assert.False(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.True(displayState.IsPaused);
        }

        [Fact]
        public void an_invalid_pause_hotkey_is_ignored()
        {
            settings.PauseHotkey = "Ctrl+Alt+NoSuchKey";

            Assert.False(sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true)));
            Assert.False(displayState.IsPaused);
        }

        [Fact]
        public void both_hotkeys_work_independently()
        {
            settings.PauseHotkey = "Ctrl+Alt+O";

            sut.CheckPasswordMode(CtrlAltP());
            sut.CheckPasswordMode(Key(Keys.O, alt: true, control: true));

            Assert.True(displayState.IsSilent);
            Assert.True(displayState.IsPaused);

            sut.CheckPasswordMode(CtrlAltP());

            Assert.False(displayState.IsSilent);
            Assert.True(displayState.IsPaused);
        }

        [Fact]
        public void the_silent_mode_hotkey_wins_when_both_hotkeys_are_the_same()
        {
            settings.PauseHotkey = "Ctrl+Alt+P";

            sut.CheckPasswordMode(CtrlAltP());

            Assert.True(displayState.IsSilent);
            Assert.False(displayState.IsPaused);
        }
    }
}
