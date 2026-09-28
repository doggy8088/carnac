using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Carnac.Logic
{
    /// <summary>
    /// Translates a key to the character it types on a keyboard layout, so Carnac shows what is on the keys of
    /// the user's keyboard and not what the same key types on a US keyboard.
    /// </summary>
    /// <remarks>
    /// This runs inside the low-level keyboard hook, so it must never disturb what the user is typing:
    /// ToUnicodeEx normally changes the kernel's dead-key state, which would swallow or alter accents typed in
    /// other applications. The "do not change the keyboard state" flag avoids that, but only exists from
    /// Windows 10 version 1607; on older systems nothing is translated and the US names are used.
    /// </remarks>
    public class KeyboardLayoutTranslator : IKeyboardLayoutTranslator
    {
        const uint DoNotChangeKeyboardState = 0x4;
        const uint MapVirtualKeyToScanCode = 0;
        const byte KeyDown = 0x80;

        const int VkShift = 0x10;
        const int VkControl = 0x11;
        const int VkMenu = 0x12;
        const int VkLShift = 0xA0;
        const int VkLControl = 0xA2;
        const int VkRMenu = 0xA5;

        const uint Windows10Version1607Build = 14393;

        static readonly bool isSupported = IsWindows10Version1607OrLater();

        readonly Func<IntPtr> layoutProvider;

        /// <summary>Translates with the layout of the window that has the focus.</summary>
        public KeyboardLayoutTranslator()
            : this(GetForegroundWindowLayout)
        {
        }

        public KeyboardLayoutTranslator(Func<IntPtr> layoutProvider)
        {
            if (layoutProvider == null)
            {
                throw new ArgumentNullException("layoutProvider");
            }

            this.layoutProvider = layoutProvider;
        }

        public static bool IsSupported
        {
            get { return isSupported; }
        }

        /// <summary>The keys whose text depends on the layout: letters, the digit row and the punctuation keys.</summary>
        public static bool IsCharacterKey(Keys key)
        {
            var code = (int)key;
            return (key >= Keys.A && key <= Keys.Z)
                || (key >= Keys.D0 && key <= Keys.D9)
                || (code >= 186 && code <= 192)     // VK_OEM_1 .. VK_OEM_3: ; = , - . / `  (on a US layout)
                || (code >= 219 && code <= 223)     // VK_OEM_4 .. VK_OEM_8: [ \ ] '  (on a US layout)
                || code == 226                      // VK_OEM_102
                || key == Keys.Decimal;
        }

        public string GetText(Keys key, bool shift, bool altGr)
        {
            if (!isSupported || !IsCharacterKey(key))
            {
                return null;
            }

            try
            {
                return Translate(key, shift, altGr, layoutProvider());
            }
            catch (Exception)
            {
                // never let a translation problem escape into the keyboard hook
                return null;
            }
        }

        /// <summary>Translates with an explicit layout. Also usable directly by callers that hold a layout handle.</summary>
        public static string Translate(Keys key, bool shift, bool altGr, IntPtr layout)
        {
            if (!isSupported || !IsCharacterKey(key))
            {
                return null;
            }

            var virtualKey = (uint)key;
            var scanCode = MapVirtualKeyEx(virtualKey, MapVirtualKeyToScanCode, layout);

            // Not GetKeyboardState: inside a low-level hook it does not reflect the key being processed.
            // CapsLock is left out on purpose, Carnac has always ignored it.
            var keyState = new byte[256];
            if (shift)
            {
                keyState[VkShift] = KeyDown;
                keyState[VkLShift] = KeyDown;
            }
            if (altGr)
            {
                keyState[VkControl] = KeyDown;
                keyState[VkLControl] = KeyDown;
                keyState[VkMenu] = KeyDown;
                keyState[VkRMenu] = KeyDown;
            }

            var buffer = new char[8];
            var count = ToUnicodeEx(virtualKey, scanCode, keyState, buffer, buffer.Length, DoNotChangeKeyboardState, layout);

            // 0: no translation. Negative: a dead key, whose accent is in the buffer.
            var length = Math.Abs(count);
            if (length == 0 || length > buffer.Length)
            {
                return null;
            }

            if (char.IsControl(buffer[0]))
            {
                return null;
            }

            return new string(buffer, 0, length);
        }

        static IntPtr GetForegroundWindowLayout()
        {
            var foregroundWindow = GetForegroundWindow();
            uint processId;
            var threadId = foregroundWindow == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foregroundWindow, out processId);

            // thread 0 is the calling thread
            return GetKeyboardLayout(threadId);
        }

        static bool IsWindows10Version1607OrLater()
        {
            try
            {
                var info = new OsVersionInfo { OsVersionInfoSize = (uint)Marshal.SizeOf(typeof(OsVersionInfo)) };
                // Environment.OSVersion reports 6.2 for a process without a compatibility manifest
                if (RtlGetVersion(ref info) != 0)
                {
                    return false;
                }

                return info.MajorVersion > 10 || (info.MajorVersion == 10 && info.BuildNumber >= Windows10Version1607Build);
            }
            catch (Exception)
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OsVersionInfo
        {
            public uint OsVersionInfoSize;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint BuildNumber;
            public uint PlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string ServicePack;
        }

        [DllImport("ntdll.dll")]
        static extern int RtlGetVersion(ref OsVersionInfo versionInfo);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, [Out] char[] buffer, int bufferSize, uint flags, IntPtr layout);
    }
}
