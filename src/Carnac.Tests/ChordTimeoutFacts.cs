using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    /// <summary>
    /// A key that may be the first key of a multi-key shortcut is held back. These facts drive the message stream with a
    /// TestScheduler to check that it is released after MessageProvider.ChordTimeout and only then.
    /// </summary>
    public class ChordTimeoutFacts
    {
        const string Editor = "code";
        static readonly string UpArrow = new string((char)8593, 1);
        static readonly long Timeout = MessageProvider.ChordTimeout.Ticks;

        // Like konami.yml, whose chord starts with Up
        static readonly KeyShortcut Konami = new KeyShortcut("Konami!!!",
            new KeyPressDefinition(Keys.Up),
            new KeyPressDefinition(Keys.Up),
            new KeyPressDefinition(Keys.Down),
            new KeyPressDefinition(Keys.Down));

        // Like Ctrl+K,Ctrl+C in vscode.yml
        static readonly KeyShortcut Comment = new KeyShortcut("Add Line Comment",
            new KeyPressDefinition(Keys.K, controlPressed: true),
            new KeyPressDefinition(Keys.C, controlPressed: true));

        readonly TestScheduler scheduler = new TestScheduler();
        readonly Subject<KeyPress> keys = new Subject<KeyPress>();
        readonly List<Message> messages = new List<Message>();

        sealed class StubShortcutProvider : IShortcutProvider
        {
            readonly KeyShortcut[] shortcuts;

            public StubShortcutProvider(params KeyShortcut[] shortcuts)
            {
                this.shortcuts = shortcuts;
            }

            public List<KeyShortcut> GetShortcutsStartingWith(KeyPress keyPress)
            {
                return shortcuts.Where(s => s.StartsWith(new[] { keyPress })).ToList();
            }
        }

        MessageProvider CreateSut(params KeyShortcut[] shortcuts)
        {
            var keyProvider = Substitute.For<IKeyProvider>();
            keyProvider.GetKeyStream().Returns(keys);
            var concurrencyService = Substitute.For<IConcurrencyService>();
            concurrencyService.MainThreadScheduler.Returns(scheduler);
            concurrencyService.Default.Returns(scheduler);
            return new MessageProvider(new StubShortcutProvider(shortcuts), keyProvider, new PopupSettings(), concurrencyService);
        }

        IDisposable Start(params KeyShortcut[] shortcuts)
        {
            return CreateSut(shortcuts).GetMessageStream().Subscribe(messages.Add);
        }

        static string Text(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }

        static KeyPress Press(Keys key, string process = Editor, bool control = false)
        {
            return KeyPresses.Create(process, key, control: control);
        }

        [Fact]
        public void a_single_press_of_the_first_key_of_a_chord_is_shown_after_the_timeout()
        {
            Start(Konami);

            keys.OnNext(Press(Keys.Up));
            Assert.Empty(messages);

            scheduler.AdvanceBy(Timeout - 1);
            Assert.Empty(messages);

            scheduler.AdvanceBy(1);
            var message = Assert.Single(messages);
            Assert.Equal(UpArrow, Text(message));
            Assert.False(message.IsShortcut);
        }

        [Fact]
        public void the_timeout_is_about_one_second()
        {
            Assert.Equal(TimeSpan.FromSeconds(1), MessageProvider.ChordTimeout);
        }

        [Fact]
        public void three_presses_of_the_first_key_are_still_merged_into_one_repeat_count()
        {
            Start(Konami);

            keys.OnNext(Press(Keys.Up));
            keys.OnNext(Press(Keys.Up));
            keys.OnNext(Press(Keys.Up));

            Assert.Equal(UpArrow + " x 3 ", Text(messages.Last()));

            // nothing is left over for the timeout to show
            var count = messages.Count;
            scheduler.AdvanceBy(10 * Timeout);
            Assert.Equal(count, messages.Count);
        }

        [Fact]
        public void a_chord_completed_within_the_timeout_resolves_to_the_shortcut_and_nothing_else()
        {
            Start(Comment);

            keys.OnNext(Press(Keys.K, control: true));
            scheduler.AdvanceBy(Timeout / 2);
            keys.OnNext(Press(Keys.C, control: true));

            var message = Assert.Single(messages);
            Assert.Equal("Add Line Comment", message.ShortcutName);
            Assert.True(message.IsShortcut);
            Assert.Equal("Ctrl + K, Ctrl + C [Add Line Comment]", Text(message));

            scheduler.AdvanceBy(10 * Timeout);
            Assert.Single(messages);
        }

        [Fact]
        public void a_chord_that_stalls_is_shown_as_the_keys_that_were_pressed()
        {
            Start(Comment);

            keys.OnNext(Press(Keys.K, control: true));
            scheduler.AdvanceBy(Timeout);

            var message = Assert.Single(messages);
            Assert.Equal("Ctrl + K", Text(message));
            Assert.Null(message.ShortcutName);
        }

        [Fact]
        public void a_key_after_the_timeout_starts_over_instead_of_completing_the_chord()
        {
            Start(Comment);

            keys.OnNext(Press(Keys.K, control: true));
            scheduler.AdvanceBy(Timeout);
            keys.OnNext(Press(Keys.C, control: true));

            // Ctrl+K was flushed on its own; the late Ctrl+C is a plain key press, not a chord start
            Assert.Equal(new[] { "Ctrl + K", "Ctrl + C" }, messages.Select(Text).ToArray());
            Assert.True(messages.All(m => m.ShortcutName == null));
        }

        [Fact]
        public void every_key_restarts_the_timeout()
        {
            Start(Konami);

            keys.OnNext(Press(Keys.Up));
            scheduler.AdvanceBy(Timeout * 8 / 10);
            keys.OnNext(Press(Keys.Up));
            scheduler.AdvanceBy(Timeout * 8 / 10);

            // 1.6 s after the first key, but only 0.8 s after the last one
            Assert.Empty(messages);

            scheduler.AdvanceBy(Timeout * 2 / 10);
            Assert.Equal(2, messages.Count);
            Assert.Equal(UpArrow + " x 2 ", Text(messages.Last()));
        }

        [Fact]
        public void keys_that_cannot_start_a_chord_are_shown_immediately()
        {
            Start(Konami, Comment);

            keys.OnNext(Press(Keys.A));

            var message = Assert.Single(messages);
            Assert.Equal("a", Text(message));
        }

        [Fact]
        public void a_key_that_breaks_the_chord_shows_all_pressed_keys_immediately()
        {
            Start(Comment);

            keys.OnNext(Press(Keys.K, control: true));
            keys.OnNext(Press(Keys.X));

            Assert.Equal(new[] { "Ctrl + K", "x" }, messages.Select(Text).ToArray());
        }

        [Fact]
        public void a_key_from_another_process_flushes_the_held_keys_and_is_not_lost()
        {
            Start(Konami);

            keys.OnNext(Press(Keys.Up, process: "chrome"));
            keys.OnNext(Press(Keys.X, process: "notepad"));

            Assert.Equal(new[] { UpArrow, "x" }, messages.Select(Text).ToArray());
            Assert.Equal(new[] { "chrome", "notepad" }, messages.Select(m => m.ProcessName).ToArray());

            scheduler.AdvanceBy(10 * Timeout);
            Assert.Equal(2, messages.Count);
        }

        [Fact]
        public void a_key_from_another_process_can_itself_start_a_chord()
        {
            Start(Konami);

            keys.OnNext(Press(Keys.Up, process: "chrome"));
            keys.OnNext(Press(Keys.Up, process: "notepad"));
            Assert.Equal(new[] { "chrome" }, messages.Select(m => m.ProcessName).ToArray());

            scheduler.AdvanceBy(Timeout);
            Assert.Equal(new[] { "chrome", "notepad" }, messages.Select(m => m.ProcessName).ToArray());
        }

        [Fact]
        public void disposing_the_subscription_cancels_the_pending_flush()
        {
            var subscription = Start(Konami);
            keys.OnNext(Press(Keys.Up));

            subscription.Dispose();
            scheduler.AdvanceBy(10 * Timeout);

            Assert.Empty(messages);
        }

        [Fact]
        public void completion_of_the_key_stream_is_forwarded_and_cancels_the_pending_flush()
        {
            var completed = false;
            CreateSut(Konami).GetMessageStream().Subscribe(messages.Add, () => completed = true);
            keys.OnNext(Press(Keys.Up));

            keys.OnCompleted();
            scheduler.AdvanceBy(10 * Timeout);

            Assert.True(completed);
            Assert.Empty(messages);
        }

        [Fact]
        public void errors_of_the_key_stream_are_forwarded()
        {
            Exception error = null;
            CreateSut(Konami).GetMessageStream().Subscribe(messages.Add, e => error = e);
            var failure = new InvalidOperationException("hook failed");

            keys.OnError(failure);

            Assert.Same(failure, error);
        }

        [Fact]
        public void every_subscription_has_its_own_pending_state()
        {
            var sut = CreateSut(Konami);
            var first = new List<Message>();
            var second = new List<Message>();
            sut.GetMessageStream().Subscribe(first.Add);
            sut.GetMessageStream().Subscribe(second.Add);

            keys.OnNext(Press(Keys.Up));
            scheduler.AdvanceBy(Timeout);

            Assert.Equal(1, first.Count);
            Assert.Equal(1, second.Count);
        }

        [Fact]
        public void constructor_requires_all_dependencies()
        {
            var keyProvider = Substitute.For<IKeyProvider>();
            var shortcutProvider = Substitute.For<IShortcutProvider>();
            var concurrencyService = Substitute.For<IConcurrencyService>();
            var settings = new PopupSettings();

            Assert.Equal("shortcutProvider", Assert.Throws<ArgumentNullException>(() => new MessageProvider(null, keyProvider, settings, concurrencyService)).ParamName);
            Assert.Equal("keyProvider", Assert.Throws<ArgumentNullException>(() => new MessageProvider(shortcutProvider, null, settings, concurrencyService)).ParamName);
            Assert.Equal("settings", Assert.Throws<ArgumentNullException>(() => new MessageProvider(shortcutProvider, keyProvider, null, concurrencyService)).ParamName);
            Assert.Equal("concurrencyService", Assert.Throws<ArgumentNullException>(() => new MessageProvider(shortcutProvider, keyProvider, settings, null)).ParamName);
        }
    }
}
