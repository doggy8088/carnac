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
        public static KeyPress Create(string processName, Keys key, bool control = false, bool shift = false, bool alt = false)
        {
            var args = new InterceptKeyEventArgs(key, KeyDirection.Down, alt, control, shift);
            return new KeyPress(new ProcessInfo(processName), args, false, ToInput(key, control, shift, alt));
        }

        // Mirrors what KeyProvider produces for the common cases.
        static IEnumerable<string> ToInput(Keys key, bool control, bool shift, bool alt)
        {
            var input = new List<string>();
            if (control)
                input.Add("Ctrl");
            if (alt)
                input.Add("Alt");
            if (shift && (control || alt))
                input.Add("Shift");

            var isLetter = key >= Keys.A && key <= Keys.Z;
            input.Add(isLetter && !control && !alt && !shift ? key.ToString().ToLowerInvariant() : key.Sanitise());
            return input;
        }
    }
}
