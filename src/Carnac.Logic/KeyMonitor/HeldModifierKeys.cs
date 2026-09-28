using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Carnac.Logic.KeyMonitor
{
    /// <summary>Which of the modifier keys are down.</summary>
    internal struct ModifierState
    {
        public ModifierState(bool control, bool alt, bool win, bool shift)
        {
            Control = control;
            Alt = alt;
            Win = win;
            Shift = shift;
        }

        public readonly bool Control;
        public readonly bool Alt;
        public readonly bool Win;
        public readonly bool Shift;

        public bool IsAnyDown
        {
            get { return Control || Alt || Win || Shift; }
        }

        /// <summary>Shift is what capital letters are typed with, so it does not make a shortcut.</summary>
        public bool IsAnyDownOtherThanShift
        {
            get { return Control || Alt || Win; }
        }

        /// <summary>Named and ordered like the modifiers in front of a key: Ctrl, Alt, Win, Shift.</summary>
        public string[] GetNames()
        {
            var names = new List<string>();
            if (Control) names.Add("Ctrl");
            if (Alt) names.Add("Alt");
            if (Win) names.Add("Win");
            if (Shift) names.Add("Shift");
            return names.ToArray();
        }
    }

    /// <summary>
    /// The modifier keys that are down right now, as far as the key events tell: a repeated key down of a key that is
    /// held is not a new press, and the left and right key of a modifier are tracked apart.
    /// </summary>
    internal sealed class HeldModifierKeys
    {
        readonly HashSet<Keys> keys = new HashSet<Keys>();
        readonly object sync = new object();

        /// <returns>true when the key was not down yet, false for the repeat of a key that is held.</returns>
        public bool Press(Keys key)
        {
            lock (sync)
                return keys.Add(key);
        }

        public void Release(Keys key)
        {
            lock (sync)
                keys.Remove(key);
        }

        public void Clear()
        {
            lock (sync)
                keys.Clear();
        }

        /// <summary>
        /// The key up of a modifier is not always seen (the secure desktop of a UAC prompt or Ctrl+Alt+Del, a
        /// disconnected session), which leaves it held for ever. Forget the keys that are not down any more.
        /// </summary>
        /// <param name="isDown">Whether a key really is down.</param>
        /// <param name="except">The key of the event that is being handled, whose state is not settled yet.</param>
        public void ForgetReleased(Func<Keys, bool> isDown, Keys except)
        {
            lock (sync)
            {
                if (keys.Count == 0)
                    return;

                foreach (var key in keys.Where(k => k != except).ToList())
                {
                    if (!isDown(key))
                        keys.Remove(key);
                }
            }
        }

        public ModifierState GetState()
        {
            lock (sync)
            {
                return new ModifierState(
                    keys.Contains(Keys.LControlKey) || keys.Contains(Keys.RControlKey),
                    keys.Contains(Keys.LMenu) || keys.Contains(Keys.RMenu),
                    keys.Contains(Keys.LWin) || keys.Contains(Keys.RWin),
                    keys.Contains(Keys.LShiftKey) || keys.Contains(Keys.RShiftKey));
            }
        }

        public static bool IsKeyPhysicallyDown(Keys key)
        {
            return (GetAsyncKeyState((int)key) & 0x8000) != 0;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int virtualKey);
    }
}
