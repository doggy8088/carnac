using System;
using System.ComponentModel;
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
    /// listens to the keyboard. The hook only exists while there is a subscriber and it never consumes an event.
    /// </summary>
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class InterceptMouse : IInterceptMouse
    {
        const int WH_MOUSE_LL = 14;
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

                hookId = SetHook(callback);
                if (hookId == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return Disposable.Create(() =>
                {
                    Debug.Write("Unsubscribed from mouse");
                    Win32Methods.UnhookWindowsHookEx(hookId);
                    GC.KeepAlive(callback);
                });
            })
            .Publish().RefCount();
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

        static IntPtr SetHook(Win32Methods.LowLevelKeyboardProc callback)
        {
            using (var currentProcess = Process.GetCurrentProcess())
            using (var currentModule = currentProcess.MainModule)
            {
                return Win32Methods.SetWindowsHookEx(WH_MOUSE_LL, callback, Win32Methods.GetModuleHandle(currentModule.ModuleName), 0);
            }
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
