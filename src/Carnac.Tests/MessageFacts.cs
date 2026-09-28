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
        public void typed_digits_and_full_stops_follow_the_same_rule()
        {
            Assert.Equal("1000", TextOf(Typed("1", "0", "0", "0")));
            Assert.Equal("...", TextOf(Typed(".", ".", ".")));
            Assert.Equal(". x 4 ", TextOf(Typed(".", ".", ".", ".")));
        }

        [Fact]
        public void repeated_spaces_are_still_summarised()
        {
            Assert.Equal("  x 2 ", TextOf(Typed(" ", " ")));
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