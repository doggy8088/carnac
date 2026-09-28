using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class KeyPressFacts
    {
        static readonly Keys[] TypingKeys =
        {
            Keys.A, Keys.M, Keys.Z, Keys.D0, Keys.D5, Keys.D9, Keys.NumPad0, Keys.NumPad9, Keys.Multiply, Keys.Add,
            Keys.Subtract, Keys.Decimal, Keys.Divide, Keys.Space, Keys.Oem1, Keys.Oemplus, Keys.Oemcomma, Keys.OemMinus,
            Keys.OemPeriod, Keys.OemQuestion, Keys.Oemtilde, Keys.OemOpenBrackets, Keys.Oem5, Keys.Oem6, Keys.Oem7,
            Keys.OemBackslash
        };

        static readonly Keys[] NonTypingKeys =
        {
            Keys.Enter, Keys.Tab, Keys.Escape, Keys.Back, Keys.Delete, Keys.Insert, Keys.Home, Keys.End, Keys.PageUp,
            Keys.PageDown, Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.F1, Keys.F5, Keys.F12, Keys.F24,
            Keys.PrintScreen, Keys.Pause, Keys.Apps, Keys.CapsLock, Keys.NumLock, Keys.Scroll
        };

        [Fact]
        public void letters_digits_punctuation_and_space_produce_a_character()
        {
            foreach (var key in TypingKeys)
                Assert.True(key.ProducesCharacter(), key + " types a character");
        }

        [Fact]
        public void enter_tab_navigation_and_function_keys_do_not_produce_a_character()
        {
            foreach (var key in NonTypingKeys)
                Assert.False(key.ProducesCharacter(), key + " does not type a character");
        }

        [Fact]
        public void shift_with_a_key_that_types_nothing_is_shortcut_like()
        {
            foreach (var key in NonTypingKeys)
                Assert.True(KeyPresses.Create("app", key, shift: true).IsShortcutLike, "Shift+" + key);
        }

        [Fact]
        public void shift_with_a_typing_key_is_not_shortcut_like()
        {
            foreach (var key in TypingKeys)
                Assert.False(KeyPresses.Create("app", key, shift: true).IsShortcutLike, "Shift+" + key);
        }

        [Fact]
        public void keys_without_modifiers_are_not_shortcut_like()
        {
            foreach (var key in TypingKeys.Concat(NonTypingKeys))
                Assert.False(KeyPresses.Create("app", key).IsShortcutLike, key.ToString());
        }

        [Fact]
        public void control_alt_and_windows_make_any_key_shortcut_like()
        {
            foreach (var key in TypingKeys.Concat(NonTypingKeys))
            {
                Assert.True(KeyPresses.Create("app", key, control: true).IsShortcutLike, "Ctrl+" + key);
                Assert.True(KeyPresses.Create("app", key, alt: true).IsShortcutLike, "Alt+" + key);
                Assert.True(KeyPresses.Create("app", key, win: true).IsShortcutLike, "Win+" + key);
                Assert.True(KeyPresses.Create("app", key, shift: true, win: true).IsShortcutLike, "Win+Shift+" + key);
            }
        }

        [Fact]
        public void has_modifier_pressed_still_ignores_shift()
        {
            // HasModifierPressed keeps its meaning (Ctrl, Alt or Windows) and drives how Space is displayed.
            Assert.False(KeyPresses.Create("app", Keys.Enter, shift: true).HasModifierPressed);
            Assert.True(KeyPresses.Create("app", Keys.Enter, control: true).HasModifierPressed);
            Assert.True(KeyPresses.Create("app", Keys.Enter, win: true).HasModifierPressed);
        }
    }
}
