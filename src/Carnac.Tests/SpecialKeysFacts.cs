using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Utilities;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;

namespace Carnac.Tests
{
    public class SpecialKeysFacts
    {
        [Fact]
        public void named_keys_are_special_keys_as_produced_by_sanitise()
        {
            var keys = new[]
            {
                Keys.Back, Keys.Escape, Keys.Tab,
                Keys.Insert, Keys.Delete, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
                Keys.CapsLock, Keys.NumLock, Keys.Scroll, Keys.PrintScreen, Keys.Pause, Keys.Apps,
                Keys.LShiftKey, Keys.RShiftKey
            };

            foreach (var key in keys)
                Assert.True(SpecialKeys.IsSpecialKey(key.Sanitise()), key + " is drawn as " + key.Sanitise());
        }

        [Fact]
        public void keys_sharing_an_enum_value_have_a_pinned_name()
        {
            // Enum.ToString() may return either member of an aliased pair, so these are named explicitly
            Assert.Equal("CapsLock", Keys.CapsLock.Sanitise());
            Assert.Equal("CapsLock", Keys.Capital.Sanitise());
            Assert.Equal("ScrollLock", Keys.Scroll.Sanitise());
            Assert.Equal("PrintScreen", Keys.PrintScreen.Sanitise());
            Assert.Equal("PrintScreen", Keys.Snapshot.Sanitise());
            Assert.Equal("PageUp", Keys.PageUp.Sanitise());
            Assert.Equal("PageUp", Keys.Prior.Sanitise());
            Assert.Equal("PageDown", Keys.PageDown.Sanitise());
            Assert.Equal("PageDown", Keys.Next.Sanitise());
        }

        [Fact]
        public void keymap_names_still_resolve_to_the_same_keys()
        {
            Assert.Equal(Keys.PageUp, ReplaceKey.ToKey("PageUp"));
            Assert.Equal(Keys.CapsLock, ReplaceKey.ToKey("CapsLock"));
            Assert.Equal(Keys.Scroll, ReplaceKey.ToKey("ScrollLock"));
            Assert.Equal(Keys.PrintScreen, ReplaceKey.ToKey("PrintScreen"));
            Assert.Equal(Keys.Capital, ReplaceKey.ToKey("Capital"));
        }

        [Fact]
        public void function_keys_are_special_keys()
        {
            for (var i = 0; i < 24; i++)
            {
                var key = (Keys)((int)Keys.F1 + i);
                Assert.True(SpecialKeys.IsSpecialKey(key.Sanitise()), key.ToString());
            }
        }

        [Fact]
        public async Task modifier_names_emitted_by_the_key_provider_are_special_keys()
        {
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var provider = new KeyProvider(KeyStreams.CtrlShiftL(), new PasswordModeService(), desktopLockEventService, Substitute.For<ISettingsProvider>());

            var keyPress = (await provider.GetKeyStream().ToList()).Single();

            Assert.True(SpecialKeys.IsSpecialKey(keyPress.Input.ElementAt(0)), keyPress.Input.ElementAt(0));
            Assert.True(SpecialKeys.IsSpecialKey(keyPress.Input.ElementAt(1)), keyPress.Input.ElementAt(1));
            Assert.False(SpecialKeys.IsSpecialKey(keyPress.Input.ElementAt(2)), keyPress.Input.ElementAt(2));
        }

        [Fact]
        public void text_is_not_a_special_key()
        {
            var texts = new[]
            {
                null, string.Empty, "a", "A", "1", " ", ".", " + ", ", ", " x 3 ", " [Copy]",
                "back", "f1", "F0", "F25", "Left", "Up", "Right", "Down"
            };

            foreach (var text in texts)
                Assert.False(SpecialKeys.IsSpecialKey(text), text ?? "<null>");
        }

        [Fact]
        public void converter_reports_whether_the_text_is_a_special_key()
        {
            var converter = new SpecialKeyConverter();

            Assert.Equal(true, converter.Convert("Tab", typeof(bool), null, null));
            Assert.Equal(false, converter.Convert("a", typeof(bool), null, null));
        }

        [Fact]
        public void converter_tolerates_null_and_non_string_values()
        {
            var converter = new SpecialKeyConverter();

            Assert.Equal(false, converter.Convert(null, typeof(bool), null, null));
            Assert.Equal(false, converter.Convert(42, typeof(bool), null, null));
        }

        [Fact]
        public void converter_is_one_way()
        {
            var converter = new SpecialKeyConverter();

            Assert.Same(System.Windows.Data.Binding.DoNothing, converter.ConvertBack(true, typeof(string), null, null));
        }
    }
}
