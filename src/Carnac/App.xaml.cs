using System;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Windows;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.Logic.MouseMonitor;
using Carnac.Logic.Overlay;
using Carnac.UI;
using Carnac.Utilities;
using SettingsProviderNet;
using Squirrel;

namespace Carnac
{
    public partial class App
    {
        readonly SettingsProvider settingsProvider;
        readonly IScreenManager screenManager = new ScreenManager();
        IPreviewService previewService;
        readonly PopupSettings settings;
        KeyShowView keyShowView;
        CarnacTrayIcon trayIcon;
        KeysController carnac;

#if !DEBUG
        readonly string carnacUpdateUrl = "https://github.com/Code52/carnac";
#endif

        public App()
        {
            settingsProvider = new SettingsProvider(new RoamingAppDataStorage("Carnac"));
            settings = settingsProvider.GetSettings<PopupSettings>();
            // Before anything with text (the tray menu, key labels) is created, and again whenever the setting changes.
            UiLanguage.Follow(settings, RefreshLanguage);
        }

        void RefreshLanguage()
        {
            if (trayIcon != null)
                trayIcon.RefreshLanguage();
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

            trayIcon = new CarnacTrayIcon();
            trayIcon.OpenPreferences += TrayIconOnOpenPreferences;

            // One ConcurrencyService (its main thread scheduler wraps the UI thread's synchronization context) is shared by the
            // message provider, which schedules the chord timeout on it, and the controller that shows the messages.
            var concurrencyService = new ConcurrencyService();
            var keyShowViewModel = new KeyShowViewModel(settings);
            keyShowView = new KeyShowView(keyShowViewModel, screenManager, new SystemEventsDisplaySettingsMonitor(), concurrencyService, new InterceptMouse());
            keyShowView.Show();
            previewService = new PreviewService(keyShowViewModel.Messages, PreviewService.CreateSampleProcess());

            var keyProvider = new KeyProvider(InterceptKeys.Current, new PasswordModeService(), new DesktopLockEventService(), settingsProvider, new KeyboardLayoutTranslator());
            var messageProvider = new MessageProvider(new ShortcutProvider(), keyProvider, settings, concurrencyService);

            carnac = new KeysController(keyShowViewModel.Messages, messageProvider, concurrencyService, settingsProvider);
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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            trayIcon.Dispose();
            carnac.Dispose();
            ProcessUtilities.DestroyMutex();

            base.OnExit(e);
        }

        void TrayIconOnOpenPreferences()
        {
            var preferencesViewModel = new PreferencesViewModel(settingsProvider, screenManager, previewService);
            var preferencesView = new PreferencesView(preferencesViewModel);
            preferencesView.Show();
        }
    }
}
