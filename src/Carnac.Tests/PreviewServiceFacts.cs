using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Subjects;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Reactive.Testing;
using NSubstitute;
using SettingsProviderNet;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class PreviewServiceFacts
    {
        static readonly ProcessInfo SampleProcess = new ProcessInfo("Carnac");

        static Message TypedMessage(string text)
        {
            var key = new KeyPress(
                new ProcessInfo("notepad"),
                new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false),
                false,
                new[] { text });
            return new Message(key);
        }

        static string TextOf(Message message)
        {
            return string.Join(string.Empty, message.Text);
        }

        public class when_the_preview_is_shown
        {
            readonly ObservableCollection<Message> messages = new ObservableCollection<Message>();

            [Fact]
            public void a_shortcut_with_its_description_and_a_line_of_text_are_pinned()
            {
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show();

                Assert.Equal(2, messages.Count);
                var texts = messages.Select(TextOf).ToList();
                Assert.True(texts.Contains("Ctrl + Shift + P"), string.Join(" | ", texts));
                Assert.True(messages.Any(m => m.ShortcutName == "Command Palette"));
                Assert.True(texts.Contains("Preview: type anything"), string.Join(" | ", texts));
            }

            [Fact]
            public void the_sample_messages_come_from_the_sample_process_and_are_not_fading()
            {
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show();

                Assert.True(messages.All(m => m.ProcessName == "Carnac"));
                Assert.True(messages.All(m => !m.IsDeleting));
            }

            [Fact]
            public void the_shortcut_is_flagged_as_a_shortcut_with_its_name()
            {
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show();

                var shortcut = messages.Single(m => m.IsShortcut);
                Assert.Equal("Command Palette", shortcut.ShortcutName);
            }

            [Fact]
            public void messages_that_are_already_there_stay()
            {
                var typed = TypedMessage("x");
                messages.Add(typed);
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show();

                Assert.Equal(3, messages.Count);
                Assert.True(messages.Any(m => ReferenceEquals(m, typed)));
            }
        }

        public class when_the_preview_is_hidden
        {
            readonly ObservableCollection<Message> messages = new ObservableCollection<Message>();

            [Fact]
            public void the_pinned_messages_are_removed_and_nothing_else()
            {
                var before = TypedMessage("a");
                messages.Add(before);
                var subject = new PreviewService(messages, SampleProcess);

                var visit = subject.Show();
                var during = TypedMessage("b");
                messages.Add(during);
                visit.Dispose();

                Assert.Equal(2, messages.Count);
                Assert.True(messages.Any(m => ReferenceEquals(m, before)));
                Assert.True(messages.Any(m => ReferenceEquals(m, during)));
            }

            [Fact]
            public void nothing_is_left_when_nothing_else_was_there()
            {
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show().Dispose();

                Assert.Equal(0, messages.Count);
            }

            [Fact]
            public void showing_it_again_pins_a_fresh_set()
            {
                var subject = new PreviewService(messages, SampleProcess);
                subject.Show().Dispose();

                subject.Show();

                Assert.Equal(2, messages.Count);
            }

            [Fact]
            public void a_message_that_was_removed_by_someone_else_does_not_break_hiding()
            {
                var subject = new PreviewService(messages, SampleProcess);
                var visit = subject.Show();
                messages.RemoveAt(0);

                visit.Dispose();

                Assert.Equal(0, messages.Count);
            }
        }

        public class when_the_preview_is_shown_more_than_once
        {
            readonly ObservableCollection<Message> messages = new ObservableCollection<Message>();

            [Fact]
            public void the_messages_are_not_duplicated()
            {
                var subject = new PreviewService(messages, SampleProcess);

                subject.Show();
                subject.Show();

                Assert.Equal(2, messages.Count);
            }

            [Fact]
            public void the_messages_stay_until_the_last_visitor_leaves()
            {
                var subject = new PreviewService(messages, SampleProcess);
                var first = subject.Show();
                var second = subject.Show();

                first.Dispose();
                Assert.Equal(2, messages.Count);

                second.Dispose();
                Assert.Equal(0, messages.Count);
            }

            [Fact]
            public void leaving_twice_with_the_same_token_does_not_take_the_messages_from_the_other_visitor()
            {
                var subject = new PreviewService(messages, SampleProcess);
                var first = subject.Show();
                subject.Show();

                first.Dispose();
                first.Dispose();

                Assert.Equal(2, messages.Count);
            }
        }

        public class when_messages_are_typed_while_the_preview_is_shown
        {
            [Fact]
            public void the_pinned_messages_do_not_fade_out_and_are_not_removed_like_typed_ones()
            {
                var scheduler = new TestScheduler();
                var messages = new ObservableCollection<Message>();
                var stream = new Subject<Message>();
                var messageProvider = Substitute.For<IMessageProvider>();
                messageProvider.GetMessageStream().Returns(stream);
                var concurrencyService = Substitute.For<IConcurrencyService>();
                concurrencyService.MainThreadScheduler.Returns(scheduler);
                concurrencyService.Default.Returns(scheduler);
                var settingsProvider = Substitute.For<ISettingsProvider>();
                settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings { ItemFadeDelay = 5 });
                var preview = new PreviewService(messages, SampleProcess);
                var controller = new KeysController(messages, messageProvider, concurrencyService, settingsProvider);
                controller.Start();
                preview.Show();

                stream.OnNext(TypedMessage("typed"));
                scheduler.AdvanceBy(TimeSpan.FromMilliseconds(1).Ticks);
                Assert.Equal(3, messages.Count);

                scheduler.AdvanceBy(TimeSpan.FromSeconds(5.5).Ticks);
                Assert.True(messages.Single(m => TextOf(m) == "typed").IsDeleting);
                Assert.True(messages.Where(m => m.ProcessName == "Carnac").All(m => !m.IsDeleting));

                scheduler.AdvanceBy(TimeSpan.FromSeconds(30).Ticks);
                Assert.Equal(2, messages.Count);
                Assert.True(messages.All(m => m.ProcessName == "Carnac" && !m.IsDeleting));

                controller.Dispose();
            }
        }

        public class when_creating_the_service
        {
            [Fact]
            public void the_collection_and_the_process_are_required()
            {
                Assert.Throws<ArgumentNullException>(() => new PreviewService(null, SampleProcess));
                Assert.Throws<ArgumentNullException>(() => new PreviewService(new ObservableCollection<Message>(), null));
            }

            [Fact]
            public void the_sample_messages_need_a_process()
            {
                Assert.Throws<ArgumentNullException>(() => PreviewService.CreateSampleMessages(null));
            }

            [Fact]
            public void the_sample_messages_are_the_same_every_time()
            {
                var first = PreviewService.CreateSampleMessages(SampleProcess).Select(TextOf).ToList();
                var second = PreviewService.CreateSampleMessages(SampleProcess).Select(TextOf).ToList();

                Assert.Equal(first, second);
            }

            [Fact]
            public void the_plain_text_is_not_collapsed_into_repeat_counts()
            {
                var text = PreviewService.CreateSampleMessages(SampleProcess).Select(TextOf).Single(t => t.StartsWith("Preview"));

                Assert.Equal("Preview: type anything", text);
            }
        }
    }
}
