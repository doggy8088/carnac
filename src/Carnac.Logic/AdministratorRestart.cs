using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>Starts the program with administrator rights (Windows shows the UAC prompt).</summary>
    public interface IElevatedLauncher
    {
        /// <exception cref="Win32Exception">
        /// The program could not be started. <see cref="Win32Exception.NativeErrorCode"/> is
        /// <see cref="AdministratorRestart.ErrorCancelledByUser"/> when the user declined the UAC prompt.
        /// </exception>
        void Launch(string executablePath, string arguments);
    }

    public enum RestartResult
    {
        /// <summary>The elevated instance was started; this instance is shutting down.</summary>
        Started,
        /// <summary>The user declined the UAC prompt. Nothing changed, this instance keeps running.</summary>
        Cancelled,
        AlreadyElevated,
        /// <summary>Another restart is already waiting for the UAC prompt.</summary>
        InProgress,
        /// <summary>The elevated instance could not be started. The user was told, this instance keeps running.</summary>
        Failed
    }

    /// <summary>
    /// "Restart as administrator": starts a new, elevated Carnac and closes this one. It only ever happens because the user asked for
    /// it, and only after Windows confirmed the UAC prompt: when the user declines, this instance carries on as if nothing happened.
    /// </summary>
    public sealed class AdministratorRestart
    {
        /// <summary>ERROR_CANCELLED, what ShellExecute reports when the user answers the UAC prompt with "No".</summary>
        public const int ErrorCancelledByUser = 1223;

        readonly IElevationChecker checker;
        readonly IElevatedLauncher launcher;
        readonly string executablePath;
        readonly int ownProcessId;
        readonly Action shutdown;
        readonly ITrayNotifier notifier;
        readonly ILogger logger;
        int restartRunning;

        /// <param name="executablePath">Carnac.exe.</param>
        /// <param name="shutdown">Closes this instance; invoked after the new one was started.</param>
        public AdministratorRestart(IElevationChecker checker, IElevatedLauncher launcher, string executablePath, int ownProcessId,
            Action shutdown, ITrayNotifier notifier, ILogger logger)
        {
            if (checker == null)
                throw new ArgumentNullException("checker");
            if (launcher == null)
                throw new ArgumentNullException("launcher");
            if (string.IsNullOrEmpty(executablePath))
                throw new ArgumentException("The path of the executable is required.", "executablePath");
            if (shutdown == null)
                throw new ArgumentNullException("shutdown");
            if (notifier == null)
                throw new ArgumentNullException("notifier");
            if (logger == null)
                throw new ArgumentNullException("logger");

            this.checker = checker;
            this.launcher = launcher;
            this.executablePath = executablePath;
            this.ownProcessId = ownProcessId;
            this.shutdown = shutdown;
            this.notifier = notifier;
            this.logger = logger;
        }

        public RestartResult Restart()
        {
            if (checker.IsCurrentProcessElevated)
                return RestartResult.AlreadyElevated;

            // a second click while the UAC prompt is still open must not open a second prompt
            if (Interlocked.CompareExchange(ref restartRunning, 1, 0) != 0)
                return RestartResult.InProgress;

            var started = false;
            try
            {
                logger.Info("Restarting Carnac as administrator");
                launcher.Launch(executablePath, RestartArguments.Format(ownProcessId));
                started = true;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode == ErrorCancelledByUser)
                {
                    logger.Info("The restart as administrator was cancelled in the UAC prompt");
                    return RestartResult.Cancelled;
                }

                return Failed(ex);
            }
            catch (Exception ex)
            {
                // started from a menu click: whatever went wrong, Carnac has to keep running and the user has to be told
                return Failed(ex);
            }
            finally
            {
                // after a failure or a cancel the user can try again; after a success this instance is on its way out
                if (!started)
                    Interlocked.Exchange(ref restartRunning, 0);
            }

            shutdown();
            return RestartResult.Started;
        }

        RestartResult Failed(Exception exception)
        {
            logger.Warn("Carnac could not be restarted as administrator", exception);
            var reason = string.IsNullOrWhiteSpace(exception.Message) ? "Windows did not start Carnac with administrator rights." : exception.Message;
            notifier.ShowBalloon("Carnac could not restart as administrator", reason, ToolTipIcon.Error, null);
            return RestartResult.Failed;
        }
    }
}
