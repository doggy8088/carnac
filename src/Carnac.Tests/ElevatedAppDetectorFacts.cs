using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Carnac.Logic;
using NSubstitute;
using Xunit;

namespace Carnac.Tests
{
    public class ElevatedAppDetectorFacts
    {
        const int CarnacProcessId = 100;

        readonly FakeForeground foreground = new FakeForeground();
        readonly FakeElevation elevation = new FakeElevation();
        readonly ITrayNotifier notifier = Substitute.For<ITrayNotifier>();
        readonly ILogger logger = Substitute.For<ILogger>();
        int restartRequests;

        ElevatedAppDetector CreateDetector(int rememberedProcesses = ElevatedAppDetector.DefaultRememberedProcesses)
        {
            return new ElevatedAppDetector(foreground, elevation, notifier, () => restartRequests++, logger, CarnacProcessId, rememberedProcesses);
        }

        void AssertNoBalloon()
        {
            notifier.DidNotReceiveWithAnyArgs().ShowBalloon(null, null, ToolTipIcon.None, null);
        }

        void AssertBalloons(int count)
        {
            notifier.Received(count).ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>());
        }

        [Fact]
        public void shows_one_balloon_when_an_elevated_application_gets_the_focus()
        {
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, "notepad");

            CreateDetector().Poll();

            notifier.Received(1).ShowBalloon(Arg.Any<string>(), Arg.Is<string>(text => text.Contains("notepad") && text.Contains("Restart Carnac as administrator?")),
                ToolTipIcon.Warning, Arg.Any<Action>());
        }

        [Fact]
        public void clicking_the_balloon_asks_for_the_restart()
        {
            Action onClick = null;
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => onClick = call.Arg<Action>());
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, "notepad");
            CreateDetector().Poll();
            Assert.Equal(0, restartRequests);

            onClick();

            Assert.Equal(1, restartRequests);
        }

        [Fact]
        public void the_balloon_stays_within_the_limits_of_a_tray_balloon()
        {
            string title = null;
            string text = null;
            notifier.When(n => n.ShowBalloon(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ToolTipIcon>(), Arg.Any<Action>()))
                .Do(call => { title = call.ArgAt<string>(0); text = call.ArgAt<string>(1); });
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, new string('n', 300));

            CreateDetector().Poll();

            Assert.True(title.Length <= 63);
            Assert.True(text.Length <= 255, "the text has " + text.Length + " characters");
        }

        [Fact]
        public void logs_the_detection()
        {
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, "notepad");

            CreateDetector().Poll();

            logger.Received(1).Log(LogLevel.Info, Arg.Is<string>(text => text.Contains("notepad") && text.Contains("1234") && text.Contains("administrator")), null);
        }

        [Fact]
        public void says_nothing_about_applications_that_do_not_run_as_administrator()
        {
            elevation.Elevated[1234] = false;
            foreground.Current = new ForegroundProcess(1234, "notepad");

            CreateDetector().Poll();

            AssertNoBalloon();
        }

        [Fact]
        public void says_nothing_when_the_rights_of_the_application_cannot_be_told()
        {
            foreground.Current = new ForegroundProcess(1234, "notepad");   // unknown to the fake: null

            CreateDetector().Poll();

            AssertNoBalloon();
            logger.DidNotReceiveWithAnyArgs().Log(LogLevel.Warning, null, null);
        }

        [Fact]
        public void says_nothing_without_a_foreground_application()
        {
            foreground.Current = null;

            CreateDetector().Poll();

            AssertNoBalloon();
            Assert.Equal(0, elevation.Checks);
        }

        [Fact]
        public void announces_a_process_only_once_per_session()
        {
            elevation.Elevated[1234] = true;
            elevation.Elevated[5678] = false;
            var sut = CreateDetector();

            foreground.Current = new ForegroundProcess(1234, "notepad");
            sut.Poll();
            foreground.Current = new ForegroundProcess(5678, "chrome");
            sut.Poll();
            foreground.Current = new ForegroundProcess(1234, "notepad");   // back again
            sut.Poll();
            foreground.Current = new ForegroundProcess(5678, "chrome");
            sut.Poll();
            foreground.Current = new ForegroundProcess(1234, "notepad");
            sut.Poll();

            AssertBalloons(1);
        }

        [Fact]
        public void every_elevated_process_gets_its_own_balloon()
        {
            elevation.Elevated[1234] = true;
            elevation.Elevated[5678] = true;
            var sut = CreateDetector();

            foreground.Current = new ForegroundProcess(1234, "notepad");
            sut.Poll();
            foreground.Current = new ForegroundProcess(5678, "regedit");
            sut.Poll();

            AssertBalloons(2);
        }

        [Fact]
        public void does_not_ask_windows_again_while_the_foreground_process_stays_the_same()
        {
            elevation.Elevated[1234] = false;
            foreground.Current = new ForegroundProcess(1234, "notepad");
            var sut = CreateDetector();

            for (var i = 0; i < 10; i++)
                sut.Poll();

            Assert.Equal(1, elevation.Checks);
        }

        [Fact]
        public void a_window_of_carnac_itself_is_never_reported()
        {
            elevation.Elevated[CarnacProcessId] = true;
            foreground.Current = new ForegroundProcess(CarnacProcessId, "Carnac");

            CreateDetector().Poll();

            AssertNoBalloon();
        }

        [Fact]
        public void the_uac_prompt_and_the_sign_in_screens_are_ignored()
        {
            var names = new[] { "consent", "CONSENT", "LogonUI", "winlogon" };
            var sut = CreateDetector();

            for (var i = 0; i < names.Length; i++)
            {
                elevation.Elevated[200 + i] = true;
                foreground.Current = new ForegroundProcess(200 + i, names[i]);
                sut.Poll();
            }

            AssertNoBalloon();
        }

        [Fact]
        public void does_nothing_when_carnac_already_runs_as_administrator()
        {
            elevation.CarnacIsElevated = true;
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, "notepad");

            CreateDetector().Poll();

            AssertNoBalloon();
            Assert.Equal(0, elevation.Checks);
        }

        [Fact]
        public void a_failing_check_is_logged_and_does_not_stop_the_detection()
        {
            elevation.Failure = new InvalidOperationException("the check failed");
            foreground.Current = new ForegroundProcess(1234, "notepad");
            var sut = CreateDetector();

            sut.Poll();

            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "the check failed"));

            elevation.Failure = null;
            elevation.Elevated[5678] = true;
            foreground.Current = new ForegroundProcess(5678, "regedit");
            sut.Poll();

            AssertBalloons(1);
        }

        [Fact]
        public void a_failing_foreground_source_is_logged()
        {
            foreground.Failure = new InvalidOperationException("no desktop");

            CreateDetector().Poll();

            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Is<Exception>(ex => ex.Message == "no desktop"));
        }

        [Fact]
        public void forgets_the_oldest_processes_so_that_the_memory_stays_bounded()
        {
            var sut = CreateDetector(2);
            foreach (var id in new[] { 1, 2, 3 })
            {
                elevation.Elevated[id] = true;
                foreground.Current = new ForegroundProcess(id, "app" + id);
                sut.Poll();
            }

            AssertBalloons(3);

            // process 1 was forgotten (only 2 are remembered), so it is announced again; 3 is still known
            foreground.Current = new ForegroundProcess(3, "app3");
            sut.Poll();
            AssertBalloons(3);
            foreground.Current = new ForegroundProcess(1, "app1");
            sut.Poll();
            AssertBalloons(4);
        }

        [Fact]
        public void a_process_without_a_name_still_gets_a_balloon()
        {
            elevation.Elevated[1234] = true;
            foreground.Current = new ForegroundProcess(1234, null);

            CreateDetector().Poll();

            AssertBalloons(1);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Action restart = () => { };
            Assert.Throws<ArgumentNullException>(() => new ElevatedAppDetector(null, elevation, notifier, restart, logger, 1));
            Assert.Throws<ArgumentNullException>(() => new ElevatedAppDetector(foreground, null, notifier, restart, logger, 1));
            Assert.Throws<ArgumentNullException>(() => new ElevatedAppDetector(foreground, elevation, null, restart, logger, 1));
            Assert.Throws<ArgumentNullException>(() => new ElevatedAppDetector(foreground, elevation, notifier, null, logger, 1));
            Assert.Throws<ArgumentNullException>(() => new ElevatedAppDetector(foreground, elevation, notifier, restart, null, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ElevatedAppDetector(foreground, elevation, notifier, restart, logger, 1, 0));
        }

        class FakeForeground : IForegroundSource
        {
            public ForegroundProcess Current;
            public Exception Failure;

            public ForegroundProcess GetForegroundProcess()
            {
                if (Failure != null)
                    throw Failure;
                return Current;
            }
        }

        class FakeElevation : IElevationChecker
        {
            public readonly Dictionary<int, bool> Elevated = new Dictionary<int, bool>();
            public bool CarnacIsElevated;
            public Exception Failure;
            public int Checks;

            public bool IsCurrentProcessElevated
            {
                get { return CarnacIsElevated; }
            }

            public bool? IsProcessElevated(int processId)
            {
                Checks++;
                if (Failure != null)
                    throw Failure;

                bool elevated;
                return Elevated.TryGetValue(processId, out elevated) ? elevated : (bool?)null;
            }
        }
    }
}
