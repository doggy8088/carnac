using System;
using System.Runtime.InteropServices;
using Carnac.Logic.Overlay;

namespace Carnac.Logic
{
    public static class Win32Methods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ReSharper disable InconsistentNaming
        //

        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 256;
        public const int WM_KEYUP = 257;
        public const int WM_SYSKEYUP = 261;
        public const int WM_SYSKEYDOWN = 260;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public  const int GWL_EXSTYLE = (-20);

        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_FRAMECHANGED = 0x0020;
        const uint SWP_SHOWWINDOW = 0x0040;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        //
        // ReSharper restore InconsistentNaming

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        /// <summary>
        /// Makes the window click-through and keeps it from ever taking the focus; it is hidden from window lists
        /// unless <paramref name="captureFriendly"/> is set. Can be called again while the window is shown.
        /// </summary>
        public static void ApplyOverlayWindowStyles(IntPtr hwnd, bool captureFriendly)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            var newStyle = OverlayWindowStyles.Apply(extendedStyle, captureFriendly);
            if (newStyle == extendedStyle)
                return;

            SetWindowLong(hwnd, GWL_EXSTYLE, newStyle);
            // Windows caches the frame (and the shell what it knows about the window) until it is told that it changed.
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        /// <summary>
        /// Moves and resizes the window to a rectangle in virtual desktop pixels, without activating it or changing its z-order.
        /// </summary>
        public static bool SetWindowRect(IntPtr hwnd, PixelRect rect)
        {
            return SetWindowPos(hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Puts the window back on top of the topmost windows, without activating it, moving it or resizing it.
        /// </summary>
        public static bool BringToTopmost(IntPtr hwnd)
        {
            return SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }
    }
}