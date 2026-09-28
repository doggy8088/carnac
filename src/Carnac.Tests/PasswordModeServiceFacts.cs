using System;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Xunit;

namespace Carnac.Tests
{
    public class PasswordModeServiceFacts
    {
        readonly KeyDisplayState displayState = new KeyDisplayState();
        readonly PasswordModeService sut;

        public PasswordModeServiceFacts()
        {
            sut = new PasswordModeService(displayState);
        }

        static InterceptKeyEventArgs Key(Keys key, bool alt = false, bool control = false, bool shift = false)
        {
            return new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift);
        }

        static InterceptKeyEventArgs CtrlAltP()
        {
            return Key(Keys.P, alt: true, control: true);
        }

        [Fact]
        public void constructor_requires_a_display_state()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new PasswordModeService(null));

            Assert.Equal("displayState", exception.ParamName);
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
        public void the_default_constructor_uses_its_own_state()
        {
            var standalone = new PasswordModeService();

            Assert.True(standalone.CheckPasswordMode(CtrlAltP()));
            Assert.True(standalone.CheckPasswordMode(Key(Keys.A)));
            Assert.False(displayState.IsSilent);
        }
    }
}
