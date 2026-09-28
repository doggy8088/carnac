using System;
using System.Reactive;

namespace Carnac.Logic.Overlay
{
    /// <summary>Tells when the arrangement, resolution or number of the monitors changed.</summary>
    public interface IDisplaySettingsMonitor
    {
        /// <summary>
        /// Raised for every display change notification; Windows sends several in a row for a single change.
        /// It is raised on an arbitrary thread.
        /// </summary>
        IObservable<Unit> DisplaySettingsChanged { get; }
    }
}
