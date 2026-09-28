using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Carnac.Logic.Models;
using Carnac.Logic.Native;

namespace Carnac.Logic.Overlay
{
    /// <summary>
    /// Keeps the overlay window on the selected screen: it places the window when asked to, again when a setting that
    /// decides the position or size changes, and again once the monitors settled after a display change.
    /// All of it belongs to the UI thread.
    /// </summary>
    public class OverlayPlacementController : IDisposable
    {
        /// <summary>
        /// Windows sends several notifications for a single change (a monitor that is plugged in can produce a burst over
        /// a second); the window is placed once this long has passed without another one.
        /// </summary>
        public static readonly TimeSpan DisplayChangeQuietPeriod = TimeSpan.FromMilliseconds(500);

        readonly IOverlayWindow window;
        readonly IScreenManager screenManager;
        readonly PopupSettings settings;
        readonly IDisposable displayChangeSubscription;
        IList<DetailedScreen> screens;
        bool disposed;

        public OverlayPlacementController(
            IOverlayWindow window,
            IScreenManager screenManager,
            PopupSettings settings,
            IDisplaySettingsMonitor displaySettingsMonitor,
            IConcurrencyService concurrencyService)
        {
            if (window == null) throw new ArgumentNullException("window");
            if (screenManager == null) throw new ArgumentNullException("screenManager");
            if (settings == null) throw new ArgumentNullException("settings");
            if (displaySettingsMonitor == null) throw new ArgumentNullException("displaySettingsMonitor");
            if (concurrencyService == null) throw new ArgumentNullException("concurrencyService");

            this.window = window;
            this.screenManager = screenManager;
            this.settings = settings;

            settings.PropertyChanged += SettingsPropertyChanged;
            displayChangeSubscription = displaySettingsMonitor.DisplaySettingsChanged
                .Throttle(DisplayChangeQuietPeriod, concurrencyService.Default)
                .ObserveOn(concurrencyService.MainThreadScheduler)
                .Subscribe(_ => DisplaySettingsChanged());
        }

        /// <summary>Places the window on the screen the settings select.</summary>
        public void Apply()
        {
            if (disposed)
                return;

            if (screens == null)
                screens = screenManager.GetScreens().ToList();

            var bounds = OverlayPlacement.Resolve(screens, settings, window.DpiScale);
            if (bounds.HasValue && !window.SetBounds(bounds.Value))
                Trace.TraceWarning("Carnac: could not place the overlay window at {0}", bounds.Value);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            settings.PropertyChanged -= SettingsPropertyChanged;
            displayChangeSubscription.Dispose();
        }

        void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!OverlayPlacement.AffectsPlacement(e.PropertyName))
                return;

            // The screens are only read again when another screen may be selected, not on every slider tick.
            if (OverlayPlacement.AffectsScreenSelection(e.PropertyName))
                screens = null;

            Apply();
        }

        void DisplaySettingsChanged()
        {
            screens = null;
            Apply();
        }
    }
}
