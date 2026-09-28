using System.Windows.Forms;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class ConfiguredHotkeysFacts
    {
        static readonly KeyPressDefinition CtrlAltP = new KeyPressDefinition(Keys.P, controlPressed: true, altPressed: true);

        [Fact]
        public void the_default_silent_mode_hotkey_is_the_one_carnac_always_had()
        {
            Assert.Equal("Ctrl+Alt+P", ConfiguredHotkeys.DefaultSilentMode);
            Assert.Equal(CtrlAltP, ConfiguredHotkeys.ResolveSilentMode(ConfiguredHotkeys.DefaultSilentMode));
        }

        [Fact]
        public void silent_mode_uses_the_default_when_nothing_was_ever_configured()
        {
            Assert.Equal(CtrlAltP, ConfiguredHotkeys.ResolveSilentMode(null));
        }

        [Fact]
        public void silent_mode_uses_a_custom_hotkey()
        {
            Assert.Equal(new KeyPressDefinition(Keys.F9, controlPressed: true, shiftPressed: true), ConfiguredHotkeys.ResolveSilentMode("Ctrl+Shift+F9"));
        }

        [Fact]
        public void an_empty_silent_mode_hotkey_switches_the_hotkey_off()
        {
            Assert.Null(ConfiguredHotkeys.ResolveSilentMode(string.Empty));
            Assert.Null(ConfiguredHotkeys.ResolveSilentMode("   "));
        }

        [Fact]
        public void an_invalid_silent_mode_hotkey_falls_back_to_the_default()
        {
            Assert.Equal(CtrlAltP, ConfiguredHotkeys.ResolveSilentMode("Ctrl+Alt+NoSuchKey"));
            Assert.Equal(CtrlAltP, ConfiguredHotkeys.ResolveSilentMode("P"));
        }

        [Fact]
        public void pause_is_off_by_default()
        {
            Assert.Null(ConfiguredHotkeys.ResolvePause(null));
            Assert.Null(ConfiguredHotkeys.ResolvePause(string.Empty));
        }

        [Fact]
        public void pause_uses_a_custom_hotkey()
        {
            Assert.Equal(new KeyPressDefinition(Keys.O, controlPressed: true, altPressed: true), ConfiguredHotkeys.ResolvePause("Ctrl+Alt+O"));
        }

        [Fact]
        public void an_invalid_pause_hotkey_is_ignored()
        {
            Assert.Null(ConfiguredHotkeys.ResolvePause("Ctrl+Alt+NoSuchKey"));
            Assert.Null(ConfiguredHotkeys.ResolvePause("O"));
        }
    }
}
