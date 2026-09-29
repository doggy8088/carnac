using System;
using System.Windows.Forms;
using Carnac.Logic;
using NSubstitute;
using Xunit;

namespace Carnac.Tests
{
    public class ErrorNotifyingLoggerFacts
    {
        readonly ILogger inner = Substitute.For<ILogger>();
        readonly ITrayNotifier notifier = Substitute.For<ITrayNotifier>();
        DateTime now = new DateTime(2026, 3, 14, 10, 0, 0);
        int folderOpened;

        ErrorNotifyingLogger CreateLogger(int threshold = 3)
        {
            var logger = new ErrorNotifyingLogger(inner, () => folderOpened++, () => now, threshold, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));
            logger.SetNotifier(notifier);
            return logger;
        }

        static readonly Exception Failure = new InvalidOperationException("boom");

        void AssertNoBalloon()
        {
            notifier.DidNotReceiveWithAnyArgs().ShowBalloon(null, null, ToolTipIcon.None, null);
        }

        [Fact]
        public void passes_every_entry_on_to_the_inner_logger()
        {
            var sut = CreateLogger();

            sut.Info("info");
            sut.Warn("warning");
            sut.Error("error", Failure);

            inner.Received().Log(LogLevel.Info, "info", null);
            inner.Received().Log(LogLevel.Warning, "warning", null);
            inner.Received().Log(LogLevel.Error, "error", Failure);
        }

        [Fact]
        public void shows_one_balloon_once_enough_errors_happened()
        {
            var sut = CreateLogger();

            sut.Error("first", Failure);
            sut.Error("second", Failure);
            AssertNoBalloon();

            sut.Error("third", Failure);

            notifier.Received(1).ShowBalloon(Arg.Any<string>(), Arg.Is<string>(text => text.Length > 0), ToolTipIcon.Warning, Arg.Any<Action>());
        }

        [Fact]
        public void the_balloon_click_opens_the_log_folder()
        {
            Action onClick = null;
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => onClick = call.Arg<Action>());
            var sut = CreateLogger(1);

            sut.Error("problem", Failure);
            Assert.NotNull(onClick);
            Assert.Equal(0, folderOpened);

            onClick();

            Assert.Equal(1, folderOpened);
        }

        [Fact]
        public void warnings_and_infos_never_cause_a_balloon()
        {
            var sut = CreateLogger(1);

            for (var i = 0; i < 20; i++)
            {
                sut.Warn("warning " + i);
                sut.Info("info " + i);
            }

            AssertNoBalloon();
        }

        [Fact]
        public void errors_that_are_further_apart_than_the_window_do_not_add_up()
        {
            var sut = CreateLogger();

            sut.Error("first", Failure);
            now = now.AddMinutes(4);
            sut.Error("second", Failure);
            now = now.AddMinutes(4);
            sut.Error("third", Failure);
            now = now.AddMinutes(4);
            sut.Error("fourth", Failure);

            AssertNoBalloon();
        }

        [Fact]
        public void a_failure_that_repeats_all_the_time_gives_one_balloon_per_cooldown()
        {
            var sut = CreateLogger(2);

            for (var i = 0; i < 100; i++)
                sut.Error("again", Failure);

            notifier.Received(1).ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>());

            now = now.AddMinutes(31);
            sut.Error("again", Failure);
            sut.Error("again", Failure);

            notifier.Received(2).ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>());
        }

        [Fact]
        public void does_nothing_special_until_a_notifier_is_available()
        {
            var logger = new ErrorNotifyingLogger(inner, null, () => now, 1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30));

            logger.Error("before the tray icon exists", Failure);

            inner.Received().Log(LogLevel.Error, "before the tray icon exists", Failure);
            AssertNoBalloon();
        }

        [Fact]
        public void stops_notifying_when_the_notifier_is_removed()
        {
            var sut = CreateLogger(1);
            sut.SetNotifier(null);

            sut.Error("while shutting down", Failure);

            AssertNoBalloon();
        }

        [Fact]
        public void a_failing_notifier_does_not_break_logging()
        {
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => { throw new InvalidOperationException("no tray"); });
            var sut = CreateLogger(1);

            sut.Error("problem", Failure);

            inner.Received().Log(LogLevel.Warning, Arg.Is<string>(text => text.Contains("tray")), Arg.Any<InvalidOperationException>());
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ErrorNotifyingLogger(null, null));
            Assert.Throws<ArgumentNullException>(() => new ErrorNotifyingLogger(inner, null, null, 3, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ErrorNotifyingLogger(inner, null, () => now, 0, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30)));
        }
    }
}
