using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;

namespace Carnac.Logic.MouseMonitor
{
    /// <summary>
    /// Listens to the mouse buttons with a low-level mouse hook, the same way <see cref="KeyMonitor.InterceptKeys"/>
    /// listens to the keyboard. Every subscription has a hook of its own, which is gone with the subscription, and
    /// it never consumes an event. (There is nothing to share: a stream shared between subscribers stays broken
    /// for good when the hook could not be installed once.)
    /// </summary>
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class InterceptMouse : IInterceptMouse
    {
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_MBUTTONDOWN = 0x0207;

        readonly IObservable<MouseClick> clickStream;

        public InterceptMouse()
        {
            clickStream = Observable.Create<MouseClick>(observer =>
            {
                Debug.Write("Subscribed to mouse");
                IntPtr hookId = IntPtr.Zero;

                // The callback is called from native code, so it has to stay reachable for as long as the hook is installed
                Win32Methods.LowLevelKeyboardProc callback = (nCode, wParam, lParam) =>
                {
                    if (nCode >= 0)
                    {
                        try
                        {
                            var click = CreateClick(wParam, lParam);
                            if (click != null)
                                observer.OnNext(click);
                        }
                        catch (Exception)
                        {
                            // never let anything escape into the hook, the mouse has to keep working
                        }
                    }

                    // ReSharper disable once AccessToModifiedClosure
                    return Win32Methods.CallNextHookEx(hookId, nCode, wParam, lParam);
                };

                hookId = Win32Methods.SetHook(Win32Methods.WH_MOUSE_LL, callback);

                return Disposable.Create(() =>
                {
                    Debug.Write("Unsubscribed from mouse");
                    Win32Methods.UnhookWindowsHookEx(hookId);
                    GC.KeepAlive(callback);
                });
            });
        }

        public IObservable<MouseClick> GetClickStream()
        {
            return clickStream;
        }

        /// <summary>The click a mouse hook message stands for, or null for anything but a left, middle or right button press.</summary>
        public static MouseClick CreateClick(IntPtr wParam, IntPtr lParam)
        {
            MouseButtons button;
            switch (wParam.ToInt64())
            {
                case WM_LBUTTONDOWN:
                    button = MouseButtons.Left;
                    break;
                case WM_MBUTTONDOWN:
                    button = MouseButtons.Middle;
                    break;
                case WM_RBUTTONDOWN:
                    button = MouseButtons.Right;
                    break;
                default:
                    return null;
            }

            var hookData = (MouseHookData)Marshal.PtrToStructure(lParam, typeof(MouseHookData));
            return new MouseClick(hookData.X, hookData.Y, button);
        }

        // MSLLHOOKSTRUCT: only the position is used
        [StructLayout(LayoutKind.Sequential)]
        struct MouseHookData
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }
    }
}
