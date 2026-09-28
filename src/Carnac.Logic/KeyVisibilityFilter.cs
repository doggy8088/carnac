using System;
using System.Linq;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;

namespace Carnac.Logic
{
    /// <summary>
    /// Decides from the settings which key presses are shown: the key groups of
    /// <see cref="PopupSettings.VisibleKeyCategories"/> and the <see cref="PopupSettings.IgnoredKeys"/> list.
    /// Both are read from the settings for every message, so changes in Preferences apply immediately.
    /// <list type="bullet">
    /// <item>Keys pressed with Ctrl, Alt or the Windows key, and shortcuts recognised from the keymaps, are always shown
    /// by the key groups (they are "shortcuts").</item>
    /// <item>An ignored key is never shown, even as a shortcut. A recognised chord is only hidden when every one of its
    /// key presses is ignored, so an ignored key still completes a chord.</item>
    /// </list>
    /// </summary>
    public class KeyVisibilityFilter
    {
        readonly PopupSettings settings;
        readonly KeyIgnoreList ignoreList;

        /// <param name="settings">The live settings.</param>
        /// <param name="warn">Receives a message for every ignore list entry that is not understood; null writes to the trace listeners.</param>
        public KeyVisibilityFilter(PopupSettings settings, Action<string> warn = null)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.settings = settings;
            ignoreList = new KeyIgnoreList(warn);
        }

        /// <summary>
        /// True when the message is shown. Meant for the messages the shortcut accumulator produces, before they are merged:
        /// a message is shown when at least one of its key presses is.
        /// </summary>
        public bool IsVisible(Message message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            var keyPresses = message.KeyPresses;
            if (keyPresses.Count == 0)
                return true;

            var categories = settings.VisibleKeyCategories;
            var ignoredKeys = settings.IgnoredKeys;
            return keyPresses.Any(key => IsKeyVisible(key, message.IsShortcut, categories, ignoredKeys));
        }

        bool IsKeyVisible(KeyPress key, bool isPartOfShortcut, KeyCategory categories, string ignoredKeys)
        {
            if (ignoreList.IsIgnored(ignoredKeys, key))
                return false;

            if (isPartOfShortcut || key.HasModifierPressed)
                return true;

            return (categories & KeyCategories.For(key.Key)) != KeyCategory.None;
        }
    }
}
