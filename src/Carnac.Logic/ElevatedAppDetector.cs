using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Notices when the user works in an application that runs as administrator while Carnac does not, and says so once per process
    /// with a tray balloon that offers to restart Carnac as administrator. Nothing is elevated automatically: the balloon (and the
    /// tray menu item) only starts <c>restartAsAdministrator</c>, which asks Windows for permission.
    /// Call <see cref="Poll"/> regularly (about once a second); it does the work only when the foreground process changed.
    /// </summary>
    public sealed class ElevatedAppDetector
    {
        public const int DefaultRememberedProcesses = 256;

        // The UAC prompt and the sign-in screens run with higher rights than any application but nobody types there.
        static readonly string[] IgnoredProcessNames = { "consent", "LogonUI", "winlogon" };

        const int MaxNameLength = 50;

        readonly IForegroundSource foreground;
        readonly IElevationChecker checker;
        readonly ITrayNotifier notifier;
        readonly Action restartAsAdministrator;
        readonly ILogger logger;
        readonly int ownProcessId;
        readonly int rememberedProcesses;
        readonly object sync = new object();
        readonly HashSet<int> announced = new HashSet<int>();
        readonly Queue<int> announcedOrder = new Queue<int>();
        bool? carnacIsElevated;
        int lastProcessId = -1;

        /// <param name="restartAsAdministrator">Invoked when the user clicks the balloon.</param>
        /// <param name="ownProcessId">Carnac's own process id; its windows are never reported.</param>
        /// <param name="rememberedProcesses">How many processes are remembered as already announced (the oldest are forgotten first).</param>
        public ElevatedAppDetector(IForegroundSource foreground, IElevationChecker checker, ITrayNotifier notifier, Action restartAsAdministrator,
            ILogger logger, int ownProcessId, int rememberedProcesses = DefaultRememberedProcesses)
        {
            if (foreground == null)
                throw new ArgumentNullException("foreground");
            if (checker == null)
                throw new ArgumentNullException("checker");
            if (notifier == null)
                throw new ArgumentNullException("notifier");
            if (restartAsAdministrator == null)
                throw new ArgumentNullException("restartAsAdministrator");
            if (logger == null)
                throw new ArgumentNullException("logger");
            if (rememberedProcesses < 1)
                throw new ArgumentOutOfRangeException("rememberedProcesses");

            this.foreground = foreground;
            this.checker = checker;
            this.notifier = notifier;
            this.restartAsAdministrator = restartAsAdministrator;
            this.logger = logger;
            this.ownProcessId = ownProcessId;
            this.rememberedProcesses = rememberedProcesses;
        }

        /// <summary>Never throws; a failure is logged and the next poll tries again.</summary>
        public void Poll()
        {
            try
            {
                if (IsCarnacElevated())
                    return;

                var process = foreground.GetForegroundProcess();
                if (process == null)
                    return;

                lock (sync)
                {
                    if (process.Id == lastProcessId)
                        return;

                    lastProcessId = process.Id;
                }

                Inspect(process);
            }
            catch (Exception ex)
            {
                // runs on a timer thread: an exception would take the whole process down
                logger.Warn("Checking whether the foreground application runs as administrator failed", ex);
            }
        }

        bool IsCarnacElevated()
        {
            // cannot change while Carnac runs
            if (!carnacIsElevated.HasValue)
                carnacIsElevated = checker.IsCurrentProcessElevated;

            return carnacIsElevated.Value;
        }

        void Inspect(ForegroundProcess process)
        {
            if (process.Id == ownProcessId || IsIgnored(process.Name))
                return;

            if (checker.IsProcessElevated(process.Id) != true)
                return;

            lock (sync)
            {
                if (!Remember(process.Id))
                    return;
            }

            var name = string.IsNullOrEmpty(process.Name) ? "this application" : Shorten(process.Name);
            logger.Info(string.Format("The foreground application '{0}' (process {1}) runs as administrator: Carnac cannot see the keys typed into it",
                process.Name, process.Id));

            notifier.ShowBalloon(
                "Carnac can't see this application",
                string.Format("Carnac can't see keys typed in {0} because it is running as administrator. Restart Carnac as administrator? (Click this notice.)", name),
                ToolTipIcon.Warning,
                restartAsAdministrator);
        }

        // true when the process was not announced before
        bool Remember(int processId)
        {
            if (!announced.Add(processId))
                return false;

            announcedOrder.Enqueue(processId);
            while (announcedOrder.Count > rememberedProcesses)
                announced.Remove(announcedOrder.Dequeue());

            return true;
        }

        static bool IsIgnored(string processName)
        {
            foreach (var ignored in IgnoredProcessNames)
            {
                if (string.Equals(ignored, processName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static string Shorten(string name)
        {
            return name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength);
        }
    }
}
