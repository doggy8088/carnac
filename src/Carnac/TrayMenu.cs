using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.Models;

namespace Carnac
{
    /// <summary>
    /// The tray icon's context menu and status text. The menu items mirror the shared pause / silent-mode
    /// state (<see cref="IKeyDisplayState"/>): clicking an item changes that state and every change, whichever
    /// way it was made (menu, hotkey), is reflected in the item captions, check marks and <see cref="StatusText"/>.
    /// The configured hotkeys are shown next to the items. It has no dependency on the notification area itself,
    /// which keeps <see cref="CarnacTrayIcon"/> thin.
    /// </summary>
    public class TrayMenu : IDisposable
    {
        public const string AppName = "Carnac";

        readonly IKeyDisplayState displayState;
        readonly PopupSettings settings;
        readonly Action<Action> invokeOnUiThread;
        readonly ContextMenu contextMenu;
        readonly MenuItem settingsItem;
        readonly MenuItem pauseItem;
        readonly MenuItem silentItem;
        readonly MenuItem restartAsAdministratorItem;
        readonly MenuItem exitItem;
        bool disposed;

        /// <param name="displayState">The shared pause / silent-mode state.</param>
        /// <param name="settings">Source of the hotkeys that are shown next to the pause and silent mode items.</param>
        /// <param name="openSettings">Invoked by the "Settings..." item.</param>
        /// <param name="exit">Invoked by the "Exit" item.</param>
        /// <param name="invokeOnUiThread">
        /// Runs an action on the UI thread. The state can change on the keyboard hook's thread (hotkeys),
        /// menu items may only be touched from the UI thread.
        /// </param>
        /// <param name="restartAsAdministrator">
        /// Invoked by the "Restart as administrator" item. Pass null when Carnac already runs as administrator: the item is then left out.
        /// </param>
        public TrayMenu(IKeyDisplayState displayState, PopupSettings settings, Action openSettings, Action exit, Action<Action> invokeOnUiThread,
            Action restartAsAdministrator = null)
        {
            if (displayState == null)
                throw new ArgumentNullException("displayState");
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (openSettings == null)
                throw new ArgumentNullException("openSettings");
            if (exit == null)
                throw new ArgumentNullException("exit");
            if (invokeOnUiThread == null)
                throw new ArgumentNullException("invokeOnUiThread");

            this.displayState = displayState;
            this.settings = settings;
            this.invokeOnUiThread = invokeOnUiThread;

            settingsItem = new MenuItem(Properties.Resources.TrayMenu_Settings, (sender, args) => openSettings())
            {
                DefaultItem = true
            };
            pauseItem = new MenuItem(Properties.Resources.TrayMenu_Pause, (sender, args) => displayState.TogglePaused());
            silentItem = new MenuItem(Properties.Resources.TrayMenu_SilentMode, (sender, args) => displayState.ToggleSilent());
            exitItem = new MenuItem(Properties.Resources.ShellView_Exit, (sender, args) => exit());

            var items = new List<MenuItem> { settingsItem, pauseItem, silentItem, new MenuItem("-") };
            if (restartAsAdministrator != null)
            {
                restartAsAdministratorItem = new MenuItem(Properties.Resources.TrayMenu_RestartAsAdministrator, (sender, args) => restartAsAdministrator());
                items.Add(restartAsAdministratorItem);
            }

            items.Add(exitItem);
            contextMenu = new ContextMenu(items.ToArray());

            Refresh();
            displayState.Changed += DisplayStateChanged;
            settings.PropertyChanged += SettingsChanged;
        }

        public ContextMenu ContextMenu
        {
            get { return contextMenu; }
        }

        /// <summary>Tooltip / accessible name of the tray icon, for example "Carnac" or "Carnac - paused".</summary>
        public string StatusText { get; private set; }

        /// <summary>Raised on the UI thread after <see cref="StatusText"/> changed.</summary>
        public event EventHandler StatusChanged;

        void DisplayStateChanged(object sender, EventArgs e)
        {
            RefreshOnUiThread();
        }

        void SettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            // an empty name means "everything changed"
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "SilentModeHotkey" || e.PropertyName == "PauseHotkey")
                RefreshOnUiThread();
        }

        void RefreshOnUiThread()
        {
            invokeOnUiThread(() =>
            {
                if (disposed)
                    return;

                Refresh();
                var handler = StatusChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            });
        }

        public void RefreshLanguage()
        {
            if (disposed)
                return;

            settingsItem.Text = Properties.Resources.TrayMenu_Settings;
            silentItem.Text = Properties.Resources.TrayMenu_SilentMode;
            if (restartAsAdministratorItem != null)
                restartAsAdministratorItem.Text = Properties.Resources.TrayMenu_RestartAsAdministrator;
            exitItem.Text = Properties.Resources.ShellView_Exit;
            Refresh();
        }

        void Refresh()
        {
            var paused = displayState.IsPaused;
            var silent = displayState.IsSilent;

            pauseItem.Text = WithHotkey(paused ? Properties.Resources.TrayMenu_Resume : Properties.Resources.TrayMenu_Pause,
                ConfiguredHotkeys.ResolvePause(settings.PauseHotkey));
            pauseItem.Checked = paused;
            silentItem.Text = WithHotkey(Properties.Resources.TrayMenu_SilentMode,
                ConfiguredHotkeys.ResolveSilentMode(settings.SilentModeHotkey));
            silentItem.Checked = silent;

            StatusText = TrayStatusText.Compose(AppName, paused, silent,
                Properties.Resources.TrayStatus_Paused, Properties.Resources.TrayStatus_Silent);
        }

        static string WithHotkey(string caption, KeyPressDefinition hotkey)
        {
            // Windows shows the text after a tab right-aligned, like the shortcut of a normal menu item
            return hotkey == null ? caption : caption + "\t" + HotkeyParser.Format(hotkey);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            displayState.Changed -= DisplayStateChanged;
            settings.PropertyChanged -= SettingsChanged;
            contextMenu.Dispose();
        }
    }
}
