using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using NSubstitute;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class ShortcutAccumulatorFacts
    {
        const string Process = "code";

        readonly IShortcutProvider shortcutProvider = Substitute.For<IShortcutProvider>();

        public ShortcutAccumulatorFacts()
        {
            var chord = new KeyShortcut("Comment",
                new KeyPressDefinition(Keys.K, controlPressed: true),
                new KeyPressDefinition(Keys.C, controlPressed: true));
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>())
                .Returns(call => new[] { chord }.Where(s => s.StartsWith(new[] { call.Arg<KeyPress>() })).ToList());
        }

        static KeyPress CtrlK(string process = Process)
        {
            return KeyPresses.Create(process, Keys.K, control: true);
        }

        static string Text(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }

        [Fact]
        public void first_key_of_a_chord_leaves_the_accumulator_pending()
        {
            var sut = new ShortcutAccumulator().ProcessKey(shortcutProvider, CtrlK());

            Assert.False(sut.HasCompletedValue);
        }

        [Fact]
        public void flush_turns_a_pending_chord_into_one_message_per_key()
        {
            var sut = new ShortcutAccumulator().ProcessKey(shortcutProvider, CtrlK());

            sut.Flush();

            Assert.True(sut.HasCompletedValue);
            var message = Assert.Single(sut.GetMessages());
            Assert.Equal("Ctrl + K", Text(message));
            Assert.False(message.IsShortcut);
            Assert.Null(message.ShortcutName);
        }

        [Fact]
        public void flush_keeps_every_key_that_was_buffered()
        {
            var sut = new ShortcutAccumulator();
            var longerChord = new KeyShortcut("Three keys",
                new KeyPressDefinition(Keys.K, controlPressed: true),
                new KeyPressDefinition(Keys.A),
                new KeyPressDefinition(Keys.B));
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut> { longerChord });

            sut.ProcessKey(shortcutProvider, CtrlK());
            sut.ProcessKey(shortcutProvider, KeyPresses.Create(Process, Keys.A));
            Assert.False(sut.HasCompletedValue);
            sut.Flush();

            Assert.Equal(new[] { "Ctrl + K", "a" }, sut.GetMessages().Select(Text).ToArray());
        }

        [Fact]
        public void flush_does_nothing_when_no_key_is_buffered()
        {
            var sut = new ShortcutAccumulator();

            sut.Flush();

            Assert.False(sut.HasCompletedValue);
        }

        [Fact]
        public void flush_does_not_change_a_completed_shortcut()
        {
            var sut = new ShortcutAccumulator()
                .ProcessKey(shortcutProvider, CtrlK())
                .ProcessKey(shortcutProvider, KeyPresses.Create(Process, Keys.C, control: true));
            var before = sut.GetMessages();

            sut.Flush();

            Assert.Same(before, sut.GetMessages());
            Assert.Equal("Comment", sut.GetMessages().Single().ShortcutName);
        }

        [Fact]
        public void flush_does_not_change_a_key_that_never_was_pending()
        {
            var sut = new ShortcutAccumulator().ProcessKey(shortcutProvider, KeyPresses.Create(Process, Keys.A));
            var before = sut.GetMessages();

            sut.Flush();

            Assert.Same(before, sut.GetMessages());
        }

        [Fact]
        public void next_key_after_a_flush_starts_a_new_accumulator()
        {
            var flushed = new ShortcutAccumulator().ProcessKey(shortcutProvider, CtrlK());
            flushed.Flush();

            var next = flushed.ProcessKey(shortcutProvider, KeyPresses.Create(Process, Keys.C, control: true));

            Assert.NotSame(flushed, next);
            Assert.Equal("Ctrl + K", Text(flushed.GetMessages().Single()));
            Assert.Equal("Ctrl + C", Text(next.GetMessages().Single()));
        }

        [Fact]
        public void pending_accumulator_knows_when_a_key_comes_from_another_process()
        {
            var sut = new ShortcutAccumulator().ProcessKey(shortcutProvider, CtrlK());

            Assert.True(sut.IsPendingForOtherProcess(KeyPresses.Create("notepad", Keys.A)));
            Assert.False(sut.IsPendingForOtherProcess(KeyPresses.Create(Process, Keys.A)));
        }

        [Fact]
        public void accumulator_without_pending_keys_is_never_pending_for_another_process()
        {
            var empty = new ShortcutAccumulator();
            var completed = new ShortcutAccumulator().ProcessKey(shortcutProvider, KeyPresses.Create(Process, Keys.A));

            Assert.False(empty.IsPendingForOtherProcess(KeyPresses.Create("notepad", Keys.A)));
            Assert.False(completed.IsPendingForOtherProcess(KeyPresses.Create("notepad", Keys.A)));
        }
    }
}
