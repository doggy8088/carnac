using System;
using System.ComponentModel;
using System.Windows.Forms;
using Carnac.Logic;
using NSubstitute;
using Xunit;

namespace Carnac.Tests
{
    public class AdministratorRestartFacts
    {
        const string Executable = @"C:\Program Files\Carnac\Carnac.exe";
        const int ProcessId = 4321;

        readonly IElevationChecker checker = Substitute.For<IElevationChecker>();
        readonly IElevatedLauncher launcher = Substitute.For<IElevatedLauncher>();
        readonly ITrayNotifier notifier = Substitute.For<ITrayNotifier>();
        readonly ILogger logger = Substitute.For<ILogger>();
        int shutdowns;

        AdministratorRestart CreateRestart()
        {
            return new AdministratorRestart(checker, launcher, Executable, ProcessId, () => shutdowns++, notifier, logger);
        }

        [Fact]
        public void starts_the_elevated_instance_and_then_shuts_this_one_down()
        {
            var result = CreateRestart().Restart();

            Assert.Equal(RestartResult.Started, result);
            launcher.Received(1).Launch(Executable, "--wait-for-process 4321");
            Assert.Equal(1, shutdowns);
        }

        [Fact]
        public void tells_the_new_instance_to_wait_for_this_one()
        {
            string arguments = null;
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>())).Do(call => arguments = call.ArgAt<string>(1));

            CreateRestart().Restart();

            int waitFor;
            Assert.True(RestartArguments.TryGetProcessIdToWaitFor(arguments.Split(' '), out waitFor));
            Assert.Equal(ProcessId, waitFor);
        }

        [Fact]
        public void the_launch_happens_before_the_shutdown()
        {
            var order = new System.Collections.Generic.List<string>();
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>())).Do(call => order.Add("launch"));
            var restart = new AdministratorRestart(checker, launcher, Executable, ProcessId, () => order.Add("shutdown"), notifier, logger);

            restart.Restart();

            Assert.Equal(new[] { "launch", "shutdown" }, order.ToArray());
        }

        [Fact]
        public void declining_the_uac_prompt_leaves_this_instance_running()
        {
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>()))
                .Do(call => { throw new Win32Exception(AdministratorRestart.ErrorCancelledByUser, "The operation was canceled by the user"); });

            var result = CreateRestart().Restart();

            Assert.Equal(RestartResult.Cancelled, result);
            Assert.Equal(0, shutdowns);
            notifier.DidNotReceiveWithAnyArgs().ShowBalloon(null, null, ToolTipIcon.None, null);
            logger.DidNotReceive().Log(LogLevel.Warning, Arg.Any<string>(), Arg.Any<Exception>());
            logger.DidNotReceive().Log(LogLevel.Error, Arg.Any<string>(), Arg.Any<Exception>());
        }

        [Fact]
        public void the_cancel_error_code_is_the_one_windows_uses()
        {
            Assert.Equal(1223, AdministratorRestart.ErrorCancelledByUser);
        }

        [Fact]
        public void after_a_cancel_the_user_can_try_again()
        {
            var attempts = 0;
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>()))
                .Do(call =>
                {
                    if (++attempts == 1)
                        throw new Win32Exception(AdministratorRestart.ErrorCancelledByUser);
                });
            var sut = CreateRestart();

            Assert.Equal(RestartResult.Cancelled, sut.Restart());
            Assert.Equal(RestartResult.Started, sut.Restart());
            Assert.Equal(1, shutdowns);
        }

        [Fact]
        public void a_failure_to_start_is_logged_and_shown_and_this_instance_keeps_running()
        {
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>()))
                .Do(call => { throw new Win32Exception(2, "The system cannot find the file specified"); });

            var result = CreateRestart().Restart();

            Assert.Equal(RestartResult.Failed, result);
            Assert.Equal(0, shutdowns);
            logger.Received(1).Log(LogLevel.Warning, Arg.Any<string>(), Arg.Any<Win32Exception>());
            notifier.Received(1).ShowBalloon("Carnac could not restart as administrator", "The system cannot find the file specified", ToolTipIcon.Error, null);
        }

        [Fact]
        public void an_unexpected_exception_is_handled_like_a_failure()
        {
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>()))
                .Do(call => { throw new InvalidOperationException(""); });

            var result = CreateRestart().Restart();

            Assert.Equal(RestartResult.Failed, result);
            Assert.Equal(0, shutdowns);
            notifier.Received(1).ShowBalloon(Arg.Any<string>(), Arg.Is<string>(text => text.Length > 0), ToolTipIcon.Error, null);
        }

        [Fact]
        public void does_nothing_when_carnac_already_runs_as_administrator()
        {
            checker.IsCurrentProcessElevated.Returns(true);

            var result = CreateRestart().Restart();

            Assert.Equal(RestartResult.AlreadyElevated, result);
            launcher.DidNotReceiveWithAnyArgs().Launch(null, null);
            Assert.Equal(0, shutdowns);
        }

        [Fact]
        public void a_second_click_while_the_uac_prompt_is_open_does_not_start_a_second_restart()
        {
            var sut = CreateRestart();
            RestartResult second = RestartResult.Failed;
            launcher.When(l => l.Launch(Arg.Any<string>(), Arg.Any<string>())).Do(call => second = sut.Restart());

            var first = sut.Restart();

            Assert.Equal(RestartResult.Started, first);
            Assert.Equal(RestartResult.InProgress, second);
            launcher.Received(1).Launch(Arg.Any<string>(), Arg.Any<string>());
            Assert.Equal(1, shutdowns);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Action shutdown = () => { };
            Assert.Throws<ArgumentNullException>(() => new AdministratorRestart(null, launcher, Executable, 1, shutdown, notifier, logger));
            Assert.Throws<ArgumentNullException>(() => new AdministratorRestart(checker, null, Executable, 1, shutdown, notifier, logger));
            Assert.Throws<ArgumentException>(() => new AdministratorRestart(checker, launcher, "", 1, shutdown, notifier, logger));
            Assert.Throws<ArgumentNullException>(() => new AdministratorRestart(checker, launcher, Executable, 1, null, notifier, logger));
            Assert.Throws<ArgumentNullException>(() => new AdministratorRestart(checker, launcher, Executable, 1, shutdown, null, logger));
            Assert.Throws<ArgumentNullException>(() => new AdministratorRestart(checker, launcher, Executable, 1, shutdown, notifier, null));
        }
    }

    public class RestartArgumentsFacts
    {
        static int? WaitFor(params string[] args)
        {
            int processId;
            return RestartArguments.TryGetProcessIdToWaitFor(args, out processId) ? processId : (int?)null;
        }

        [Fact]
        public void formats_the_option_with_the_process_id()
        {
            Assert.Equal("--wait-for-process 1234", RestartArguments.Format(1234));
        }

        [Fact]
        public void finds_the_process_id_in_the_formatted_arguments()
        {
            Assert.Equal(1234, WaitFor(RestartArguments.Format(1234).Split(' ')));
        }

        [Fact]
        public void finds_the_option_among_other_arguments_in_any_case()
        {
            Assert.Equal(77, WaitFor("--something", "--WAIT-FOR-PROCESS", "77", "--more"));
        }

        [Fact]
        public void ignores_missing_or_invalid_values()
        {
            Assert.Null(WaitFor());
            Assert.Null(WaitFor("--wait-for-process"));
            Assert.Null(WaitFor("--wait-for-process", "abc"));
            Assert.Null(WaitFor("--wait-for-process", "-5"));
            Assert.Null(WaitFor("--wait-for-process", "0"));
            Assert.Null(WaitFor("--wait-for-process", "99999999999"));
            Assert.Null(WaitFor("--wait-for-process", "12 34"));
            Assert.Null(WaitFor("12"));
        }

        [Fact]
        public void tolerates_null_arguments()
        {
            int processId;
            Assert.False(RestartArguments.TryGetProcessIdToWaitFor(null, out processId));
            Assert.Equal(0, processId);
        }
    }
}
