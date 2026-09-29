using System;
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
using Squirrel;

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

#if !DEBUG
        readonly string carnacUpdateUrl = "https://github.com/Code52/carnac";
#endif

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
            try
            {
                // Explorer opens the folder; the returned Process (null when an existing Explorer window is reused) is not needed
                using (Process.Start(new ProcessStartInfo(fileLogger.Directory) { UseShellExecute = true }))
                {
                }
            }
            catch (Exception ex)
            {
                logger.Warn("Could not open the log folder " + fileLogger.Directory, ex);
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
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

            trayIcon = new CarnacTrayIcon(displayState, settings);
            trayIcon.OpenPreferences += TrayIconOnOpenPreferences;
            logger.SetNotifier(trayIcon);

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

#if !DEBUG
            if (settings.AutoUpdate)
            {
                Observable
                    .Timer(TimeSpan.FromMinutes(5))
                    .Subscribe(async x =>
                    {
                        try
                        {
                            using (var mgr = UpdateManager.GitHubUpdateManager(carnacUpdateUrl))
                            {
                                await mgr.Result.UpdateApp();
                            }
                        }
                        catch
                        {
                            // Do something useful with the exception
                        }
                    });
            }
#endif

            base.OnStartup(e);
            started = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
