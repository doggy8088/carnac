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
    }
}