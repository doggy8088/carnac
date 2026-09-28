using System;
using System.Reactive;
using System.Reactive.Linq;
using Microsoft.Win32;

namespace Carnac.Logic.Overlay
{
    /// <summary>Reports <see cref="SystemEvents.DisplaySettingsChanged"/> to whoever subscribes, for as long as they are subscribed.</summary>
    public class SystemEventsDisplaySettingsMonitor : IDisplaySettingsMonitor
    {
        public IObservable<Unit> DisplaySettingsChanged
        {
            get
            {
                return Observable
                    .FromEventPattern<EventHandler, EventArgs>(
                        handler => SystemEvents.DisplaySettingsChanged += handler,
                        handler => SystemEvents.DisplaySettingsChanged -= handler)
                    .Select(_ => Unit.Default);
            }
        }
    }
}
