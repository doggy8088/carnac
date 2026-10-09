using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Microsoft.Win32;
using System.Windows.Media;
using SettingsProviderNet;
using System.Text.RegularExpressions;

namespace Carnac.Logic
{
    public class KeyProvider : IKeyProvider
    {
        readonly IInterceptKeys interceptKeysSource;
        readonly IPasswordModeService passwordModeService;
        readonly IDesktopLockEventService desktopLockEventService;
        readonly PopupSettings settings;
        readonly IKeyboardLayoutTranslator keyboardLayoutTranslator;
        readonly object filterSync = new object();
        string currentFilter = null;
        Regex currentFilterRegex;

        // How to tell whether a key really is down, for the key events that come from the keyboard hook.
        internal Func<Keys, bool> IsKeyDown = HeldModifierKeys.IsKeyPhysicallyDown;

        public KeyProvider(IInterceptKeys interceptKeysSource, IPasswordModeService passwordModeService, IDesktopLockEventService desktopLockEventService, ISettingsProvider settingsProvider)
            : this(interceptKeysSource, passwordModeService, desktopLockEventService, settingsProvider, null)
        {
        }

        /// <param name="keyboardLayoutTranslator">
        /// Names the keys as they are on the keyboard layout of the window that has the focus. Without one the
        /// keys are named as on a US keyboard.
        /// </param>
        public KeyProvider(IInterceptKeys interceptKeysSource, IPasswordModeService passwordModeService, IDesktopLockEventService desktopLockEventService, ISettingsProvider settingsProvider, IKeyboardLayoutTranslator keyboardLayoutTranslator)
        {
            if (settingsProvider == null)
            {
                throw new ArgumentNullException("settingsProvider");
            }

            this.interceptKeysSource = interceptKeysSource;
            this.passwordModeService = passwordModeService;
            this.desktopLockEventService = desktopLockEventService;
            this.keyboardLayoutTranslator = keyboardLayoutTranslator;

            settings = settingsProvider.GetSettings<PopupSettings>();
        }

        private bool ShouldFilterProcess(out Regex filterRegex)
        {
            lock (filterSync)
            {
                var processFilterExpression = settings == null ? null : settings.ProcessFilterExpression;

                if (processFilterExpression != currentFilter)
                {
                    currentFilter = processFilterExpression;

                    if (!string.IsNullOrEmpty(currentFilter))
                    {
                        try
                        {
                            currentFilterRegex = new Regex(currentFilter, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
                        }
                        catch
                        {
                            currentFilterRegex = null;
                        }
                    }
                    else
                    {
                        currentFilterRegex = null;
                    }
                }

                filterRegex = currentFilterRegex;
                return (filterRegex != null);
            }
        }

        public IObservable<KeyPress> GetKeyStream()
        {
            // We are using an observable create to tie the lifetimes of the session switch stream and the keystream
            return Observable.Create<KeyPress>(observer =>
            {
                // The modifiers that are down belong to this subscription
                var heldModifierKeys = new HeldModifierKeys();

                // When desktop is locked we will not get the keyup, because we track the modifier keys
                // specially we need to set them to not being pressed anymore
                var sessionSwitchStreamSubscription = desktopLockEventService.GetSessionSwitchStream()
                .Subscribe(ss =>
                {
                    if (ss.Reason == SessionSwitchReason.SessionLock)
                        heldModifierKeys.Clear();
                }, observer.OnError);

                var keyStreamSubsription = interceptKeysSource.GetKeyStream()
                    .Where(k => ShouldShowKeyPress(k, heldModifierKeys))
                    .Select(k => ToCarnacKeyPress(k, heldModifierKeys))
                    .Where(keypress => keypress != null)
                    .Where(k => !passwordModeService.CheckPasswordMode(k.InterceptKeyEventArgs))
                    .Select(NameByKeyboardLayout)
                    .Where(keypress => keypress != null)
                    .Subscribe(observer);

                return new CompositeDisposable(sessionSwitchStreamSubscription, keyStreamSubsription);
            });
        }

        // Keeps track of the modifier keys that are down, and tells which key events are shown
        bool ShouldShowKeyPress(InterceptKeyEventArgs interceptKeyEventArgs, HeldModifierKeys heldModifierKeys)
        {
            // Nobody pressed the Control key that Windows adds in front of AltGr
            if (interceptKeyEventArgs.IsAltGrControl)
                return false;

            // The key ups of modifiers are not always seen, so ask the keyboard what is really down. Only for events
            // of the keyboard hook (the state of the keyboard means nothing for events that are made up), and only when
            // the modifiers are shown: the state of a key that another program's hook takes away is not reliable, and
            // whoever does not use this should not depend on it.
            if (interceptKeyEventArgs.IsFromKeyboardHook && settings != null && settings.ShowModifierKeyPresses)
            {
                var isKeyUp = interceptKeyEventArgs.KeyDirection == KeyDirection.Up;
                heldModifierKeys.ForgetReleased(IsKeyDown, isKeyUp ? interceptKeyEventArgs.Key : Keys.None);
            }

            if (!interceptKeyEventArgs.IsModifier())
                return interceptKeyEventArgs.KeyDirection == KeyDirection.Down;

            var isNewPress = false;
            if (interceptKeyEventArgs.KeyDirection == KeyDirection.Down)
                isNewPress = heldModifierKeys.Press(interceptKeyEventArgs.Key);
            else if (interceptKeyEventArgs.KeyDirection == KeyDirection.Up)
                heldModifierKeys.Release(interceptKeyEventArgs.Key);

            // Shift on its own is what capital letters are typed with, it is only shown as part of Ctrl/Alt/Win + Shift
            return isNewPress
                && settings != null
                && settings.ShowModifierKeyPresses
                && heldModifierKeys.GetState().IsAnyDownOtherThanShift;
        }

        KeyPress ToCarnacKeyPress(InterceptKeyEventArgs interceptKeyEventArgs, HeldModifierKeys heldModifierKeys)
        {
            var process = AssociatedProcessUtilities.GetAssociatedProcess();
            if (process == null)
            {
                return null;
            }

            // see if this process is one being filtered for
            Regex filterRegex;
            if (ShouldFilterProcess(out filterRegex) && !filterRegex.IsMatch(process.ProcessName))
            {
                return null;
            }

            var modifiers = heldModifierKeys.GetState();
            var isWinKeyPressed = modifiers.Win;
            string[] inputs;
            if (interceptKeyEventArgs.IsModifier())
            {
                // A modifier on its own is shown with the ones that are down with it ("Ctrl + Shift"). What the hook
                // reports as modifier state while the key is being pressed is not reliable, so use the held keys.
                inputs = modifiers.GetNames();
                if (inputs.Length == 0)
                    return null;

                interceptKeyEventArgs = new InterceptKeyEventArgs(interceptKeyEventArgs.Key, interceptKeyEventArgs.KeyDirection,
                    altPressed: modifiers.Alt, controlPressed: modifiers.Control, shiftPressed: modifiers.Shift);
            }
            else
            {
                var isLetter = interceptKeyEventArgs.IsLetter();
                inputs = ToInputs(isLetter, isWinKeyPressed, interceptKeyEventArgs).ToArray();
            }

            try
            {
                string processFileName = process.MainModule.FileName;
                ImageSource image = IconUtilities.GetProcessIconAsImageSource(processFileName);
                return new KeyPress(new ProcessInfo(process.ProcessName, image), interceptKeyEventArgs, isWinKeyPressed, inputs);
            }
            catch (Exception)
            {
                return new KeyPress(new ProcessInfo(process.ProcessName), interceptKeyEventArgs, isWinKeyPressed, inputs);
            }
        }

        IEnumerable<string> ToInputs(bool isLetter, bool isWinKeyPressed, InterceptKeyEventArgs interceptKeyEventArgs)
        {
            var controlPressed = interceptKeyEventArgs.ControlPressed;
            var altPressed = interceptKeyEventArgs.AltPressed;
            var shiftPressed = interceptKeyEventArgs.ShiftPressed;
            if (controlPressed)
                yield return "Ctrl";
            if (altPressed)
                yield return "Alt";
            if (isWinKeyPressed)
                yield return "Win";

            if (controlPressed || altPressed || isWinKeyPressed)
            {
                //Treat as a shortcut, don't be too smart
                if (shiftPressed)
                    yield return "Shift";

                yield return GetShortcutKeyName(interceptKeyEventArgs.Key);
            }
            else
            {
                string input;
                // Shift only turns a key into another character when the key types one. Shift+Insert and Shift+Delete
                // are shortcuts: their Shift must stay visible (the "ins"/"del" shift entries are only keymap aliases).
                var shiftModifiesInput = interceptKeyEventArgs.Key.SanitiseShift(out input)
                    && interceptKeyEventArgs.Key.ProducesCharacter();

                if (!isLetter && !shiftModifiesInput && shiftPressed)
                    yield return "Shift";

                if (interceptKeyEventArgs.ShiftPressed && shiftModifiesInput)
                    yield return input;
                else if (isLetter && !interceptKeyEventArgs.ShiftPressed)
                    yield return interceptKeyEventArgs.Key.ToString().ToLower();
                else
                    yield return interceptKeyEventArgs.Key.Sanitise();
            }
        }

        // A key that types a character on the keyboard layout of the window that has the focus is shown as that
        // character, with AltGr too. It is text like the rest, so without the Ctrl and Alt Windows reports for AltGr:
        // it merges with the text around it and is not taken for a shortcut. A key that types nothing that can be
        // seen (a dead key: the accent comes with the key after it) is not shown at all.
        // This is done after the password mode has had its look at the key as it was pressed, which is what the
        // Ctrl+Alt+P that switches it on looks like.
        KeyPress NameByKeyboardLayout(KeyPress keyPress)
        {
            var eventArgs = keyPress.InterceptKeyEventArgs;

            // Ctrl or Alt alone, or the Windows key, make a shortcut; Ctrl and Alt together may be AltGr
            if (keyPress.WinkeyPressed || eventArgs.ControlPressed != eventArgs.AltPressed)
                return keyPress;

            var text = GetLayoutText(eventArgs.Key, eventArgs.ShiftPressed, eventArgs.ControlPressed);
            if (text == null)
                return keyPress;

            if (text.Length == 0)
                return null;

            return new KeyPress(
                keyPress.Process,
                new InterceptKeyEventArgs(eventArgs.Key, eventArgs.KeyDirection, false, false, eventArgs.ShiftPressed),
                false,
                new[] { text });
        }

        string GetLayoutText(Keys key, bool shift, bool controlAlt)
        {
            if (keyboardLayoutTranslator == null || (settings != null && !settings.UseKeyboardLayoutNames))
                return null;

            return keyboardLayoutTranslator.GetText(key, shift, controlAlt);
        }

        // Shortcuts are written with the name of the letter or digit ("Ctrl+C", "Ctrl+1") whatever the layout, the
        // punctuation keys are named as they are on the keyboard.
        static bool IsNamedLikeItsLatinLetter(Keys key)
        {
            return (key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9);
        }

        string GetShortcutKeyName(Keys key)
        {
            // a key that types nothing on its own (a dead key) has no name on the layout: the one it always had
            var layoutText = IsNamedLikeItsLatinLetter(key) ? null : GetLayoutText(key, false, false);
            return string.IsNullOrEmpty(layoutText) ? key.Sanitise() : layoutText;
        }
    }
}
