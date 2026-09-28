using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Linq;
using System.Windows;
using Carnac.Logic;
using Application = System.Windows.Application;

namespace Carnac
{
    public class CarnacTrayIcon : ITrayNotifier, IDisposable
    {
        const int BalloonTimeoutMilliseconds = 10000;

        readonly NotifyIcon trayIcon;
        readonly TrayMenu trayMenu;
        Action balloonClickAction;
        bool disposed;

        public CarnacTrayIcon(IKeyDisplayState displayState)
        {
            if (displayState == null)
                throw new ArgumentNullException("displayState");

            trayMenu = new TrayMenu(displayState, OpenOrActivatePreferences, Exit, InvokeOnUiThread);

            using (var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Carnac.icon.embedded.ico"))
            {
                trayIcon = new NotifyIcon
                {
                    Icon = new Icon(iconStream),
                    ContextMenu = trayMenu.ContextMenu,
                    // Windows also exposes this text as the accessible name of the notification-area button
                    Text = trayMenu.StatusText
                };
            }

            trayMenu.StatusChanged += (sender, args) => trayIcon.Text = trayMenu.StatusText;
            trayIcon.MouseClick += NotifyIconClick;
            trayIcon.BalloonTipClicked += BalloonClicked;
            trayIcon.Visible = true;
        }

        public event Action OpenPreferences = () => { };

        /// <summary>Shows a balloon on the tray icon. Can be called from any thread.</summary>
        public void ShowBalloon(string title, string text, ToolTipIcon icon, Action onClick)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("A balloon needs some text.", "text");

            InvokeOnUiThread(() =>
            {
                if (disposed)
                    return;

                balloonClickAction = onClick;
                trayIcon.ShowBalloonTip(BalloonTimeoutMilliseconds, title ?? string.Empty, text, icon);
            });
        }

        void BalloonClicked(object sender, EventArgs e)
        {
            var action = balloonClickAction;
            balloonClickAction = null;
            if (action != null)
                action();
        }

        void NotifyIconClick(object sender, MouseEventArgs mouseEventArgs)
        {
            if (mouseEventArgs.Button == MouseButtons.Left)
            {
                OpenOrActivatePreferences();
            }
        }

        void OpenOrActivatePreferences()
        {
            var preferencesWindow = Application.Current.Windows.Cast<Window>().FirstOrDefault(x => x.Name == "PreferencesViewWindow");
            if (preferencesWindow != null)
            {
                preferencesWindow.Activate();
            }
            else
            {
                OpenPreferences();
            }
        }

        void Exit()
        {
            trayIcon.Visible = false;
            Application.Current.Shutdown();
        }

        static void InvokeOnUiThread(Action action)
        {
            var application = Application.Current;
            if (application == null || application.Dispatcher.CheckAccess())
                action();
            else
                application.Dispatcher.BeginInvoke(action);
        }

        public void Dispose()
        {
            disposed = true;
            trayMenu.Dispose();
            trayIcon.Dispose();
        }
    }
}
