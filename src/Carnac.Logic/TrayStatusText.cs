using System.Collections.Generic;

namespace Carnac.Logic
{
    /// <summary>
    /// Builds the tooltip of the tray icon. Windows also exposes this text as the accessible name of the
    /// notification-area button, so screen readers announce it.
    /// </summary>
    public static class TrayStatusText
    {
        /// <summary>NotifyIcon.Text throws for anything longer than 63 characters.</summary>
        public const int MaxLength = 63;

        // an en dash and an ellipsis, spelled as code points to keep this source file ASCII
        static readonly string Separator = " " + (char)0x2013 + " ";
        static readonly string Ellipsis = ((char)0x2026).ToString();

        /// <summary>
        /// "Carnac" while everything is active, otherwise for example "Carnac - paused" or "Carnac - paused, silent mode".
        /// </summary>
        public static string Compose(string appName, bool paused, bool silent, string pausedText, string silentText)
        {
            var states = new List<string>();
            if (paused && !string.IsNullOrEmpty(pausedText))
                states.Add(pausedText);
            if (silent && !string.IsNullOrEmpty(silentText))
                states.Add(silentText);

            var text = appName ?? string.Empty;
            if (states.Count > 0)
                text += Separator + string.Join(", ", states);

            if (text.Length > MaxLength)
                text = text.Substring(0, MaxLength - Ellipsis.Length) + Ellipsis;

            return text;
        }
    }
}
