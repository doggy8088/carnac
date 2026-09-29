using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Passes every entry on to another logger and, when errors keep happening, tells the user with one tray balloon
    /// ("Carnac hit an error - click to open the log folder"). This is the "at least pop up a tooltip from the tray icon"
    /// that Code52/carnac#85 asked for. Only <see cref="LogLevel.Error"/> entries count.
    /// A balloon is shown when <c>errorThreshold</c> errors happened within <c>window</c>; after that the balloon stays quiet
    /// for <c>cooldown</c>, so a failure that repeats on every key press does not nag.
    /// </summary>
    public sealed class ErrorNotifyingLogger : ILogger
    {
        public const int DefaultErrorThreshold = 3;
        public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(30);

        readonly ILogger inner;
        readonly Func<DateTime> clock;
        readonly int errorThreshold;
        readonly TimeSpan window;
        readonly TimeSpan cooldown;
        readonly Action openLogFolder;
        readonly object sync = new object();
        readonly Queue<DateTime> recentErrors = new Queue<DateTime>();
        ITrayNotifier notifier;
        DateTime? lastBalloon;

        /// <param name="inner">The logger that really writes the entries.</param>
        /// <param name="openLogFolder">Invoked when the user clicks the balloon; may be null.</param>
        public ErrorNotifyingLogger(ILogger inner, Action openLogFolder)
            : this(inner, openLogFolder, () => DateTime.Now, DefaultErrorThreshold, DefaultWindow, DefaultCooldown)
        {
        }

        public ErrorNotifyingLogger(ILogger inner, Action openLogFolder, Func<DateTime> clock, int errorThreshold, TimeSpan window, TimeSpan cooldown)
        {
            if (inner == null)
                throw new ArgumentNullException("inner");
            if (clock == null)
                throw new ArgumentNullException("clock");
            if (errorThreshold < 1)
                throw new ArgumentOutOfRangeException("errorThreshold");

            this.inner = inner;
            this.openLogFolder = openLogFolder;
            this.clock = clock;
            this.errorThreshold = errorThreshold;
            this.window = window;
            this.cooldown = cooldown;
        }

        /// <summary>
        /// The tray icon only exists once the application started, so it is handed over later.
        /// Errors before that are logged but cannot be announced.
        /// </summary>
        public void SetNotifier(ITrayNotifier trayNotifier)
        {
            lock (sync)
            {
                notifier = trayNotifier;
            }
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
            inner.Log(level, message, exception);

            if (level == LogLevel.Error)
                CountError();
        }

        void CountError()
        {
            ITrayNotifier target;
            lock (sync)
            {
                var now = clock();
                recentErrors.Enqueue(now);
                while (recentErrors.Count > 0 && now - recentErrors.Peek() > window)
                    recentErrors.Dequeue();

                if (notifier == null || recentErrors.Count < errorThreshold)
                    return;

                if (lastBalloon.HasValue && now - lastBalloon.Value < cooldown)
                    return;

                lastBalloon = now;
                recentErrors.Clear();
                target = notifier;
            }

            try
            {
                target.ShowBalloon("Carnac hit an error", "Carnac ran into a problem and kept going. Click here to open the log folder.",
                    ToolTipIcon.Warning, openLogFolder);
            }
            catch (Exception ex)
            {
                // a broken tray icon must not turn error reporting into another error; the entry goes to the file only
                inner.Log(LogLevel.Warning, "Could not show the error notice in the tray", ex);
            }
        }
    }
}
