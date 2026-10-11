using System;
using System.Collections.Generic;
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
        const int WM_MOUSEWHEEL = 0x020A;
        const int WM_XBUTTONDOWN = 0x020B;
        const ushort XBUTTON1 = 1;
        const ushort XBUTTON2 = 2;

        // A native hook keeps calling its callback until Windows has taken it away, so the callback is kept alive until
        // then, also when nobody gets round to unsubscribing (the dispatcher that was to do it is shutting down).
        static readonly HashSet<Win32Methods.LowLevelKeyboardProc> InstalledCallbacks = new HashSet<Win32Methods.LowLevelKeyboardProc>();

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
                lock (InstalledCallbacks)
                {
                    InstalledCallbacks.Add(callback);
                }

                return Disposable.Create(() =>
                {
                    Debug.Write("Unsubscribed from mouse");
                    if (Win32Methods.UnhookWindowsHookEx(hookId))
                    {
                        lock (InstalledCallbacks)
                        {
                            InstalledCallbacks.Remove(callback);
                        }
                    }
                });
            });
        }

        public IObservable<MouseClick> GetClickStream()
        {
            return clickStream;
        }

        /// <summary>The mouse action a mouse hook message stands for, or null for messages that are not button presses or wheel scrolls.</summary>
        public static MouseClick CreateClick(IntPtr wParam, IntPtr lParam, Func<Keys, bool> isKeyDown = null)
        {
            var message = wParam.ToInt64();
            switch (message)
            {
                case WM_LBUTTONDOWN:
                case WM_MBUTTONDOWN:
                case WM_RBUTTONDOWN:
                case WM_MOUSEWHEEL:
                case WM_XBUTTONDOWN:
                    break;
                default:
                    return null;
            }

            if (lParam == IntPtr.Zero)
                return null;

            var hookData = (MouseHookData)Marshal.PtrToStructure(lParam, typeof(MouseHookData));
            var button = MouseButtons.None;
            var wheelDelta = 0;

            switch (message)
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
                case WM_MOUSEWHEEL:
                    wheelDelta = unchecked((short)(hookData.MouseData >> 16));
                    if (wheelDelta == 0)
                        return null;
                    break;
                case WM_XBUTTONDOWN:
                    var xButton = (ushort)(hookData.MouseData >> 16);
                    if (xButton == XBUTTON1)
                        button = MouseButtons.XButton1;
                    else if (xButton == XBUTTON2)
                        button = MouseButtons.XButton2;
                    else
                        return null;
                    break;
            }

            var checkKeyDown = isKeyDown ?? KeyMonitor.HeldModifierKeys.IsKeyPhysicallyDown;
            var altPressed = checkKeyDown(Keys.LMenu) || checkKeyDown(Keys.RMenu) || checkKeyDown(Keys.Menu);
            var controlPressed = checkKeyDown(Keys.LControlKey) || checkKeyDown(Keys.RControlKey) || checkKeyDown(Keys.ControlKey);
            var shiftPressed = checkKeyDown(Keys.LShiftKey) || checkKeyDown(Keys.RShiftKey) || checkKeyDown(Keys.ShiftKey);
            var winPressed = checkKeyDown(Keys.LWin) || checkKeyDown(Keys.RWin);
            return new MouseClick(
                hookData.X,
                hookData.Y,
                button,
                wheelDelta,
                altPressed: altPressed,
                controlPressed: controlPressed,
                shiftPressed: shiftPressed,
                winPressed: winPressed)
            {
                IsFromMouseHook = true
            };
        }

        // MSLLHOOKSTRUCT: position and button/wheel data
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
