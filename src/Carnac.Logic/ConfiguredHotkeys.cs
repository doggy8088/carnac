namespace Carnac.Logic
{
    /// <summary>
    /// Turns the hotkey strings stored in the settings into the hotkeys that are really in effect.
    /// A <c>null</c> result means "no hotkey".
    /// </summary>
    public static class ConfiguredHotkeys
    {
        /// <summary>Default of <c>PopupSettings.SilentModeHotkey</c>; the combination Carnac has always used.</summary>
        public const string DefaultSilentMode = "Ctrl+Alt+P";

        static readonly KeyPressDefinition DefaultSilentModeHotkey = ParseOrNull(DefaultSilentMode);

        /// <summary>
        /// Settings that were never written (<c>null</c>) or that hold a value nobody can type in the preferences
        /// (a hand-edited file) fall back to the default, because silent mode is a privacy feature.
        /// An empty value is a deliberate "no hotkey".
        /// </summary>
        public static KeyPressDefinition ResolveSilentMode(string configured)
        {
            if (configured == null)
                return DefaultSilentModeHotkey;

            if (configured.Trim().Length == 0)
                return null;

            return ParseOrNull(configured) ?? DefaultSilentModeHotkey;
        }

        /// <summary>The pause hotkey is off unless a valid hotkey is configured.</summary>
        public static KeyPressDefinition ResolvePause(string configured)
        {
            return ParseOrNull(configured);
        }

        static KeyPressDefinition ParseOrNull(string text)
        {
            KeyPressDefinition hotkey;
            string error;
            return HotkeyParser.TryParse(text, out hotkey, out error) ? hotkey : null;
        }
    }
}
