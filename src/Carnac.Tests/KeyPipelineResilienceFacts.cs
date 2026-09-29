using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Reactive.Testing;
using Microsoft.Win32;
using NSubstitute;
using SettingsProviderNet;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    /// <summary>What happens to the key stream when the process lookup, the process filter or a later step fails.</summary>
    public class KeyProviderResilienceFacts : IDisposable
    {
        readonly Subject<InterceptKeyEventArgs> source = new Subject<InterceptKeyEventArgs>();
        readonly IProcessProvider processProvider = Substitute.For<IProcessProvider>();
        readonly ILogger logger = Substitute.For<ILogger>();
        readonly Process currentProcess = Process.GetCurrentProcess();
        readonly List<KeyPress> keys = new List<KeyPress>();
        readonly List<Exception> streamErrors = new List<Exception>();

        public void Dispose()
        {
            currentProcess.Dispose();
        }

        KeyProvider CreateProvider(PopupSettings settings = null)
        {
            var interceptKeys = Substitute.For<IInterceptKeys>();
            interceptKeys.GetKeyStream().Returns(source);
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(settings ?? new PopupSettings());
            var provider = new KeyProvider(interceptKeys, new PasswordModeService(new KeyDisplayState(), new PopupSettings()),
                desktopLockEventService, settingsProvider, processProvider, logger);
            provider.GetKeyStream().Subscribe(keys.Add, streamErrors.Add);
            return provider;
        }

        void Press(Keys key)
        {
            source.OnNext(new InterceptKeyEventArgs(key, KeyDirection.Down, false, false, false));
            source.OnNext(new InterceptKeyEventArgs(key, KeyDirection.Up, false, false, false));
        }

        [Fact]
        public void a_key_press_of_the_foreground_process_is_delivered_with_its_name()
        {
            processProvider.GetAssociatedProcess().Returns(currentProcess);
            CreateProvider();

            Press(Keys.A);

            Assert.Equal(1, keys.Count);
            Assert.Equal(currentProcess.ProcessName, keys[0].Process.ProcessName);
            Assert.Equal(new[] { "a" }, keys[0].Input.ToArray());
        }

        [Fact]
        public void a_process_lookup_that_throws_is_logged_and_the_next_key_is_still_delivered()
        {
            processProvider.GetAssociatedProcess().Returns(
                call => { throw new InvalidOperationException("the process is gone"); },
                call => currentProcess);
            CreateProvider();

            Press(Keys.A);
            Press(Keys.B);

            Assert.Equal(1, keys.Count);
            Assert.Equal(new[] { "b" }, keys[0].Input.ToArray());
            Assert.Empty(streamErrors);
            logger.Received(1).Log(LogLevel.Error, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "the process is gone"));
        }

        [Fact]
        public void a_process_that_exited_before_its_name_was_read_is_logged_and_the_next_key_is_still_delivered()
        {
            using (var exited = AssociatedProcessUtilitiesFacts.StartAndWaitForExit())
            {
                processProvider.GetAssociatedProcess().Returns(exited, currentProcess);
                CreateProvider();

                Press(Keys.A);
                Press(Keys.B);

                Assert.Equal(1, keys.Count);
                Assert.Equal(new[] { "b" }, keys[0].Input.ToArray());
                Assert.Empty(streamErrors);
                logger.Received(1).Log(LogLevel.Error, Arg.Any<string>(), Arg.Any<Exception>());
            }
        }

        [Fact]
        public void many_failures_in_a_row_never_end_the_stream()
        {
            var lookups = 0;
            processProvider.GetAssociatedProcess().Returns(call =>
            {
                if (++lookups <= 100)
                    throw new InvalidOperationException("still failing");
                return currentProcess;
            });
            CreateProvider();

            for (var i = 0; i < 101; i++)
                Press(Keys.A);

            Assert.Equal(1, keys.Count);
            Assert.Empty(streamErrors);
        }

        [Fact]
        public void no_foreground_process_means_no_key_and_no_error()
        {
            processProvider.GetAssociatedProcess().Returns((Process)null);
            CreateProvider();

            Press(Keys.A);

            Assert.Empty(keys);
            logger.DidNotReceiveWithAnyArgs().Log(LogLevel.Error, null, null);
        }

        [Fact]
        public void a_process_filter_still_applies()
        {
            processProvider.GetAssociatedProcess().Returns(currentProcess);
            CreateProvider(new PopupSettings { ProcessFilterExpression = "^no-such-process-name$" });

            Press(Keys.A);

            Assert.Empty(keys);
        }

        [Fact]
        public void a_matching_process_filter_lets_keys_through()
        {
            processProvider.GetAssociatedProcess().Returns(currentProcess);
            CreateProvider(new PopupSettings { ProcessFilterExpression = System.Text.RegularExpressions.Regex.Escape(currentProcess.ProcessName) });

            Press(Keys.A);

            Assert.Equal(1, keys.Count);
        }

        [Fact]
        public void an_invalid_process_filter_is_logged_once_and_ignored()
        {
            processProvider.GetAssociatedProcess().Returns(currentProcess);
            CreateProvider(new PopupSettings { ProcessFilterExpression = "(" });

            Press(Keys.A);
            Press(Keys.B);

            Assert.Equal(2, keys.Count);
            logger.Received(1).Log(LogLevel.Warning, Arg.Is<string>(text => text.Contains("(")), Arg.Any<ArgumentException>());
        }

        [Fact]
        public void constructor_requires_a_process_provider_and_a_logger()
        {
            var interceptKeys = Substitute.For<IInterceptKeys>();
            var passwordModeService = Substitute.For<IPasswordModeService>();
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            var settingsProvider = Substitute.For<ISettingsProvider>();

            var processException = Assert.Throws<ArgumentNullException>(() =>
                new KeyProvider(interceptKeys, passwordModeService, desktopLockEventService, settingsProvider, null, logger));
            var loggerException = Assert.Throws<ArgumentNullException>(() =>
                new KeyProvider(interceptKeys, passwordModeService, desktopLockEventService, settingsProvider, processProvider, null));

            Assert.Equal("processProvider", processException.ParamName);
            Assert.Equal("logger", loggerException.ParamName);
        }
    }

    public class KeysControllerResilienceFacts : IDisposable
    {
        readonly TestScheduler scheduler = new TestScheduler();
        readonly ObservableCollection<Message> messages = new ObservableCollection<Message>();
        readonly ILogger logger = Substitute.For<ILogger>();
        readonly Process currentProcess = Process.GetCurrentProcess();

        public void Dispose()
        {
            currentProcess.Dispose();
        }

        KeysController CreateController(IMessageProvider messageProvider)
        {
            var concurrencyService = Substitute.For<IConcurrencyService>();
            concurrencyService.MainThreadScheduler.Returns(scheduler);
            concurrencyService.Default.Returns(scheduler);
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings { ItemFadeDelay = 5 });
            return new KeysController(messages, messageProvider, concurrencyService, settingsProvider, logger);
        }

        static KeyPress Letter(Keys key, string text)
        {
            return new KeyPress(new ProcessInfo("notepad"), new InterceptKeyEventArgs(key, KeyDirection.Down, false, false, false), false, new[] { text });
        }

        [Fact]
        public void a_failing_message_stream_is_logged_and_started_again_after_a_short_pause()
        {
            var attempts = 0;
            var good = new Subject<Message>();
            var messageProvider = Substitute.For<IMessageProvider>();
            messageProvider.GetMessageStream().Returns(call =>
            {
                attempts++;
                return attempts == 1 ? Observable.Throw<Message>(new InvalidOperationException("bad key")) : (IObservable<Message>)good;
            });
            var sut = CreateController(messageProvider);

            sut.Start();

            Assert.Equal(1, attempts);
            logger.Received(1).Log(LogLevel.Error, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "bad key"));

            scheduler.AdvanceBy(KeysController.RestartDelay.Ticks);
            Assert.Equal(2, attempts);

            var message = new Message(Letter(Keys.A, "a"));
            good.OnNext(message);
            scheduler.AdvanceBy(1000);
            Assert.Contains(message, messages);
        }

        [Fact]
        public void an_exception_while_handling_one_key_is_logged_and_the_next_key_is_still_displayed()
        {
            // the whole production pipeline: hook stream -> key provider -> message provider -> keys controller
            var hookKeys = new Subject<InterceptKeyEventArgs>();
            var interceptKeys = Substitute.For<IInterceptKeys>();
            interceptKeys.GetKeyStream().Returns(hookKeys);
            var desktopLockEventService = Substitute.For<IDesktopLockEventService>();
            desktopLockEventService.GetSessionSwitchStream().Returns(Observable.Never<SessionSwitchEventArgs>());
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings());
            var processProvider = Substitute.For<IProcessProvider>();
            processProvider.GetAssociatedProcess().Returns(currentProcess);
            var shortcutProvider = Substitute.For<IShortcutProvider>();
            shortcutProvider.GetShortcutsStartingWith(Arg.Any<KeyPress>()).Returns(new List<KeyShortcut>());

            // a step behind the key provider's own try/catch blows up on the first key
            var passwordModeService = Substitute.For<IPasswordModeService>();
            passwordModeService.CheckPasswordMode(Arg.Any<InterceptKeyEventArgs>()).Returns(
                call => { throw new InvalidOperationException("unexpected failure while handling a key"); },
                call => false);
            var keyProvider = new KeyProvider(interceptKeys, passwordModeService, desktopLockEventService, settingsProvider, processProvider, logger);
            var messageProvider = new MessageProvider(shortcutProvider, keyProvider, new PopupSettings());
            var sut = CreateController(messageProvider);
            sut.Start();

            hookKeys.OnNext(new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, false, false));
            logger.Received(1).Log(LogLevel.Error, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message.Contains("unexpected failure")));

            scheduler.AdvanceBy(KeysController.RestartDelay.Ticks);
            hookKeys.OnNext(new InterceptKeyEventArgs(Keys.B, KeyDirection.Down, false, false, false));
            scheduler.AdvanceBy(1000);

            Assert.Equal(1, messages.Count);
            Assert.Equal("b", string.Join(string.Empty, messages[0].Text));
        }

        [Fact]
        public void the_controller_without_a_logger_still_works()
        {
            var good = new Subject<Message>();
            var messageProvider = Substitute.For<IMessageProvider>();
            messageProvider.GetMessageStream().Returns(good);
            var concurrencyService = Substitute.For<IConcurrencyService>();
            concurrencyService.MainThreadScheduler.Returns(scheduler);
            concurrencyService.Default.Returns(scheduler);
            var settingsProvider = Substitute.For<ISettingsProvider>();
            settingsProvider.GetSettings<PopupSettings>().Returns(new PopupSettings { ItemFadeDelay = 5 });
            var sut = new KeysController(messages, messageProvider, concurrencyService, settingsProvider);
            sut.Start();

            var message = new Message(Letter(Keys.A, "a"));
            good.OnNext(message);
            scheduler.AdvanceBy(1000);

            Assert.Contains(message, messages);
        }

        [Fact]
        public void constructor_requires_a_logger()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new KeysController(messages, Substitute.For<IMessageProvider>(), Substitute.For<IConcurrencyService>(), Substitute.For<ISettingsProvider>(), null));

            Assert.Equal("logger", exception.ParamName);
        }
    }
}
