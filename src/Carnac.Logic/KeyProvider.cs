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
        readonly object filterSync = new object();
        string currentFilter = null;
        Regex currentFilterRegex;

        // How to tell whether a key really is down, for the key events that come from the keyboard hook.
        internal Func<Keys, bool> IsKeyDown = HeldModifierKeys.IsKeyPhysicallyDown;

        public KeyProvider(IInterceptKeys interceptKeysSource, IPasswordModeService passwordModeService, IDesktopLockEventService desktopLockEventService, ISettingsProvider settingsProvider)
        {
            if (settingsProvider == null)
            {
                throw new ArgumentNullException("settingsProvider");
            }

            this.interceptKeysSource = interceptKeysSource;
            this.passwordModeService = passwordModeService;
            this.desktopLockEventService = desktopLockEventService;

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
                    .Subscribe(observer);

                return new CompositeDisposable(sessionSwitchStreamSubscription, keyStreamSubsription);
            });
        }

        // Keeps track of the modifier keys that are down, and tells which key events are shown
        bool ShouldShowKeyPress(InterceptKeyEventArgs interceptKeyEventArgs, HeldModifierKeys heldModifierKeys)
        {
            // The key ups of modifiers are not always seen, so ask the keyboard what is really down. Only for events
            // of the keyboard hook: the state of the keyboard means nothing for events that are made up.
            if (interceptKeyEventArgs.IsFromKeyboardHook)
                heldModifierKeys.ForgetReleased(IsKeyDown, interceptKeyEventArgs.Key);

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

        static IEnumerable<string> ToInputs(bool isLetter, bool isWinKeyPressed, InterceptKeyEventArgs interceptKeyEventArgs)
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

            if (controlPressed || altPressed)
            {
                //Treat as a shortcut, don't be too smart
                if (shiftPressed)
                    yield return "Shift";

                yield return interceptKeyEventArgs.Key.Sanitise();
            }
            else
            {
                string input;
                var shiftModifiesInput = interceptKeyEventArgs.Key.SanitiseShift(out input);

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
    }
}
