using System.Windows.Forms;
using Carnac.Logic;
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

        Message CreateShortcutMessage(string shortcutName)
        {
            var controlF = new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.F, KeyDirection.Down, false, true, false), false, new[] { "Control", "F" });
            return new Message(new[] { controlF }, new KeyShortcut(shortcutName), true);
        }

        [Fact]
        public void shortcut_name_is_not_part_of_the_text()
        {
            // act
            var result = CreateShortcutMessage("Open the Find Bar");

            // assert
            Assert.Equal("Control + F", string.Join(string.Empty, result.Text));
            Assert.Equal("Open the Find Bar", result.ShortcutName);
            Assert.True(result.HasShortcutName);
        }

        [Fact]
        public void shortcut_name_and_plain_text_survive_fade_out()
        {
            // arrange
            var message = CreateShortcutMessage("Open the Find Bar");

            // act
            var result = message.FadeOut();

            // assert
            Assert.True(result.IsDeleting);
            Assert.Equal("Control + F", string.Join(string.Empty, result.Text));
            Assert.Equal("Open the Find Bar", result.ShortcutName);
        }

        [Fact]
        public void shortcut_without_a_name_has_no_shortcut_name()
        {
            Assert.False(CreateShortcutMessage(null).HasShortcutName);
            Assert.False(CreateShortcutMessage(string.Empty).HasShortcutName);
            Assert.Equal("Control + F", string.Join(string.Empty, CreateShortcutMessage(string.Empty).Text));
        }

        [Fact]
        public void message_that_is_not_a_shortcut_has_no_shortcut_name()
        {
            // act
            var result = new Message(new KeyPress(fakeProcess, new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false), false, new[] { "a" }));

            // assert
            Assert.Null(result.ShortcutName);
            Assert.False(result.HasShortcutName);
            Assert.Equal("a", string.Join(string.Empty, result.Text));
        }
    }
}