using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using NSubstitute;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class MessageProviderPauseFacts
    {
        readonly Subject<KeyPress> keys = new Subject<KeyPress>();
        readonly KeyDisplayState displayState = new KeyDisplayState();
        readonly List<Message> messages = new List<Message>();
        readonly MessageProvider sut;

        public MessageProviderPauseFacts()
        {
            var shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());
            var keyProvider = Substitute.For<IKeyProvider>();
            keyProvider.GetKeyStream().Returns(keys);

            sut = new MessageProvider(shortcutProvider, keyProvider, new PopupSettings(), displayState);
            sut.GetMessageStream().Subscribe(messages.Add);
        }

        static KeyPress Letter(Keys key, string text)
        {
            return new KeyPress(
                new ProcessInfo("notepad"),
                new InterceptKeyEventArgs(key, KeyDirection.Down, false, false, false),
                false,
                new[] { text });
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }

        [Fact]
        public void constructor_requires_a_display_state()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new MessageProvider(Substitute.For<IShortcutProvider>(), Substitute.For<IKeyProvider>(), new PopupSettings(), null));

            Assert.Equal("displayState", exception.ParamName);
        }

        [Fact]
        public void emits_messages_while_not_paused()
        {
            keys.OnNext(Letter(Keys.A, "a"));

            Assert.Equal(1, messages.Count);
            Assert.Equal("a", TextOf(messages[0]));
        }

        [Fact]
        public void emits_nothing_while_paused()
        {
            displayState.SetPaused(true);

            keys.OnNext(Letter(Keys.A, "a"));
            keys.OnNext(Letter(Keys.B, "b"));

            Assert.Equal(0, messages.Count);
        }

        [Fact]
        public void pausing_takes_effect_immediately()
        {
            keys.OnNext(Letter(Keys.A, "a"));
            displayState.TogglePaused();
            keys.OnNext(Letter(Keys.B, "b"));

            Assert.Equal(1, messages.Count);
            Assert.Equal("a", TextOf(messages[0]));
        }

        [Fact]
        public void resuming_shows_messages_again()
        {
            displayState.TogglePaused();
            keys.OnNext(Letter(Keys.A, "a"));
            displayState.TogglePaused();
            keys.OnNext(Letter(Keys.B, "b"));

            Assert.Equal(1, messages.Count);
            Assert.Equal("b", TextOf(messages[0]));
        }

        [Fact]
        public void keys_typed_while_paused_never_show_up_in_a_later_message()
        {
            keys.OnNext(Letter(Keys.A, "a"));
            displayState.SetPaused(true);
            keys.OnNext(Letter(Keys.S, "s"));
            keys.OnNext(Letter(Keys.E, "e"));
            keys.OnNext(Letter(Keys.C, "c"));
            displayState.SetPaused(false);
            keys.OnNext(Letter(Keys.D, "d"));

            foreach (var message in messages)
            {
                var text = TextOf(message);
                Assert.DoesNotContain("s", text);
                Assert.DoesNotContain("e", text);
                Assert.DoesNotContain("c", text);
            }

            Assert.Equal(2, messages.Count);
            Assert.Equal("ad", TextOf(messages[1]));
        }

        [Fact]
        public void silent_mode_alone_does_not_pause_the_message_pipeline()
        {
            // silent mode is applied earlier, in the key provider, which drops the keys before they get here
            displayState.SetSilent(true);

            keys.OnNext(Letter(Keys.A, "a"));

            Assert.Equal(1, messages.Count);
        }

        [Fact]
        public void the_three_argument_constructor_is_never_paused()
        {
            var otherKeys = new Subject<KeyPress>();
            var keyProvider = Substitute.For<IKeyProvider>();
            keyProvider.GetKeyStream().Returns(otherKeys);
            var shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());
            var received = new List<Message>();
            new MessageProvider(shortcutProvider, keyProvider, new PopupSettings()).GetMessageStream().Subscribe(received.Add);

            otherKeys.OnNext(Letter(Keys.A, "a"));

            Assert.Equal(1, received.Count);
        }
    }
}
