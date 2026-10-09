using System;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Windows.Forms;

namespace Carnac.Logic.KeyMonitor
{
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class InterceptKeys : IInterceptKeys
    {
        const int AltGrControlScanCode = 0x21D;

        public static readonly InterceptKeys Current = new InterceptKeys();
        readonly IObservable<InterceptKeyEventArgs> keyStream;
        volatile ILogger logger = NullLogger.Instance;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        Win32Methods.LowLevelKeyboardProc callback;

        InterceptKeys()
        {
            keyStream = Observable.Create<InterceptKeyEventArgs>(observer =>
            {
                Debug.Write("Subscribed to keys");
                IntPtr hookId = IntPtr.Zero;
                // Need to hold onto this callback, otherwise it will get GC'd as it is an unmanged callback
                callback = (nCode, wParam, lParam) =>
                {
                    if (nCode >= 0)
                    {
                        try
                        {
                            var eventArgs = CreateEventArgs(wParam, lParam);
                            observer.OnNext(eventArgs);
                            if (eventArgs.Handled)
                                return (IntPtr)1;
                        }
                        catch (Exception ex)
                        {
                            // An exception that escapes into the native hook procedure takes the whole process down
                            // (the "silent crash"). Log it and let the key through to the application as usual.
                            Logger.Error("An exception was thrown while handling a key press in the keyboard hook", ex);
                        }
                    }

                    // ReSharper disable once AccessToModifiedClosure
                    return Win32Methods.CallNextHookEx(hookId, nCode, wParam, lParam);
                };
                hookId = SetHook(callback);
                return Disposable.Create(() =>
                {
                    Debug.Write("Unsubscribed from keys");
                    Win32Methods.UnhookWindowsHookEx(hookId);
                    callback = null;
                });
            })
            .Publish().RefCount();
        }

        /// <summary>Where exceptions from the hook procedure are reported. Set once at startup; nothing is logged until then.</summary>
        public ILogger Logger
        {
            get { return logger; }
            set { logger = value ?? NullLogger.Instance; }
        }

        public IObservable<InterceptKeyEventArgs> GetKeyStream()
        {
            return keyStream;
        }

        internal static InterceptKeyEventArgs CreateEventArgs(IntPtr wParam, IntPtr lParam)
        {
            bool alt = (Control.ModifierKeys & Keys.Alt) != 0;
            bool control = (Control.ModifierKeys & Keys.Control) != 0;
            bool shift = (Control.ModifierKeys & Keys.Shift) != 0;
            bool keyDown = wParam == (IntPtr)Win32Methods.WM_KEYDOWN;
            bool keyUp = wParam == (IntPtr)Win32Methods.WM_KEYUP;
            int vkCode = Marshal.ReadInt32(lParam);
            var key = (Keys)vkCode;
            //http://msdn.microsoft.com/en-us/library/windows/desktop/ms646286(v=vs.85).aspx
            // A key pressed while Alt is down is a system key, and so are the Alt keys themselves
            var isAltKey = key == Keys.RMenu || key == Keys.LMenu;
            if (wParam == (IntPtr)Win32Methods.WM_SYSKEYDOWN)
            {
                alt = alt || !isAltKey;
                keyDown = true;
            }
            if (wParam == (IntPtr)Win32Methods.WM_SYSKEYUP)
            {
                alt = alt || !isAltKey;
                keyUp = true;
            }

            // the Control key that Windows adds in front of AltGr has this scan code (KBDLLHOOKSTRUCT.scanCode)
            var isAltGrControl = key == Keys.LControlKey && Marshal.ReadInt32(lParam, 4) == AltGrControlScanCode;

            return new InterceptKeyEventArgs(
                key,
                keyDown ?
                KeyDirection.Down : keyUp
                ? KeyDirection.Up : KeyDirection.Unknown,
                alt, control, shift) { IsFromKeyboardHook = true, IsAltGrControl = isAltGrControl };
        }

        static IntPtr SetHook(Win32Methods.LowLevelKeyboardProc proc)
        {
            // NOTE: This requires FullTrust to use the Process class.
            //       There don't seem to be alternatives to achieving this in
            //       MediumTrust environment which is fine because that's a
            //       concept that has long been obsoleted. But just a warning
            //       if you ever try and run Carnac in that sort of way.
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return Win32Methods.SetWindowsHookEx(Win32Methods.WH_KEYBOARD_LL, proc, Win32Methods.GetModuleHandle(curModule.ModuleName), 0);
            }
        }
    }
}