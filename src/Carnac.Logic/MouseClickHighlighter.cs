using System;
using System.ComponentModel;
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

        /// <param name="toOverlayLocation">Where on the overlay a click is, or null when it is not on the overlay.</param>
        public MouseClickHighlighter(IInterceptMouse interceptMouse, PopupSettings settings, Func<MouseClick, OverlayLocation> toOverlayLocation)
        {
            if (interceptMouse == null) throw new ArgumentNullException("interceptMouse");
            if (settings == null) throw new ArgumentNullException("settings");
            if (toOverlayLocation == null) throw new ArgumentNullException("toOverlayLocation");

            this.interceptMouse = interceptMouse;
            this.settings = settings;
            this.toOverlayLocation = toOverlayLocation;
        }

        public IObservable<ClickHighlight> GetHighlightStream()
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
        }

        // A mouse that cannot be listened to only means there is nothing to highlight
        IObservable<MouseClick> GetClicks()
        {
            return interceptMouse.GetClickStream().Catch<MouseClick, Exception>(exception => Observable.Never<MouseClick>());
        }

        ClickHighlight CreateHighlight(MouseClick click)
        {
            var location = toOverlayLocation(click);
            if (location == null)
                return null;

            string colorName;
            switch (click.Button)
            {
                case MouseButtons.Left:
                    colorName = ClickHighlightSettings.GetColorName(settings.LeftClickColor, "OrangeRed");
                    break;
                case MouseButtons.Middle:
                    colorName = ClickHighlightSettings.GetColorName(settings.MiddleClickColor, "Gold");
                    break;
                case MouseButtons.Right:
                    colorName = ClickHighlightSettings.GetColorName(settings.RightClickColor, "RoyalBlue");
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
