using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class HotkeyParserFacts
    {
        static KeyPressDefinition Parse(string text)
        {
            KeyPressDefinition hotkey;
            string error;
            Assert.True(HotkeyParser.TryParse(text, out hotkey, out error), "'" + text + "' should be valid but: " + error);
            Assert.Null(error);
            return hotkey;
        }

        static string Reject(string text)
        {
            KeyPressDefinition hotkey;
            string error;
            Assert.False(HotkeyParser.TryParse(text, out hotkey, out error), "'" + text + "' should be rejected");
            Assert.Null(hotkey);
            Assert.False(string.IsNullOrEmpty(error), "a rejected hotkey needs a reason");
            return error;
        }

        [Fact]
        public void parses_the_default_silent_mode_hotkey()
        {
            var hotkey = Parse("Ctrl+Alt+P");

            Assert.Equal(new KeyPressDefinition(Keys.P, controlPressed: true, altPressed: true), hotkey);
        }

        [Fact]
        public void parses_every_modifier()
        {
            Assert.Equal(new KeyPressDefinition(Keys.A, controlPressed: true), Parse("Ctrl+A"));
            Assert.Equal(new KeyPressDefinition(Keys.A, altPressed: true), Parse("Alt+A"));
            Assert.Equal(new KeyPressDefinition(Keys.A, shiftPressed: true), Parse("Shift+A"));
            Assert.Equal(new KeyPressDefinition(Keys.A, shiftPressed: true, altPressed: true, controlPressed: true), Parse("Ctrl+Alt+Shift+A"));
        }

        [Fact]
        public void accepts_control_as_a_spelling_of_ctrl()
        {
            Assert.Equal(Parse("Ctrl+P"), Parse("Control+P"));
        }

        [Fact]
        public void is_case_insensitive()
        {
            Assert.Equal(Parse("Ctrl+Alt+P"), Parse("ctrl+ALT+p"));
            Assert.Equal(Parse("Ctrl+Alt+F5"), Parse("CTRL+alt+f5"));
            Assert.Equal(Parse("Ctrl+Space"), Parse("ctrl+SPACE"));
        }

        [Fact]
        public void ignores_whitespace_around_the_parts()
        {
            Assert.Equal(Parse("Ctrl+Alt+P"), Parse("  Ctrl + Alt + P  "));
        }

        [Fact]
        public void the_order_of_the_modifiers_does_not_matter()
        {
            Assert.Equal(Parse("Ctrl+Alt+Shift+P"), Parse("Shift+Alt+Ctrl+P"));
        }

        [Fact]
        public void parses_digits_as_the_number_keys_and_not_as_enum_numbers()
        {
            Assert.Equal(Keys.D1, Parse("Ctrl+1").Key);
            Assert.Equal(Keys.D0, Parse("Ctrl+0").Key);
            Assert.Equal(Keys.D9, Parse("Ctrl+Alt+9").Key);
        }

        [Fact]
        public void parses_function_and_named_keys()
        {
            Assert.Equal(Keys.F5, Parse("Ctrl+F5").Key);
            Assert.Equal(Keys.F12, Parse("Alt+F12").Key);
            Assert.Equal(Keys.Space, Parse("Ctrl+Alt+Space").Key);
            Assert.Equal(Keys.Pause, Parse("Ctrl+Pause").Key);
            Assert.Equal(Keys.PageDown, Parse("Ctrl+PageDown").Key);
            Assert.Equal(Keys.NumPad1, Parse("Ctrl+NumPad1").Key);
            Assert.Equal(Keys.Enter, Parse("Ctrl+Enter").Key);
        }

        [Fact]
        public void parses_symbols_through_the_keymap_key_names()
        {
            Assert.Equal(Keys.OemMinus, Parse("Ctrl+-").Key);
            Assert.Equal(Keys.Oemcomma, Parse("Ctrl+,").Key);
            Assert.Equal(Keys.OemPeriod, Parse("Ctrl+.").Key);
        }

        [Fact]
        public void the_result_never_has_the_windows_key()
        {
            Assert.False(Parse("Ctrl+Alt+P").WinkeyPressed);
        }

        [Fact]
        public void rejects_missing_text()
        {
            Reject(null);
            Reject("");
            Reject("   ");
        }

        [Fact]
        public void rejects_a_hotkey_without_a_modifier()
        {
            var error = Reject("P");

            Assert.Contains("modifier", error);
            Reject("F5");
        }

        [Fact]
        public void rejects_modifiers_without_a_key()
        {
            Assert.Contains("modifier", Reject("Ctrl"));
            Assert.Contains("modifier", Reject("Ctrl+Alt"));
            Assert.Contains("modifier", Reject("Ctrl+Alt+Shift"));
            Assert.Contains("modifier", Reject("Ctrl+Control"));
        }

        [Fact]
        public void rejects_the_windows_key()
        {
            Assert.Contains("Windows", Reject("Win+P"));
            Assert.Contains("Windows", Reject("Ctrl+Windows+P"));
            Assert.Contains("Windows", Reject("Ctrl+Win"));
        }

        [Fact]
        public void rejects_empty_parts()
        {
            Reject("Ctrl++P");
            Reject("+P");
            Reject("Ctrl+P+");
            Reject("Ctrl+");
        }

        [Fact]
        public void rejects_a_modifier_that_is_listed_twice()
        {
            Assert.Contains("twice", Reject("Ctrl+Ctrl+P"));
            Assert.Contains("twice", Reject("Ctrl+Control+P"));
        }

        [Fact]
        public void rejects_more_than_one_key()
        {
            Reject("Ctrl+A+B");
            Reject("Ctrl+P+Alt");
        }

        [Fact]
        public void rejects_unknown_keys()
        {
            Assert.Contains("Foo", Reject("Ctrl+Alt+Foo"));
            Reject("Ctrl+Page Down");
        }

        [Fact]
        public void rejects_numbers_that_are_not_a_single_digit()
        {
            // Enum.TryParse would happily turn these into key codes
            Reject("Ctrl+65");
            Reject("Ctrl+12");
            Reject("Ctrl+-1");
        }

        [Fact]
        public void rejects_enum_flag_lists()
        {
            Reject("Ctrl+A, B");
            Reject("Ctrl+A,B");
        }

        [Fact]
        public void rejects_mouse_buttons_and_modifier_keys_as_the_key()
        {
            Assert.Contains("Mouse", Reject("Ctrl+LButton"));
            Reject("Ctrl+RButton");
            Reject("Ctrl+ShiftKey");
            Reject("Alt+LControlKey");
            Reject("Ctrl+Menu");
            Reject("Ctrl+LWin");
        }

        [Fact]
        public void formats_modifiers_in_a_fixed_order()
        {
            var hotkey = new KeyPressDefinition(Keys.P, shiftPressed: true, altPressed: true, controlPressed: true);

            Assert.Equal("Ctrl+Alt+Shift+P", HotkeyParser.Format(hotkey));
        }

        [Fact]
        public void formats_the_default_hotkey_like_the_readme()
        {
            Assert.Equal("Ctrl+Alt+P", HotkeyParser.Format(new KeyPressDefinition(Keys.P, controlPressed: true, altPressed: true)));
        }

        [Fact]
        public void formats_digits_as_digits_and_aliased_keys_with_one_stable_name()
        {
            Assert.Equal("Ctrl+1", HotkeyParser.Format(new KeyPressDefinition(Keys.D1, controlPressed: true)));
            Assert.Equal("Ctrl+Enter", HotkeyParser.Format(new KeyPressDefinition(Keys.Return, controlPressed: true)));
            Assert.Equal("Ctrl+PageDown", HotkeyParser.Format(new KeyPressDefinition(Keys.Next, controlPressed: true)));
            Assert.Equal("Ctrl+PageUp", HotkeyParser.Format(new KeyPressDefinition(Keys.Prior, controlPressed: true)));
        }

        [Fact]
        public void format_requires_a_hotkey()
        {
            Assert.Throws<ArgumentNullException>(() => HotkeyParser.Format(null));
        }

        [Fact]
        public void formatting_and_parsing_round_trip_for_every_usable_key()
        {
            var failures = new List<string>();
            var usableKeys = 0;
            foreach (var key in Enum.GetValues(typeof(Keys)).Cast<Keys>().Distinct())
            {
                KeyPressDefinition created;
                string error;
                if (!HotkeyParser.TryCreate(key, true, false, false, false, out created, out error))
                    continue;

                usableKeys++;
                var text = HotkeyParser.Format(created);
                KeyPressDefinition parsed;
                if (!HotkeyParser.TryParse(text, out parsed, out error) || !created.Equals(parsed))
                    failures.Add(key + " -> '" + text + "'");
            }

            Assert.True(usableKeys > 100, "expected the letters, digits, function keys, ... to be usable");
            Assert.Equal(string.Empty, string.Join(", ", failures));
        }

        [Fact]
        public void formatting_and_parsing_round_trip_for_every_modifier_combination()
        {
            for (var mask = 1; mask < 8; mask++)
            {
                var hotkey = new KeyPressDefinition(Keys.Q, controlPressed: (mask & 1) != 0, altPressed: (mask & 2) != 0, shiftPressed: (mask & 4) != 0);

                Assert.Equal(hotkey, Parse(HotkeyParser.Format(hotkey)));
            }
        }

        [Fact]
        public void create_accepts_a_captured_key_combination()
        {
            KeyPressDefinition hotkey;
            string error;

            Assert.True(HotkeyParser.TryCreate(Keys.P, true, true, false, false, out hotkey, out error));

            Assert.Equal("Ctrl+Alt+P", HotkeyParser.Format(hotkey));
            Assert.Null(error);
        }

        [Fact]
        public void create_rejects_a_captured_key_without_modifier()
        {
            KeyPressDefinition hotkey;
            string error;

            Assert.False(HotkeyParser.TryCreate(Keys.P, false, false, false, false, out hotkey, out error));

            Assert.Null(hotkey);
            Assert.Contains("modifier", error);
        }

        [Fact]
        public void create_rejects_the_windows_key_modifier()
        {
            KeyPressDefinition hotkey;
            string error;

            Assert.False(HotkeyParser.TryCreate(Keys.P, true, false, false, true, out hotkey, out error));

            Assert.Contains("Windows", error);
        }

        [Fact]
        public void create_rejects_keys_that_are_no_keys()
        {
            KeyPressDefinition hotkey;
            string error;

            Assert.False(HotkeyParser.TryCreate(Keys.None, true, false, false, false, out hotkey, out error));
            Assert.False(HotkeyParser.TryCreate(Keys.ControlKey, true, false, false, false, out hotkey, out error));
            Assert.False(HotkeyParser.TryCreate(Keys.LMenu, true, false, false, false, out hotkey, out error));
            Assert.False(HotkeyParser.TryCreate(Keys.Control | Keys.A, true, false, false, false, out hotkey, out error));
            Assert.False(HotkeyParser.TryCreate((Keys)0x0F, true, false, false, false, out hotkey, out error));
            Assert.False(HotkeyParser.TryCreate(Keys.LButton, true, false, false, false, out hotkey, out error));
        }
    }
}
