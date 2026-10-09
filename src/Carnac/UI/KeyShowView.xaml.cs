using System;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Carnac.Logic;
using Carnac.Logic.MouseMonitor;
using Carnac.Logic.Overlay;

namespace Carnac.UI
{
    public partial class KeyShowView : IOverlayWindow, IOverlayStyle
    {
        // How often the overlay is put back on top of other topmost windows.
        static readonly TimeSpan TopmostRefreshInterval = TimeSpan.FromSeconds(1);

        readonly SingleAssignmentDisposable clickHighlights = new SingleAssignmentDisposable();
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
            : this(keyShowViewModel, screenManager, displaySettingsMonitor, concurrencyService, new InterceptMouse())
        {
        }

        public KeyShowView(
            KeyShowViewModel keyShowViewModel,
            IScreenManager screenManager,
            IDisplaySettingsMonitor displaySettingsMonitor,
            IConcurrencyService concurrencyService,
            IInterceptMouse interceptMouse)
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

            var highlighter = new MouseClickHighlighter(interceptMouse, keyShowViewModel.Settings, ToOverlayLocation, new DispatcherScheduler(Dispatcher));
            Loaded += (sender, e) =>
            {
                if (clickHighlights.Disposable == null)
                    clickHighlights.Disposable = highlighter.GetHighlightStream().Subscribe(ShowClickRing, exception => Debug.WriteLine("The click highlight has ended: " + exception));
            };
            Closed += (sender, e) => clickHighlights.Dispose();
        }

        // A ring that cannot be drawn must not end the rings after it
        void ShowClickRing(ClickHighlight highlight)
        {
            try
            {
                if (PresentationSource.FromVisual(ClickLayer) != null)
                {
                    var local = ClickLayer.PointFromScreen(new Point(highlight.Location.X, highlight.Location.Y));
                    var radius = highlight.Diameter / 2.0 + 4;
                    if (local.X >= radius && local.Y >= radius &&
                        local.X <= ClickLayer.ActualWidth - radius && local.Y <= ClickLayer.ActualHeight - radius)
                    {
                        ClickRing.Show(ClickLayer, new ClickHighlight(new OverlayLocation(local.X, local.Y), highlight.ColorName, highlight.Diameter, highlight.Duration));
                        return;
                    }
                }

                ClickRing.ShowAtScreenLocation(this, highlight);
            }
            catch (Exception)
            {
            }
        }

        OverlayLocation ToOverlayLocation(MouseClick click)
        {
            if (PresentationSource.FromVisual(ClickLayer) == null)
                return null;

            return new OverlayLocation(click.X, click.Y);
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
