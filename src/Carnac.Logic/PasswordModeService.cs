using System;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;

namespace Carnac.Logic
{
    /// <summary>
    /// Watches the key stream for the configured silent-mode and pause hotkeys and applies them to the shared
    /// <see cref="IKeyDisplayState"/>. The hotkeys are read from the settings on every key press, so a change
    /// in the preferences is effective immediately, without a restart.
    /// </summary>
    public class PasswordModeService : IPasswordModeService
    {
        readonly IKeyDisplayState displayState;
        readonly LiveHotkey silentModeHotkey;
        readonly LiveHotkey pauseHotkey;

        /// <summary>Creates a service with its own private state and the default settings; the application passes the shared ones.</summary>
        public PasswordModeService()
            : this(new KeyDisplayState(), new PopupSettings())
        {
        }

        public PasswordModeService(IKeyDisplayState displayState, PopupSettings settings)
        {
            if (displayState == null)
            {
                throw new ArgumentNullException("displayState");
            }

            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }

            this.displayState = displayState;
            silentModeHotkey = new LiveHotkey(() => settings.SilentModeHotkey, ConfiguredHotkeys.ResolveSilentMode);
            pauseHotkey = new LiveHotkey(() => settings.PauseHotkey, ConfiguredHotkeys.ResolvePause);
        }

        public bool CheckPasswordMode(InterceptKeyEventArgs key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }

            if (key.KeyDirection == KeyDirection.Down)
            {
                // The hotkey itself is never shown, neither when it switches a mode on nor when it switches it off.
                if (Matches(silentModeHotkey.Current, key))
                {
                    displayState.ToggleSilent();
                    return true;
                }

                if (Matches(pauseHotkey.Current, key))
                {
                    displayState.TogglePaused();
                    return true;
                }
            }

            return displayState.IsSilent;
        }

        static bool Matches(KeyPressDefinition hotkey, InterceptKeyEventArgs key)
        {
            return hotkey != null
                   && key.Key == hotkey.Key
                   && key.ControlPressed == hotkey.ControlPressed
                   && key.AltPressed == hotkey.AltPressed
                   && key.ShiftPressed == hotkey.ShiftPressed;
        }

        /// <summary>
        /// A hotkey that follows a setting. The (cheap) text comparison per key press avoids parsing the same text again and again,
        /// and the immutable snapshot keeps it safe when it is read from more than one thread.
        /// </summary>
        sealed class LiveHotkey
        {
            readonly Func<string> readSetting;
            readonly Func<string, KeyPressDefinition> resolve;
            Snapshot snapshot;

            public LiveHotkey(Func<string> readSetting, Func<string, KeyPressDefinition> resolve)
            {
                this.readSetting = readSetting;
                this.resolve = resolve;
            }

            public KeyPressDefinition Current
            {
                get
                {
                    var text = readSetting();
                    var current = snapshot;
                    if (current == null || !string.Equals(current.Text, text, StringComparison.Ordinal))
                    {
                        current = new Snapshot(text, resolve(text));
                        snapshot = current;
                    }

                    return current.Hotkey;
                }
            }

            sealed class Snapshot
            {
                public Snapshot(string text, KeyPressDefinition hotkey)
                {
                    Text = text;
                    Hotkey = hotkey;
                }

                public string Text { get; private set; }
                public KeyPressDefinition Hotkey { get; private set; }
            }
        }
    }
}
