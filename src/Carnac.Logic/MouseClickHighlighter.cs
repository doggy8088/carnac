using System;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Forms;
using Carnac.Logic.Models;
using Carnac.Logic.MouseMonitor;

namespace Carnac.Logic
{
    /// <summary>
    /// Turns mouse clicks into rings to show on the overlay, for as long as the "show mouse clicks" setting is on.
    /// </summary>
    public class MouseClickHighlighter
    {
        readonly IInterceptMouse interceptMouse;
        readonly PopupSettings settings;
        readonly Func<MouseClick, OverlayLocation> toOverlayLocation;
        readonly IScheduler scheduler;

        /// <param name="toOverlayLocation">Where on the overlay a click is, or null when it is not on the overlay.</param>
        /// <param name="scheduler">
        /// Where the highlights are made. A mouse hook must be quick to let the mouse go on, so the clicks are handed
        /// over to this (the UI thread) and the hook is free again before anything is drawn.
        /// </param>
        public MouseClickHighlighter(IInterceptMouse interceptMouse, PopupSettings settings, Func<MouseClick, OverlayLocation> toOverlayLocation, IScheduler scheduler)
        {
            if (interceptMouse == null) throw new ArgumentNullException("interceptMouse");
            if (settings == null) throw new ArgumentNullException("settings");
            if (toOverlayLocation == null) throw new ArgumentNullException("toOverlayLocation");
            if (scheduler == null) throw new ArgumentNullException("scheduler");

            this.interceptMouse = interceptMouse;
            this.settings = settings;
            this.toOverlayLocation = toOverlayLocation;
            this.scheduler = scheduler;
        }

        public IObservable<ClickHighlight> GetHighlightStream()
        {
            // The setting is read when the stream is subscribed to, not when it is made
            return Observable.Defer(() =>
            {
                var settingChanged = Observable
                    .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                        handler => settings.PropertyChanged += handler,
                        handler => settings.PropertyChanged -= handler)
                    .Where(e => e.EventArgs.PropertyName == "ShowMouseClicks");

                // The mouse is only listened to while the setting is on: switching it off drops the subscription
                // (and with it the hook), switching it on again subscribes again.
                return settingChanged
                    .Select(e => settings.ShowMouseClicks)
                    .StartWith(settings.ShowMouseClicks)
                    .DistinctUntilChanged()
                    .Select(showMouseClicks => showMouseClicks ? GetClicks() : Observable.Never<MouseClick>())
                    .Switch()
                    .Select(CreateHighlight)
                    .Where(highlight => highlight != null);
            });
        }

        // A mouse that cannot be listened to only means there is nothing to highlight
        IObservable<MouseClick> GetClicks()
        {
            return interceptMouse.GetClickStream()
                .Catch<MouseClick, Exception>(exception => Observable.Never<MouseClick>())
                .ObserveOn(scheduler);
        }

        ClickHighlight CreateHighlight(MouseClick click)
        {
            OverlayLocation location;
            try
            {
                location = toOverlayLocation(click);
            }
            catch (Exception)
            {
                // a click that cannot be placed is not highlighted, and must not end the highlighting of the ones after it
                return null;
            }

            if (location == null)
                return null;

            string colorName;
            switch (click.Button)
            {
                case MouseButtons.Left:
                    colorName = ClickHighlightSettings.GetColorName(settings.LeftClickColor, ClickHighlightSettings.DefaultLeftColor);
                    break;
                case MouseButtons.Middle:
                    colorName = ClickHighlightSettings.GetColorName(settings.MiddleClickColor, ClickHighlightSettings.DefaultMiddleColor);
                    break;
                case MouseButtons.Right:
                    colorName = ClickHighlightSettings.GetColorName(settings.RightClickColor, ClickHighlightSettings.DefaultRightColor);
                    break;
                default:
                    return null;
            }

            return new ClickHighlight(
                location,
                colorName,
                ClickHighlightSettings.GetDiameter(settings.ClickCircleSize),
                ClickHighlightSettings.GetDuration(settings.ClickCircleDuration));
        }
    }
}
