using System.Collections.Generic;
using System.Windows.Forms;
using Carnac.Logic.KeyMonitor;

namespace Carnac.Tests
{
    public static class KeyStreams
    {
        /// <summary>
        /// A key pressed together with the given modifiers: modifier key downs, the key down and up, modifier key ups.
        /// The key events carry the modifier flags like the real keyboard hook does.
        /// </summary>
        public static KeyPlayer Combination(Keys key, bool shift = false, bool control = false, bool alt = false, bool win = false)
        {
            var player = new KeyPlayer();
            var modifiers = new List<Keys>();
            if (win)
                modifiers.Add(Keys.LWin);
            if (control)
                modifiers.Add(Keys.LControlKey);
            if (alt)
                modifiers.Add(Keys.LMenu);
            if (shift)
                modifiers.Add(Keys.LShiftKey);

            foreach (var modifier in modifiers)
                player.Add(new InterceptKeyEventArgs(modifier, KeyDirection.Down, false, false, false));
            player.Add(new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift));
            player.Add(new InterceptKeyEventArgs(key, KeyDirection.Up, alt, control, shift));
            modifiers.Reverse();
            foreach (var modifier in modifiers)
                player.Add(new InterceptKeyEventArgs(modifier, KeyDirection.Up, false, false, false));
            return player;
        }

        public static KeyPlayer WinkeyE()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.LWin, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.E, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.E, KeyDirection.Up, false, false, false),
                           new InterceptKeyEventArgs(Keys.LWin, KeyDirection.Up, false, false, false),
                       };
        }

        public static KeyPlayer ExclaimationMark()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.D1, KeyDirection.Down, false, false, true),
                           new InterceptKeyEventArgs(Keys.D1, KeyDirection.Up, false, false, true),
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Up, false, false, true),
                       };
        }

        public static KeyPlayer Number1()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.D1, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.D1, KeyDirection.Up, false, false, false)
                       };
        }

        public static KeyPlayer LetterL()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Up, false, false, false),
                       };
        }

        public static KeyPlayer ShiftL()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, false, true),
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Up, false, false, true),
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Up, false, false, true),
                       };
        }

        public static KeyPlayer CtrlShiftL()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Down, false, true, false),
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Down, false, true, true),
                           new InterceptKeyEventArgs(Keys.L, KeyDirection.Up, false, true, true),
                           new InterceptKeyEventArgs(Keys.LShiftKey, KeyDirection.Up, false, true, true),
                           new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Up, false, true, false)
                       };
        }

        public static KeyPlayer CtrlU()
        {
            return new KeyPlayer
                       {
                           new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Down, false, false, false),
                           new InterceptKeyEventArgs(Keys.U, KeyDirection.Down, false, true, false),
                           new InterceptKeyEventArgs(Keys.U, KeyDirection.Up, false, true, false),
                           new InterceptKeyEventArgs(Keys.LControlKey, KeyDirection.Up, false, true, false)
                       };
        }
    }
}