using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.Utilities;
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
                Keys.CapsLock, Keys.NumLock, Keys.Scroll, Keys.PrintScreen, Keys.Pause, Keys.Cancel, Keys.Clear, Keys.Apps,
                Keys.LShiftKey, Keys.RShiftKey
            };

            foreach (var key in keys)
                Assert.True(SpecialKeys.IsSpecialKey(key.Sanitise()), key + " is drawn as " + key.Sanitise());
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
        public void keys_sharing_an_enum_value_have_a_pinned_name()
        {
            // Enum.ToString() may return either member of an aliased pair, so both are named explicitly
            Assert.Equal("Return", Keys.Return.Sanitise());
            Assert.Equal("Return", Keys.Enter.Sanitise());
            Assert.Equal("CapsLock", Keys.CapsLock.Sanitise());
            Assert.Equal("CapsLock", Keys.Capital.Sanitise());
            Assert.Equal("PrintScreen", Keys.PrintScreen.Sanitise());
            Assert.Equal("PrintScreen", Keys.Snapshot.Sanitise());
            Assert.Equal("PageUp", Keys.PageUp.Sanitise());
            Assert.Equal("PageUp", Keys.Prior.Sanitise());
            Assert.Equal("PageDown", Keys.PageDown.Sanitise());
            Assert.Equal("PageDown", Keys.Next.Sanitise());
        }

        [Fact]
        public void keys_are_labelled_as_on_the_keyboard()
        {
            Assert.Equal("ScrollLock", Keys.Scroll.Sanitise());
            Assert.Equal("Break", Keys.Cancel.Sanitise());
            Assert.Equal("ContextMenu", Keys.Apps.Sanitise());
        }

        [Fact]
        public void keymap_names_still_resolve_to_the_same_keys()
        {
            Assert.Equal(Keys.PageUp, ReplaceKey.ToKey("PageUp"));
            Assert.Equal(Keys.CapsLock, ReplaceKey.ToKey("CapsLock"));
            Assert.Equal(Keys.Scroll, ReplaceKey.ToKey("ScrollLock"));
            Assert.Equal(Keys.PrintScreen, ReplaceKey.ToKey("PrintScreen"));
            Assert.Equal(Keys.Capital, ReplaceKey.ToKey("Capital"));
            Assert.Equal(Keys.Return, ReplaceKey.ToKey("Return"));
            Assert.Equal(Keys.Cancel, ReplaceKey.ToKey("Break"));
            Assert.Equal(Keys.Apps, ReplaceKey.ToKey("ContextMenu"));
            // "Menu" is the Alt key (VK_MENU); the context-menu key must not take its name
            Assert.Equal(Keys.Menu, ReplaceKey.ToKey("Menu"));
        }

        [Fact]
        public void modifier_names_are_special_keys()
        {
            // "Ctrl", "Alt" and "Shift" are the texts KeyProvider emits (see KeyProviderTests.ctrlshiftl_is_processed_correctly)
            Assert.True(SpecialKeys.IsSpecialKey("Ctrl"));
            Assert.True(SpecialKeys.IsSpecialKey("Alt"));
            Assert.True(SpecialKeys.IsSpecialKey("Shift"));
        }

        [Fact]
        public void space_in_a_shortcut_is_drawn_as_a_key_cap()
        {
            // Ctrl+Space, and Shift+Space (HasModifierPressed does not cover Shift)
            foreach (var modifier in new[] { "Ctrl", "Shift" })
            {
                var keyPress = new KeyPress(
                    new ProcessInfo("FakeProcess"),
                    new InterceptKeyEventArgs(Keys.Space, KeyDirection.Down, false, modifier == "Ctrl", modifier == "Shift"),
                    false,
                    new[] { modifier, " " });

                var textParts = keyPress.GetTextParts().ToArray();

                Assert.Equal(new[] { modifier, " + ", "Space" }, textParts);
                Assert.True(SpecialKeys.IsSpecialKey(textParts[2]));
            }
        }

        [Fact]
        public void a_typed_space_is_not_a_key_cap()
        {
            var keyPress = new KeyPress(
                new ProcessInfo("FakeProcess"),
                new InterceptKeyEventArgs(Keys.Space, KeyDirection.Down, false, false, false),
                false,
                new[] { " " });

            Assert.Equal(new[] { " " }, keyPress.GetTextParts().ToArray());
            Assert.False(SpecialKeys.IsSpecialKey(" "));
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
    }
}
