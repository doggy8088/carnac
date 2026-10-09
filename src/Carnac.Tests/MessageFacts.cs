using System.Linq;
using System.Windows.Forms;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class MessageFacts
    {
        readonly ProcessInfo fakeProcess = new ProcessInfo("FakeProcess");

        [Fact]
        public void message_does_not_group_different_letters()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "b" })));

            // assert
            var expected = string.Join(string.Empty, result.Text);
            Assert.Equal("ab", expected);
        }

        [Fact]
        public void message_does_not_group_letter_and_backspace()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" })));

            // assert
            Assert.Equal("aBack", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_groups_multiple_backspace_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Back, KeyDirection.Down, false, false, false), false, new[] { "Back" })));

            // assert
            Assert.Equal("Back x 2 ", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_does_not_group_different_arrow_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Up, KeyDirection.Down, false, false, false), false, new[] { "Up" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↑↓", string.Join(string.Empty, result.Text));
        }
        
        [Fact]
        public void message_groups_two_equal_arrow_key_presses_together()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↓ x 2 ", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void message_groups_three_equal_arrow_key_presses_together()
        {
            // arrange
            var result = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" }))
                .Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })))
                .Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.Down, KeyDirection.Down, false, false, false), false, new[] { "Down" })));

            // assert
            Assert.Equal("↓ x 3 ", string.Join(string.Empty, result.Text));
        }

        static Message MessageFor(Keys key, bool shift = false, bool control = false, bool alt = false)
        {
            return new Message(KeyPresses.Create("FakeProcess", key, control, shift, alt));
        }

        [Fact]
        public void shift_with_a_key_that_types_nothing_is_a_modifier_message_that_is_not_merged()
        {
            foreach (var key in new[] { Keys.Enter, Keys.Tab, Keys.F5, Keys.Left, Keys.Home, Keys.Delete, Keys.Insert })
            {
                var message = MessageFor(key, shift: true);

                Assert.True(message.IsModifier, "Shift+" + key + " should count as a modifier message");
                Assert.False(message.CanBeMerged, "Shift+" + key + " should not be merged into typed text");
            }
        }

        [Fact]
        public void shift_with_a_letter_or_digit_is_typing_and_not_a_modifier_message()
        {
            foreach (var key in new[] { Keys.A, Keys.D1, Keys.Oemcomma, Keys.Space })
            {
                var message = MessageFor(key, shift: true);

                Assert.False(message.IsModifier, "Shift+" + key + " is typing");
                Assert.True(message.CanBeMerged, "Shift+" + key + " is typing");
            }
        }

        [Fact]
        public void plain_enter_and_plain_letters_are_not_modifier_messages()
        {
            Assert.False(MessageFor(Keys.Enter).IsModifier);
            Assert.False(MessageFor(Keys.A).IsModifier);
        }

        [Fact]
        public void ctrl_alt_and_windows_are_still_modifier_messages()
        {
            Assert.True(MessageFor(Keys.S, control: true).IsModifier);
            Assert.True(MessageFor(Keys.Left, alt: true).IsModifier);
            Assert.True(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.E, KeyDirection.Down, false, false, false), true, new[] { "Win", "E" })).IsModifier);
        }

        [Fact]
        public void shift_tab_is_shown_as_its_own_message_not_merged_into_typed_text()
        {
            var typed = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            var result = Message.MergeIfNeeded(typed, MessageFor(Keys.Tab, shift: true));

            Assert.Equal("Shift + Tab", string.Join(string.Empty, result.Text));
        }

        [Fact]
        public void multiple_shortcuts_have_comma_inserted_between_input()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.T, KeyDirection.Down, false, true, false), false, new[] { "Control", "T" })));

            // assert
            Assert.Equal("Control + R, Control + T", string.Join(string.Empty, result.Text));
        }
        
        [Fact]
        public void multiple_shortcuts_duplicate_shortcuts_are_grouped()
        {
            // arrange
            var message = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            // act
            var result = message.Merge(new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" })));

            // assert
            string actual = string.Join(string.Empty, result.Text);

            Assert.Equal("Control + R x 2 ", actual);
        }

        [Fact]
        public void typed_letters_repeated_twice_are_not_summarised()
        {
            Assert.Equal("hello", TextOf(Typed("h", "e", "l", "l", "o")));
            Assert.Equal("AA", TextOf(Typed("A", "A")));
        }

        [Fact]
        public void typed_letters_repeated_three_times_are_not_summarised()
        {
            Assert.Equal("www", TextOf(Typed("w", "w", "w")));
        }

        [Fact]
        public void typed_letters_repeated_four_times_are_summarised()
        {
            Assert.Equal("l x 4 ", TextOf(Typed("l", "l", "l", "l")));
        }

        [Fact]
        public void summarised_typed_letters_keep_counting()
        {
            Assert.Equal("l x 6 ", TextOf(Typed("l", "l", "l", "l", "l", "l")));
        }

        [Fact]
        public void summarised_typed_letters_are_followed_by_the_next_key()
        {
            Assert.Equal("a x 4 b", TextOf(Typed("a", "a", "a", "a", "b")));
            Assert.Equal("a x 4 bbb", TextOf(Typed("a", "a", "a", "a", "b", "b", "b")));
        }

        [Fact]
        public void typed_full_stops_follow_the_same_rule()
        {
            Assert.Equal("...", TextOf(Typed(".", ".", ".")));
            Assert.Equal(". x 4 ", TextOf(Typed(".", ".", ".", ".")));
        }

        [Fact]
        public void typed_digits_are_only_summarised_from_ten_in_a_row()
        {
            Assert.Equal("1000", TextOf(Typed("1", "0", "0", "0")));
            Assert.Equal("1000000", TextOf(Typed("1", "0", "0", "0", "0", "0", "0")));
            Assert.Equal("1000000000", TextOf(Typed("1", "0", "0", "0", "0", "0", "0", "0", "0", "0")));
            Assert.Equal("0 x 10 ", TextOf(Typed("0", "0", "0", "0", "0", "0", "0", "0", "0", "0")));
        }

        [Fact]
        public void typed_punctuation_and_symbols_follow_the_same_rule()
        {
            Assert.Equal("foo((x))", TextOf(Typed("f", "o", "o", "(", "(", "x", ")", ")")));
            Assert.Equal("a == b", TextOf(Typed("a", " ", "=", "=", " ", "b")));
            Assert.Equal("===", TextOf(Typed("=", "=", "=")));
            Assert.Equal("= x 4 ", TextOf(Typed("=", "=", "=", "=")));
            Assert.Equal("&&", TextOf(Typed("&", "&")));
        }

        [Fact]
        public void typed_uppercase_letters_follow_the_same_rule()
        {
            Assert.Equal("AAA", TextOf(Typed("A", "A", "A")));
            Assert.Equal("A x 4 ", TextOf(Typed("A", "A", "A", "A")));
        }

        [Fact]
        public void typed_characters_outside_ascii_follow_the_same_rule()
        {
            var surrogatePairLetter = "\U0001D49C";

            Assert.Equal("\u00fc\u00fc", TextOf(Typed("\u00fc", "\u00fc")));
            Assert.Equal(surrogatePairLetter + surrogatePairLetter, TextOf(Typed(surrogatePairLetter, surrogatePairLetter)));
            Assert.Equal(surrogatePairLetter + " x 4 ", TextOf(Typed(surrogatePairLetter, surrogatePairLetter, surrogatePairLetter, surrogatePairLetter)));
        }

        [Fact]
        public void named_keys_are_still_summarised_from_two()
        {
            Assert.Equal("Return x 2 ", TextOf(Typed("Return", "Return")));
            Assert.Equal("Tab x 2 ", TextOf(Typed("Tab", "Tab")));
            Assert.Equal("F5 x 2 ", TextOf(Typed("F5", "F5")));
            Assert.Equal("del x 2 ", TextOf(Typed("del", "del")));
        }

        [Fact]
        public void typed_spaces_follow_the_same_rule()
        {
            Assert.Equal("end.  Next", TextOf(Typed("e", "n", "d", ".", " ", " ", "N", "e", "x", "t")));
            Assert.Equal("   ", TextOf(Typed(" ", " ", " ")));
            Assert.Equal("  x 4 ", TextOf(Typed(" ", " ", " ", " ")));
        }

        [Fact]
        public void typed_numpad_operators_follow_the_same_rule()
        {
            // the numpad operators are padded with spaces to read well in a sentence
            Assert.Equal(" +  + ", TextOf(Typed(" + ", " + ")));
            Assert.Equal(" + " + " x 4 ", TextOf(Typed(" + ", " + ", " + ", " + ")));
        }

        [Fact]
        public void a_short_run_before_a_long_run_of_the_same_character_is_counted_separately()
        {
            Assert.Equal("aaba x 4 ", TextOf(Typed("a", "a", "b", "a", "a", "a", "a")));
        }

        [Fact]
        public void repeated_typed_letters_after_a_shortcut_keep_their_separator()
        {
            var shortcut = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.R, KeyDirection.Down, false, true, false), false, new[] { "Control", "R" }));

            var result = shortcut.Merge(Typed("l", "l"));

            Assert.Equal("Control + R, ll", TextOf(result));
        }

        [Fact]
        public void repeated_letters_with_a_modifier_are_still_summarised()
        {
            var press = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, false), false, new[] { "Control", "L" });

            var result = new Message(press).Merge(new Message(press));

            Assert.Equal("Control + L x 2 ", TextOf(result));
        }

        [Fact]
        public void a_character_typed_with_altgr_is_a_typed_character()
        {
            // AltGr is reported as Ctrl+Alt, the character is what is typed
            var press = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.D7, KeyDirection.Down, true, true, false), false, new[] { "{" });

            var result = new Message(press).Merge(new Message(press)).Merge(new Message(press));

            Assert.Equal("{{{", TextOf(result));
        }

        [Fact]
        public void a_run_does_not_mix_typed_characters_with_the_same_text_pressed_with_a_modifier()
        {
            var typed = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, false, false), false, new[] { "l" });
            var withControl = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, false), false, new[] { "l" });

            var typedFirst = new Message(typed).Merge(new Message(typed)).Merge(new Message(typed)).Merge(new Message(withControl));
            var controlFirst = new Message(withControl).Merge(new Message(typed)).Merge(new Message(typed)).Merge(new Message(typed));

            // the Ctrl press is a group of its own, so it neither joins nor changes how the typed run is shown
            Assert.Equal("llll", TextOf(typedFirst));
            Assert.Equal("l, lll", TextOf(controlFirst));
        }

        // The key is irrelevant to the display text here, only the text of the input matters.
        Message Typed(params string[] texts)
        {
            return texts
                .Select(text => new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { text })))
                .Aggregate((merged, next) => merged.Merge(next));
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }
    }
}