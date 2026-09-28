using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Carnac.Logic;
using Carnac.Logic.Overlay;

namespace Carnac.UI
{
    public partial class KeyShowView : IOverlayWindow, IOverlayStyle
    {
        // How often the overlay is put back on top of other topmost windows.
        static readonly TimeSpan TopmostRefreshInterval = TimeSpan.FromSeconds(1);

        readonly IScreenManager screenManager;
        readonly IDisplaySettingsMonitor displaySettingsMonitor;
        readonly IConcurrencyService concurrencyService;
        IntPtr hwnd;
        DispatcherTimer topmostTimer;
        OverlayPlacementController placement;
        OverlayStyleController styles;

        public KeyShowView(
            KeyShowViewModel keyShowViewModel,
            IScreenManager screenManager,
            IDisplaySettingsMonitor displaySettingsMonitor,
            IConcurrencyService concurrencyService)
        {
            if (keyShowViewModel == null) throw new ArgumentNullException("keyShowViewModel");
            if (screenManager == null) throw new ArgumentNullException("screenManager");
            if (displaySettingsMonitor == null) throw new ArgumentNullException("displaySettingsMonitor");
            if (concurrencyService == null) throw new ArgumentNullException("concurrencyService");

            this.screenManager = screenManager;
            this.displaySettingsMonitor = displaySettingsMonitor;
            this.concurrencyService = concurrencyService;
            DataContext = keyShowViewModel;
            ShowActivated = false;
            InitializeComponent();
            Title = OverlayWindowStyles.WindowTitle;
        }

        double IOverlayWindow.DpiScale
        {
            get
            {
                // WPF lays out in DIPs of the system DPI, so this is the pixels-per-DIP factor of the window's content.
                var source = PresentationSource.FromVisual(this);
                return source != null && source.CompositionTarget != null
                    ? source.CompositionTarget.TransformToDevice.M11
                    : 1.0;
            }
        }

        bool IOverlayWindow.SetBounds(PixelRect bounds)
        {
            return hwnd != IntPtr.Zero && Win32Methods.SetWindowRect(hwnd, bounds);
        }

        void IOverlayStyle.SetCaptureFriendly(bool captureFriendly)
        {
            if (hwnd != IntPtr.Zero)
                Win32Methods.ApplyOverlayWindowStyles(hwnd, captureFriendly);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            hwnd = new WindowInteropHelper(this).Handle;
            var settings = ((KeyShowViewModel)DataContext).Settings;
            styles = new OverlayStyleController(this, settings);
            styles.Apply();

            topmostTimer = new DispatcherTimer { Interval = TopmostRefreshInterval };
            topmostTimer.Tick += TopmostTimerTick;
            topmostTimer.Start();

            placement = new OverlayPlacementController(this, screenManager, settings, displaySettingsMonitor, concurrencyService);
            placement.Apply();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (styles != null)
            {
                styles.Dispose();
                styles = null;
            }
            if (placement != null)
            {
                placement.Dispose();
                placement = null;
            }
            if (topmostTimer != null)
            {
                topmostTimer.Stop();
                topmostTimer.Tick -= TopmostTimerTick;
                topmostTimer = null;
            }
            hwnd = IntPtr.Zero;

            base.OnClosed(e);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // WPF may size the window from its own Left/Top/Width/Height once it is shown, so place it again.
            if (placement != null)
                placement.Apply();
        }

        void TopmostTimerTick(object sender, EventArgs e)
        {
            if (hwnd != IntPtr.Zero)
                Win32Methods.BringToTopmost(hwnd);
        }
    }
}
