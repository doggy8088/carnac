using System;

namespace Carnac.Logic
{
    /// <summary>Remembers when the update check last ran, so that it runs at most once a day.</summary>
    public interface IUpdateCheckHistory
    {
        DateTime? LastCheckUtc { get; }

        void RecordCheck(DateTime utcNow);
    }

    public enum UpdateCheckOutcome
    {
        /// <summary>The last check is less than a day old: nothing was requested.</summary>
        NotDue,
        UpToDate,
        UpdateAvailable,
        /// <summary>The lookup failed (offline, rate limit, unreadable answer, malformed tag). Logged, never thrown.</summary>
        Failed
    }

    public sealed class UpdateCheckResult
    {
        public UpdateCheckResult(UpdateCheckOutcome outcome, LatestRelease release, ReleaseVersion latestVersion)
        {
            Outcome = outcome;
            Release = release;
            LatestVersion = latestVersion;
        }

        public UpdateCheckOutcome Outcome { get; private set; }

        /// <summary>The release GitHub reported; set for <see cref="UpdateCheckOutcome.UpToDate"/> and <see cref="UpdateCheckOutcome.UpdateAvailable"/>.</summary>
        public LatestRelease Release { get; private set; }

        public ReleaseVersion LatestVersion { get; private set; }
    }

    /// <summary>
    /// Decides whether a newer Carnac release exists: at most one request per day, no download, and every failure
    /// is logged instead of thrown. Whoever creates a checker decides whether it runs at all (the setting "Check for
    /// updates on startup"); a checker that is not created makes no network call.
    /// </summary>
    public sealed class UpdateChecker
    {
        public static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

        readonly IReleaseFeed feed;
        readonly IUpdateCheckHistory history;
        readonly ReleaseVersion currentVersion;
        readonly ILogger logger;
        readonly Func<DateTime> utcNow;

        public UpdateChecker(IReleaseFeed feed, IUpdateCheckHistory history, ReleaseVersion currentVersion, ILogger logger, Func<DateTime> utcNow)
        {
            if (feed == null)
                throw new ArgumentNullException("feed");
            if (history == null)
                throw new ArgumentNullException("history");
            if (currentVersion == null)
                throw new ArgumentNullException("currentVersion");
            if (logger == null)
                throw new ArgumentNullException("logger");
            if (utcNow == null)
                throw new ArgumentNullException("utcNow");

            this.feed = feed;
            this.history = history;
            this.currentVersion = currentVersion;
            this.logger = logger;
            this.utcNow = utcNow;
        }

        /// <summary>Blocks while the feed is asked, so call it from a background thread. Never throws.</summary>
        public UpdateCheckResult CheckForUpdate()
        {
            try
            {
                var now = utcNow();
                var last = history.LastCheckUtc;

                // a last check "in the future" means the clock was set back: check again
                if (last.HasValue && last.Value <= now && now - last.Value < CheckInterval)
                    return new UpdateCheckResult(UpdateCheckOutcome.NotDue, null, null);

                // recorded before asking, so a failing lookup is not repeated at every start either
                history.RecordCheck(now);

                var release = LatestRelease.Parse(feed.GetLatestReleaseJson());
                ReleaseVersion latest;
                if (!ReleaseVersion.TryParse(release.TagName, out latest))
                {
                    logger.Warn(string.Format("The update check ignored the release tag '{0}': it is not a version", release.TagName));
                    return new UpdateCheckResult(UpdateCheckOutcome.Failed, null, null);
                }

                if (release.IsDraft || release.IsPreRelease || !latest.IsNewerThan(currentVersion))
                    return new UpdateCheckResult(UpdateCheckOutcome.UpToDate, release, latest);

                logger.Info(string.Format("A newer Carnac release is available: {0} (this is {1})", latest, currentVersion));
                return new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, release, latest);
            }
            catch (Exception ex)
            {
                // offline, timeout, HTTP error, rate limit, unreadable answer, settings that cannot be saved, ...
                // The update check is a convenience and must never disturb Carnac, so all of it ends up in the log (as a warning: not a Carnac bug).
                logger.Warn("The update check failed", ex);
                return new UpdateCheckResult(UpdateCheckOutcome.Failed, null, null);
            }
        }
    }
}
