using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.UI;
using Carnac.Utilities;
using SettingsProviderNet;

namespace Carnac
{
    public partial class App
    {
        readonly SettingsProvider settingsProvider;
        readonly IMessageProvider messageProvider;
        readonly PopupSettings settings;
        readonly IKeyDisplayState displayState = new KeyDisplayState();
        readonly FileLogger fileLogger = new FileLogger(FileLogger.DefaultDirectory);
        readonly ErrorNotifyingLogger logger;
        bool started;
        KeyShowView keyShowView;
        CarnacTrayIcon trayIcon;
        KeysController carnac;
        IDisposable updateCheck;
        readonly IElevationChecker elevationChecker = new SystemElevationChecker();
        AdministratorRestart administratorRestart;
        IDisposable elevationWatch;

        public App()
        {
            logger = new ErrorNotifyingLogger(fileLogger, OpenLogFolder);
            RegisterUnhandledExceptionLogging();

            settingsProvider = new SettingsProvider(new RoamingAppDataStorage("Carnac"));
            settings = settingsProvider.GetSettings<PopupSettings>();
            InterceptKeys.Current.Logger = logger;
            var keyProvider = new KeyProvider(InterceptKeys.Current, new PasswordModeService(displayState, settings), new DesktopLockEventService(), settingsProvider, new SystemProcessProvider(), logger);
            messageProvider = new MessageProvider(new ShortcutProvider(), keyProvider, settings, displayState);
        }

        // Unhandled exceptions used to leave no trace at all: Carnac just vanished or stopped showing keys.
        void RegisterUnhandledExceptionLogging()
        {
            DispatcherUnhandledException += (sender, args) =>
            {
                logger.Error("Unhandled exception on the UI thread", args.Exception);
                // Once Carnac is up, a failure in one popup should not take the tray icon down with it.
                // During startup nothing works without the failed step, so it is left to crash (after it was logged).
                args.Handled = started;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                var message = args.IsTerminating ? "Unhandled exception, Carnac is terminating" : "Unhandled exception";
                logger.Error(exception == null ? message + ": " + args.ExceptionObject : message, exception);
                fileLogger.Flush();
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                logger.Error("A task failed and nobody observed its exception", args.Exception);
                args.SetObserved();
            };
        }

        void OpenLogFolder()
        {
            ShellOpen(fileLogger.Directory);
        }

        // Opens a folder in Explorer or an address in the default browser.
        void ShellOpen(string target)
        {
            try
            {
                // the returned Process (null when an existing Explorer window is reused) is not needed
                using (Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }))
                {
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Could not open " + target, ex);
            }
        }

        // A pause after the start, so the check never competes with Carnac starting up; it then runs on a thread-pool thread.
        // Only the small settings access goes back to the UI thread: the settings provider is not thread-safe.
        void StartUpdateCheck()
        {
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var checker = new UpdateChecker(
                new GitHubReleaseFeed("Carnac/" + currentVersion),
                new MarshalledUpdateCheckHistory(new SettingsUpdateCheckHistory(settingsProvider), action => Dispatcher.Invoke(action)),
                ReleaseVersion.FromVersion(currentVersion),
                logger,
                () => DateTime.UtcNow);
            var runner = new UpdateCheckRunner(checker, ReleaseVersion.FromVersion(currentVersion), trayIcon, ShellOpen, logger);
            updateCheck = Observable.Timer(TimeSpan.FromSeconds(30)).Subscribe(x => runner.Run());
        }

        // Applications that run as administrator hide their keys from a normal Carnac (Windows blocks the keyboard hook).
        // Look at the foreground application once a second and say so, once per application; the user decides about the restart.
        void StartElevatedAppWatch(Action restartAsAdministrator)
        {
            string executablePath;
            int processId;
            try
            {
                using (var self = Process.GetCurrentProcess())
                {
                    processId = self.Id;
                    executablePath = self.MainModule.FileName;
                }
            }
            catch (Win32Exception ex)
            {
                // without its own path Carnac cannot restart itself; everything else keeps working
                logger.Warn("Restart as administrator is not available: the path of Carnac.exe is unknown", ex);
                return;
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn("Restart as administrator is not available: the path of Carnac.exe is unknown", ex);
                return;
            }

            administratorRestart = new AdministratorRestart(elevationChecker, new ShellElevatedLauncher(), executablePath, processId,
                () => Dispatcher.BeginInvoke(new Action(() => Shutdown())), trayIcon, logger);
            var detector = new ElevatedAppDetector(new SystemForegroundSource(), elevationChecker, trayIcon, restartAsAdministrator, logger, processId);
            elevationWatch = Observable.Interval(TimeSpan.FromSeconds(1)).Subscribe(x => detector.Poll());
        }

        void RestartAsAdministrator()
        {
            // The UAC prompt can stay open for a long time. The keyboard hook runs on the UI thread, which must not be blocked meanwhile.
            var restart = administratorRestart;
            if (restart != null)
            {
                Task.Factory.StartNew(() => restart.Restart());
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            // Carnac restarted itself as administrator: wait until the old instance has closed, otherwise its
            // single-instance mutex would make this instance quit at once.
            int replacedProcessId;
            if (RestartArguments.TryGetProcessIdToWaitFor(e.Args, out replacedProcessId))
            {
                ProcessUtilities.WaitForExit(replacedProcessId, TimeSpan.FromSeconds(15));
            }

            // Check if there was instance before this. If there was-close the current one.  
            if (ProcessUtilities.ThisProcessIsAlreadyRunning())
            {
                ProcessUtilities.SetFocusToPreviousInstance("Carnac");
                Shutdown();
                return;
            }

            logger.Info(string.Format("Carnac {0} started ({1}-bit process on a {2}-bit system, CLR {3})",
                Assembly.GetExecutingAssembly().GetName().Version, Environment.Is64BitProcess ? 64 : 32,
                Environment.Is64BitOperatingSystem ? 64 : 32, Environment.Version));

            // Nothing to restart (and no menu item) when Carnac already runs as administrator.
            Action restartAsAdministrator = elevationChecker.IsCurrentProcessElevated ? null : (Action)RestartAsAdministrator;
            trayIcon = new CarnacTrayIcon(displayState, settings, restartAsAdministrator);
            trayIcon.OpenPreferences += TrayIconOnOpenPreferences;
            logger.SetNotifier(trayIcon);
            if (restartAsAdministrator != null)
            {
                StartElevatedAppWatch(restartAsAdministrator);
            }
            else
            {
                logger.Info("Carnac runs as administrator");
            }

            // Settings saved before Top was persisted load it as 0, so refresh the
            // configured screen's origin to place the overlay on the right display.
            var screen = new ScreenManager().GetScreens().FirstOrDefault(s => s.Index == settings.Screen);
            if (screen != null)
            {
                settings.Left = screen.Left;
                settings.Top = screen.Top;
            }

            var keyShowViewModel = new KeyShowViewModel(settings);
            keyShowView = new KeyShowView(keyShowViewModel);
            keyShowView.Show();

            carnac = new KeysController(keyShowViewModel.Messages, messageProvider, new ConcurrencyService(), settingsProvider, logger);
            carnac.Start();

            // Opt-in: without the setting "Check for updates on startup" Carnac never contacts GitHub.
            if (settings.AutoUpdate)
            {
                StartUpdateCheck();
            }

            base.OnStartup(e);
            started = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (updateCheck != null)
            {
                updateCheck.Dispose();
            }

            if (elevationWatch != null)
            {
                elevationWatch.Dispose();
            }

            // trayIcon and carnac do not exist when this was a second instance that closed itself right after starting
            if (trayIcon != null)
            {
                logger.SetNotifier(null);
                trayIcon.Dispose();
            }

            if (carnac != null)
            {
                carnac.Dispose();
                logger.Info("Carnac exited");
            }

            fileLogger.Dispose();
            ProcessUtilities.DestroyMutex();

            base.OnExit(e);
        }

        void TrayIconOnOpenPreferences()
        {
            var preferencesViewModel = new PreferencesViewModel(settingsProvider, new ScreenManager());
            var preferencesView = new PreferencesView(preferencesViewModel);
            preferencesView.Show();
        }
    }
}
