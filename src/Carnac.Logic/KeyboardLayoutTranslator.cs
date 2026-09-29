using System;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
    /// other applications. The "do not change keyboard state" flag avoids that, but only exists from
    /// Windows 10 version 1607; on older systems nothing is translated and the US names are used.
    /// The flag stops Carnac from changing the state, not from seeing it: when a dead key was typed in the
    /// application just before, the translation of the next key includes the accent, as it does for the application.
    /// So the dead key itself is shown as nothing (empty text) and the key after it as what the application gets.
    /// </remarks>
    public class KeyboardLayoutTranslator : IKeyboardLayoutTranslator
    {
        const uint DoNotChangeKeyboardState = 0x4;
        const uint MapVirtualKeyToScanCode = 0;
        const byte KeyDown = 0x80;

        const int VkShift = 0x10;
        const int VkControl = 0x11;
        const int VkMenu = 0x12;
        const int VkCapital = 0x14;
        const int VkLShift = 0xA0;
        const int VkLControl = 0xA2;
        const int VkRMenu = 0xA5;

        const uint Windows10Version1607Build = 14393;

        static readonly bool isSupported = IsWindows10Version1607OrLater();
        static readonly uint GuiThreadInfoSize = (uint)Marshal.SizeOf(typeof(GuiThreadInfo));

        // the hook calls from one thread, so the buffers of a call can be reused by the next one
        [ThreadStatic]
        static byte[] keyStateBuffer;
        [ThreadStatic]
        static char[] textBuffer;

        readonly Func<IntPtr> layoutProvider;
        readonly Func<bool> isRightAltDown;
        readonly Func<bool> isCapsLockOn;

        /// <summary>Translates with the layout of the window that has the focus.</summary>
        public KeyboardLayoutTranslator()
            : this(GetFocusedWindowLayout)
        {
        }

        public KeyboardLayoutTranslator(Func<IntPtr> layoutProvider)
            : this(layoutProvider, IsRightAltPhysicallyDown)
        {
        }

        public KeyboardLayoutTranslator(Func<IntPtr> layoutProvider, Func<bool> isRightAltDown)
            : this(layoutProvider, isRightAltDown, IsCapsLockSwitchedOn)
        {
        }

        /// <param name="layoutProvider">The layout to translate with, or <see cref="IntPtr.Zero"/> when it is not known.</param>
        /// <param name="isRightAltDown">Whether the right Alt key is down, which is what makes Ctrl+Alt AltGr.</param>
        /// <param name="isCapsLockOn">Whether Caps Lock is on, which changes what the digit row and punctuation types on some layouts.</param>
        public KeyboardLayoutTranslator(Func<IntPtr> layoutProvider, Func<bool> isRightAltDown, Func<bool> isCapsLockOn)
        {
            if (layoutProvider == null)
            {
                throw new ArgumentNullException("layoutProvider");
            }
            if (isRightAltDown == null)
            {
                throw new ArgumentNullException("isRightAltDown");
            }
            if (isCapsLockOn == null)
            {
                throw new ArgumentNullException("isCapsLockOn");
            }

            this.layoutProvider = layoutProvider;
            this.isRightAltDown = isRightAltDown;
            this.isCapsLockOn = isCapsLockOn;
        }

        public static bool IsSupported
        {
            get { return isSupported; }
        }

        /// <summary>
        /// The keys whose text depends on the layout: letters, the digit row and the punctuation keys. Space is one too:
        /// it types the accent that is pending after a dead key.
        /// </summary>
        public static bool IsCharacterKey(Keys key)
        {
            var code = (int)key;
            return (key >= Keys.A && key <= Keys.Z)
                || (key >= Keys.D0 && key <= Keys.D9)
                || (code >= 186 && code <= 192)     // VK_OEM_1 .. VK_OEM_3: ; = , - . / `  (on a US layout)
                || (code >= 219 && code <= 223)     // VK_OEM_4 .. VK_OEM_8: [ \ ] '  (on a US layout)
                || code == 226                      // VK_OEM_102
                || code == 193 || code == 194       // VK_ABNT_C1, VK_ABNT_C2: the extra keys of Brazilian keyboards
                || key == Keys.Decimal
                || key == Keys.Space;
        }

        /// <param name="controlAlt">
        /// Ctrl and Alt are both down. That is how Windows reports AltGr, and also what a Ctrl+Alt shortcut is, so it
        /// counts as AltGr when the right Alt key is down.
        /// </param>
        /// <returns>The text; empty when the key types nothing that can be seen (a dead key, a joiner); null when there is no answer.</returns>
        public string GetText(Keys key, bool shift, bool controlAlt)
        {
            if (!isSupported || !IsCharacterKey(key))
            {
                return null;
            }

            try
            {
                if (controlAlt && !isRightAltDown())
                {
                    return null;
                }

                var layout = layoutProvider();
                return layout == IntPtr.Zero ? null : Translate(key, shift, controlAlt, layout, isCapsLockOn());
            }
            catch (Exception)
            {
                // never let a translation problem escape into the keyboard hook
                return null;
            }
        }

        /// <summary>Translates with an explicit layout.</summary>
        /// <param name="capsLock">
        /// Caps Lock is on. It only counts for what is not a letter: Carnac shows letters without the capitals of Caps
        /// Lock (also the letters on the punctuation keys of a layout), but on some layouts (French, Belgian) the digit
        /// row types digits with it.
        /// </param>
        public static string Translate(Keys key, bool shift, bool altGr, IntPtr layout, bool capsLock = false)
        {
            var text = TranslateWith(key, shift, altGr, layout, capsLock && !(key >= Keys.A && key <= Keys.Z));

            // Caps Lock made a letter of a punctuation key a capital: it is not to count, as for the other letters
            if (capsLock && !string.IsNullOrEmpty(text) && text.Any(char.IsLetter))
            {
                return TranslateWith(key, shift, altGr, layout, false);
            }

            return text;
        }

        static string TranslateWith(Keys key, bool shift, bool altGr, IntPtr layout, bool capsLock)
        {
            if (!isSupported || !IsCharacterKey(key))
            {
                return null;
            }

            var virtualKey = (uint)key;
            var scanCode = MapVirtualKeyEx(virtualKey, MapVirtualKeyToScanCode, layout);

            // Not GetKeyboardState: inside a low-level hook it does not reflect the key being processed.
            // CapsLock is left out on purpose, Carnac has always ignored it.
            var keyState = keyStateBuffer ?? (keyStateBuffer = new byte[256]);
            Array.Clear(keyState, 0, keyState.Length);
            if (shift)
            {
                keyState[VkShift] = KeyDown;
                keyState[VkLShift] = KeyDown;
            }
            if (capsLock)
            {
                keyState[VkCapital] = 0x01;
            }
            if (altGr)
            {
                keyState[VkControl] = KeyDown;
                keyState[VkLControl] = KeyDown;
                keyState[VkMenu] = KeyDown;
                keyState[VkRMenu] = KeyDown;
            }

            var buffer = textBuffer ?? (textBuffer = new char[8]);
            var count = ToUnicodeEx(virtualKey, scanCode, keyState, buffer, buffer.Length, DoNotChangeKeyboardState, layout);

            // 0: no translation
            var length = Math.Abs(count);
            if (length == 0 || length > buffer.Length)
            {
                return null;
            }

            // Negative: a dead key. It types nothing by itself: the accent comes with the key after it (or with Space).
            if (count < 0)
            {
                return string.Empty;
            }

            var text = GetVisibleText(buffer, length);
            if (text.Length > 0)
            {
                return text;
            }

            // Space types a space, which is the name it already has; every other key that types nothing visible
            // (a joiner, a direction mark, a no-break space) is shown as nothing
            return key == Keys.Space ? null : string.Empty;
        }

        // Some layouts type control characters, joiners, direction marks or a no-break space with a key: nothing to show
        static string GetVisibleText(char[] buffer, int length)
        {
            var visible = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                switch (CharUnicodeInfo.GetUnicodeCategory(buffer[i]))
                {
                    case UnicodeCategory.Control:
                    case UnicodeCategory.Format:
                    case UnicodeCategory.SpaceSeparator:
                    case UnicodeCategory.LineSeparator:
                    case UnicodeCategory.ParagraphSeparator:
                        break;
                    default:
                        visible.Append(buffer[i]);
                        break;
                }
            }

            return visible.ToString();
        }

        // The layout belongs to the thread that has the focus, which is not always the thread of the foreground window
        // (a child window of another thread). Without a window there is no layout to name keys with.
        public static IntPtr GetFocusedWindowLayout()
        {
            var info = new GuiThreadInfo { Size = GuiThreadInfoSize };
            if (!GetGUIThreadInfo(0, ref info))
            {
                return IntPtr.Zero;
            }

            var window = info.FocusWindow != IntPtr.Zero ? info.FocusWindow : info.ActiveWindow;
            if (window == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            uint processId;
            var threadId = GetWindowThreadProcessId(window, out processId);
            return threadId == 0 ? IntPtr.Zero : GetKeyboardLayout(threadId);
        }

        static bool IsRightAltPhysicallyDown()
        {
            return (GetAsyncKeyState(VkRMenu) & 0x8000) != 0;
        }

        static bool IsCapsLockSwitchedOn()
        {
            return (GetKeyState(VkCapital) & 0x0001) != 0;
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

        // GUITHREADINFO
        [StructLayout(LayoutKind.Sequential)]
        struct GuiThreadInfo
        {
            public uint Size;
            public uint Flags;
            public IntPtr ActiveWindow;
            public IntPtr FocusWindow;
            public IntPtr CaptureWindow;
            public IntPtr MenuOwnerWindow;
            public IntPtr MoveSizeWindow;
            public IntPtr CaretWindow;
            public int CaretLeft;
            public int CaretTop;
            public int CaretRight;
            public int CaretBottom;
        }

        [DllImport("ntdll.dll")]
        static extern int RtlGetVersion(ref OsVersionInfo versionInfo);

        [DllImport("user32.dll")]
        static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint threadId);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKeyEx(uint code, uint mapType, IntPtr layout);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll")]
        static extern short GetKeyState(int virtualKey);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, [Out] char[] buffer, int bufferSize, uint flags, IntPtr layout);
    }
}
