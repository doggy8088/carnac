using System.Windows.Forms;
using Carnac.Logic;
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
        public void function_keys_are_special_keys()
        {
            for (var i = 0; i < 24; i++)
            {
                var key = (Keys)((int)Keys.F1 + i);
                Assert.True(SpecialKeys.IsSpecialKey(key.Sanitise()), key.ToString());
            }
        }

        [Fact]
        public void modifier_names_are_special_keys()
        {
            Assert.True(SpecialKeys.IsSpecialKey("Ctrl"));
            Assert.True(SpecialKeys.IsSpecialKey("Alt"));
            Assert.True(SpecialKeys.IsSpecialKey("Shift"));
        }

        [Fact]
        public void text_is_not_a_special_key()
        {
            var texts = new[]
            {
                null, string.Empty, "a", "A", "1", " ", ".", " + ", ", ", " x 3 ", " [Copy]",
                "back", "f1", "F0", "F25", "Return", "Win", "Left", "Up", "Right", "Down"
            };

            foreach (var text in texts)
                Assert.False(SpecialKeys.IsSpecialKey(text), text ?? "<null>");
        }
    }
}
