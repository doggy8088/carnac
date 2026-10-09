using System;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// The whole "check for updates on startup" job: run the check and, if a newer release exists, show the balloon whose click
    /// opens the release page. It runs on a background thread, so it must never throw.
    /// </summary>
    public sealed class UpdateCheckRunner
    {
        readonly UpdateChecker checker;
        readonly ReleaseVersion currentVersion;
        readonly ITrayNotifier notifier;
        readonly Action<string> openUrl;
        readonly ILogger logger;

        /// <param name="openUrl">Opens an address in the browser; invoked when the user clicks the balloon.</param>
        public UpdateCheckRunner(UpdateChecker checker, ReleaseVersion currentVersion, ITrayNotifier notifier, Action<string> openUrl, ILogger logger)
        {
            if (checker == null)
                throw new ArgumentNullException("checker");
            if (currentVersion == null)
                throw new ArgumentNullException("currentVersion");
            if (notifier == null)
                throw new ArgumentNullException("notifier");
            if (openUrl == null)
                throw new ArgumentNullException("openUrl");
            if (logger == null)
                throw new ArgumentNullException("logger");

            this.checker = checker;
            this.currentVersion = currentVersion;
            this.notifier = notifier;
            this.openUrl = openUrl;
            this.logger = logger;
        }

        public void Run()
        {
            try
            {
                var result = checker.CheckForUpdate();
                if (result.Outcome != UpdateCheckOutcome.UpdateAvailable)
                    return;

                var notice = UpdateNotice.Create(currentVersion, result.LatestVersion, result.Release);
                notifier.ShowBalloon(notice.Title, notice.Text, ToolTipIcon.Info, () => openUrl(notice.Url));
            }
            catch (Exception ex)
            {
                // this is a background job started from a timer: an exception here would take the whole process down
                logger.Warn("The update notice could not be shown", ex);
            }
        }
    }
}
