using System;
using System.ComponentModel;
using Carnac.Logic.Models;

namespace Carnac.Logic.Overlay
{
    /// <summary>Keeps the window styles of the overlay in line with the settings, also while the settings are being changed.</summary>
    public class OverlayStyleController : IDisposable
    {
        readonly IOverlayStyle window;
        readonly PopupSettings settings;
        bool disposed;

        public OverlayStyleController(IOverlayStyle window, PopupSettings settings)
        {
            if (window == null) throw new ArgumentNullException("window");
            if (settings == null) throw new ArgumentNullException("settings");

            this.window = window;
            this.settings = settings;
            settings.PropertyChanged += SettingsPropertyChanged;
        }

        /// <summary>Applies the styles the settings ask for.</summary>
        public void Apply()
        {
            if (!disposed)
                window.SetCaptureFriendly(settings.CaptureFriendlyWindow);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            settings.PropertyChanged -= SettingsPropertyChanged;
        }

        void SettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (OverlayWindowStyles.AffectsStyles(e.PropertyName))
                Apply();
        }
    }
}
