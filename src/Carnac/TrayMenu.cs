using System;
using System.Windows.Forms;
using Carnac.Logic;

namespace Carnac
{
    /// <summary>
    /// The tray icon's context menu and status text. The menu items mirror the shared pause / silent-mode
    /// state (<see cref="IKeyDisplayState"/>): clicking an item changes that state and every change, whichever
    /// way it was made (menu, hotkey), is reflected in the item captions, check marks and <see cref="StatusText"/>.
    /// It has no dependency on the notification area itself, which keeps <see cref="CarnacTrayIcon"/> thin.
    /// </summary>
    public class TrayMenu : IDisposable
    {
        public const string AppName = "Carnac";

        readonly IKeyDisplayState displayState;
        readonly Action<Action> invokeOnUiThread;
        readonly ContextMenu contextMenu;
        readonly MenuItem settingsItem;
        readonly MenuItem pauseItem;
        readonly MenuItem silentItem;
        readonly MenuItem exitItem;
        bool disposed;

        /// <param name="displayState">The shared pause / silent-mode state.</param>
        /// <param name="openSettings">Invoked by the "Settings..." item.</param>
        /// <param name="exit">Invoked by the "Exit" item.</param>
        /// <param name="invokeOnUiThread">
        /// Runs an action on the UI thread. The state can change on the keyboard hook's thread (hotkeys),
        /// menu items may only be touched from the UI thread.
        /// </param>
        public TrayMenu(IKeyDisplayState displayState, Action openSettings, Action exit, Action<Action> invokeOnUiThread)
        {
            if (displayState == null)
                throw new ArgumentNullException("displayState");
            if (openSettings == null)
                throw new ArgumentNullException("openSettings");
            if (exit == null)
                throw new ArgumentNullException("exit");
            if (invokeOnUiThread == null)
                throw new ArgumentNullException("invokeOnUiThread");

            this.displayState = displayState;
            this.invokeOnUiThread = invokeOnUiThread;

            settingsItem = new MenuItem(Properties.Resources.TrayMenu_Settings, (sender, args) => openSettings())
            {
                DefaultItem = true
            };
            pauseItem = new MenuItem(Properties.Resources.TrayMenu_Pause, (sender, args) => displayState.TogglePaused());
            silentItem = new MenuItem(Properties.Resources.TrayMenu_SilentMode, (sender, args) => displayState.ToggleSilent());
            exitItem = new MenuItem(Properties.Resources.ShellView_Exit, (sender, args) => exit());

            contextMenu = new ContextMenu(new[] { settingsItem, pauseItem, silentItem, new MenuItem("-"), exitItem });

            Refresh();
            displayState.Changed += DisplayStateChanged;
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
            exitItem.Text = Properties.Resources.ShellView_Exit;
            Refresh();
        }

        void Refresh()
        {
            var paused = displayState.IsPaused;
            var silent = displayState.IsSilent;

            pauseItem.Text = paused ? Properties.Resources.TrayMenu_Resume : Properties.Resources.TrayMenu_Pause;
            pauseItem.Checked = paused;
            silentItem.Checked = silent;

            StatusText = TrayStatusText.Compose(AppName, paused, silent,
                Properties.Resources.TrayStatus_Paused, Properties.Resources.TrayStatus_Silent);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            displayState.Changed -= DisplayStateChanged;
            contextMenu.Dispose();
        }
    }
}
