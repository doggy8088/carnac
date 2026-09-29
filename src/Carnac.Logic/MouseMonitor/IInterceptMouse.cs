using System;

namespace Carnac.Logic.MouseMonitor
{
    public interface IInterceptMouse
    {
        /// <summary>
        /// The left, middle and right mouse button presses anywhere on the screen. Nothing is listened to
        /// until the stream is subscribed to, and only for as long as it is.
        /// </summary>
        IObservable<MouseClick> GetClickStream();
    }
}
