using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Carnac.Logic;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;

namespace Carnac.UI
{
    public partial class KeyShowView
    {
        // How often the overlay is put back on top of other topmost windows.
        static readonly TimeSpan TopmostRefreshInterval = TimeSpan.FromSeconds(1);

        readonly IScreenManager screenManager;
        readonly PopupSettings settings;
        IntPtr hwnd;
        DispatcherTimer topmostTimer;
        IList<DetailedScreen> screens;

        public KeyShowView(KeyShowViewModel keyShowViewModel, IScreenManager screenManager)
        {
            if (keyShowViewModel == null) throw new ArgumentNullException("keyShowViewModel");
            if (screenManager == null) throw new ArgumentNullException("screenManager");

            this.screenManager = screenManager;
            settings = keyShowViewModel.Settings;
            DataContext = keyShowViewModel;
            ShowActivated = false;
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            hwnd = new WindowInteropHelper(this).Handle;
            Win32Methods.ApplyOverlayWindowStyles(hwnd);

            topmostTimer = new DispatcherTimer { Interval = TopmostRefreshInterval };
            topmostTimer.Tick += TopmostTimerTick;
            topmostTimer.Start();

            settings.PropertyChanged += SettingsPropertyChanged;
            ApplyPlacement();
        }

        protected override void OnClosed(EventArgs e)
        {
            settings.PropertyChanged -= SettingsPropertyChanged;
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
            ApplyPlacement();
        }

        void TopmostTimerTick(object sender, EventArgs e)
        {
            if (hwnd != IntPtr.Zero)
                Win32Methods.BringToTopmost(hwnd);
        }

        void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!OverlayPlacement.AffectsPlacement(e.PropertyName))
                return;

            // The screens are only enumerated again when another screen is selected, not on every slider tick.
            if (string.IsNullOrEmpty(e.PropertyName) || string.Equals(e.PropertyName, "Screen", StringComparison.Ordinal))
                screens = null;

            ApplyPlacement();
        }

        void ApplyPlacement()
        {
            if (hwnd == IntPtr.Zero)
                return;

            if (screens == null)
                screens = screenManager.GetScreens().ToList();

            var rect = OverlayPlacement.Resolve(screens, settings, GetDpiScale());
            if (rect.HasValue && !Win32Methods.SetWindowRect(hwnd, rect.Value))
                Trace.TraceWarning("Carnac: could not place the overlay window at {0} (Win32 error {1})", rect.Value, Marshal.GetLastWin32Error());
        }

        double GetDpiScale()
        {
            // WPF lays out in DIPs of the system DPI, so this is the pixels-per-DIP factor of the window's content.
            var source = PresentationSource.FromVisual(this);
            return source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformToDevice.M11
                : 1.0;
        }
    }
}
