using System.Collections.Generic;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;

namespace Carnac.Tests
{
    /// <summary>Builds KeyPress objects for tests that feed a message pipeline without the keyboard hook.</summary>
    public static class KeyPresses
    {
        public static KeyPress Create(string processName, Keys key, bool control = false, bool shift = false, bool alt = false, bool win = false)
        {
            var args = new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift);
            return new KeyPress(new ProcessInfo(processName), args, win, ToInput(key, control, shift, alt, win));
        }

        // Mirrors KeyProvider.ToInputs.
        static IEnumerable<string> ToInput(Keys key, bool control, bool shift, bool alt, bool win)
        {
            var input = new List<string>();
            if (control)
                input.Add("Ctrl");
            if (alt)
                input.Add("Alt");
            if (win)
                input.Add("Win");

            if (control || alt || win)
            {
                if (shift)
                    input.Add("Shift");
                input.Add(key.Sanitise());
                return input;
            }

            var isLetter = key >= Keys.A && key <= Keys.Z;
            string shifted;
            var shiftModifiesInput = key.SanitiseShift(out shifted);
            if (!isLetter && !shiftModifiesInput && shift)
                input.Add("Shift");

            if (shift && shiftModifiesInput)
                input.Add(shifted);
            else if (isLetter && !shift)
                input.Add(key.ToString().ToLowerInvariant());
            else
                input.Add(key.Sanitise());
            return input;
        }
    }
}
