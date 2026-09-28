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

        // Modifier keys that are down right now, to tell a key that is held (auto-repeat) from a new press.
        readonly HashSet<Keys> heldModifierKeys = new HashSet<Keys>();
        readonly object heldModifierKeysSync = new object();

        private bool winKeyPressed;

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
                // When desktop is locked we will not get the keyup, because we track the windows key
                // specially we need to set it to not being pressed anymore
                var sessionSwitchStreamSubscription = desktopLockEventService.GetSessionSwitchStream()
                .Subscribe(ss =>
                {
                    if (ss.Reason == SessionSwitchReason.SessionLock)
                    {
                        winKeyPressed = false;
                        lock (heldModifierKeysSync)
                            heldModifierKeys.Clear();
                    }
                }, observer.OnError);

                var keyStreamSubsription = interceptKeysSource.GetKeyStream()
                    .Select(DetectWindowsKey)
                    .Where(ShouldShowKeyPress)
                    .Select(ToCarnacKeyPress)
                    .Where(keypress => keypress != null)
                    .Where(k => !passwordModeService.CheckPasswordMode(k.InterceptKeyEventArgs))
                    .Subscribe(observer);

                return new CompositeDisposable(sessionSwitchStreamSubscription, keyStreamSubsription);
            });
        }

        InterceptKeyEventArgs DetectWindowsKey(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            if (interceptKeyEventArgs.Key == Keys.LWin || interceptKeyEventArgs.Key == Keys.RWin)
            {
                if (interceptKeyEventArgs.KeyDirection == KeyDirection.Up)
                    winKeyPressed = false;
                else if (interceptKeyEventArgs.KeyDirection == KeyDirection.Down)
                    winKeyPressed = true;
            }

            return interceptKeyEventArgs;
        }

        static bool IsModifierKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            return interceptKeyEventArgs.IsModifier();
        }

        bool ShouldShowKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            if (!IsModifierKeyPress(interceptKeyEventArgs))
                return interceptKeyEventArgs.KeyDirection == KeyDirection.Down;

            // Always keep track of the modifiers that are down, also while they are not shown
            var isNewPress = TrackModifierKey(interceptKeyEventArgs);
            if (!isNewPress || settings == null || !settings.ShowModifierKeyPresses)
                return false;

            // Shift on its own is what capital letters are typed with, it is only shown as part of Ctrl/Alt/Win + Shift
            return GetHeldModifierNames().Any(name => name != "Shift");
        }

        // True when the key went down just now, false for auto-repeat of a key that is held and for releases.
        bool TrackModifierKey(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            lock (heldModifierKeysSync)
            {
                if (interceptKeyEventArgs.KeyDirection == KeyDirection.Down)
                    return heldModifierKeys.Add(interceptKeyEventArgs.Key);

                if (interceptKeyEventArgs.KeyDirection == KeyDirection.Up)
                    heldModifierKeys.Remove(interceptKeyEventArgs.Key);

                return false;
            }
        }

        // The modifiers that are down, named and ordered like the ones in front of a key: Ctrl, Alt, Win, Shift.
        string[] GetHeldModifierNames()
        {
            bool control, alt, win, shift;
            lock (heldModifierKeysSync)
            {
                control = heldModifierKeys.Contains(Keys.LControlKey) || heldModifierKeys.Contains(Keys.RControlKey);
                alt = heldModifierKeys.Contains(Keys.LMenu) || heldModifierKeys.Contains(Keys.RMenu);
                win = heldModifierKeys.Contains(Keys.LWin) || heldModifierKeys.Contains(Keys.RWin);
                shift = heldModifierKeys.Contains(Keys.LShiftKey) || heldModifierKeys.Contains(Keys.RShiftKey);
            }

            var names = new List<string>();
            if (control) names.Add("Ctrl");
            if (alt) names.Add("Alt");
            if (win) names.Add("Win");
            if (shift) names.Add("Shift");
            return names.ToArray();
        }

        KeyPress ToCarnacKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
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

            var isWinKeyPressed = winKeyPressed;
            string[] inputs;
            if (IsModifierKeyPress(interceptKeyEventArgs))
            {
                // A modifier on its own is shown with the ones that are down with it ("Ctrl + Shift"). What the
                // hook reports as modifier state while the key is being pressed is not reliable, so use the held keys.
                inputs = GetHeldModifierNames();
                interceptKeyEventArgs = new InterceptKeyEventArgs(interceptKeyEventArgs.Key, interceptKeyEventArgs.KeyDirection,
                    altPressed: inputs.Contains("Alt"), controlPressed: inputs.Contains("Ctrl"), shiftPressed: inputs.Contains("Shift"));
                isWinKeyPressed = inputs.Contains("Win");
            }
            else
            {
                var isLetter = interceptKeyEventArgs.IsLetter();
                inputs = ToInputs(isLetter, winKeyPressed, interceptKeyEventArgs).ToArray();
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
