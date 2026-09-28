using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Carnac.Logic.KeyMonitor
{
    public class InterceptKeyEventArgs : EventArgs
    {
        public InterceptKeyEventArgs(Keys key, KeyDirection keyDirection, bool altPressed, bool controlPressed, bool shiftPressed)
        {
            AltPressed = altPressed;
            ControlPressed = controlPressed;
            Key = key;
            KeyDirection = keyDirection;
            ShiftPressed = shiftPressed;
        }

        public bool Handled { get; set; }
        public bool AltPressed { get; private set; }
        public bool ControlPressed { get; private set; }
        public bool ShiftPressed { get; private set; }
        public Keys Key { get; private set; }
        public KeyDirection KeyDirection { get; private set; }

        public bool IsLetter()
        {
            return Key >= Keys.A && Key <= Keys.Z;
        }

        public bool IsModifier()
        {
            return ModifierKeys.Contains(Key);
        }

        static readonly HashSet<Keys> ModifierKeys = new HashSet<Keys>
        {
            Keys.LControlKey,
            Keys.RControlKey,
            Keys.LShiftKey,
            Keys.RShiftKey,
            Keys.LMenu,
            Keys.RMenu,
            Keys.ShiftKey,
            Keys.Shift,
            Keys.Alt,
            Keys.LWin,
            Keys.RWin
        };

        /// <summary>
        /// The event comes from the keyboard hook, so the state of the keyboard at this moment is the real one.
        /// It is not for events that are made up, as the tests do.
        /// </summary>
        public bool IsFromKeyboardHook { get; internal set; }
    }
}