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
        const int MaxLoggedIconFailures = 100;

        readonly IInterceptKeys interceptKeysSource;
        readonly IPasswordModeService passwordModeService;
        readonly IDesktopLockEventService desktopLockEventService;
        readonly PopupSettings settings;
        readonly IProcessProvider processProvider;
        readonly ILogger logger;
        readonly object filterSync = new object();
        readonly HashSet<string> iconFailuresLogged = new HashSet<string>();
        string currentFilter = null;
        Regex currentFilterRegex;

        private readonly IList<Keys> modifierKeys =
            new List<Keys>
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

        private bool winKeyPressed;

        public KeyProvider(IInterceptKeys interceptKeysSource, IPasswordModeService passwordModeService, IDesktopLockEventService desktopLockEventService, ISettingsProvider settingsProvider)
            : this(interceptKeysSource, passwordModeService, desktopLockEventService, settingsProvider, new SystemProcessProvider(), NullLogger.Instance)
        {
        }

        public KeyProvider(IInterceptKeys interceptKeysSource, IPasswordModeService passwordModeService, IDesktopLockEventService desktopLockEventService, ISettingsProvider settingsProvider, IProcessProvider processProvider, ILogger logger)
        {
            if (settingsProvider == null)
            {
                throw new ArgumentNullException("settingsProvider");
            }

            if (processProvider == null)
            {
                throw new ArgumentNullException("processProvider");
            }

            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            this.processProvider = processProvider;
            this.logger = logger;

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
                        catch (ArgumentException ex)
                        {
                            // not a valid regular expression: keys of all applications are shown; logged once per change of the setting
                            logger.Warn(string.Format("The process filter '{0}' is not a valid regular expression and is ignored", currentFilter), ex);
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
                        winKeyPressed = false;
                }, observer.OnError);

                var keyStreamSubsription = interceptKeysSource.GetKeyStream()
                    .Select(DetectWindowsKey)
                    .Where(k => !IsModifierKeyPress(k) && k.KeyDirection == KeyDirection.Down)
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

        bool IsModifierKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            return modifierKeys.Contains(interceptKeyEventArgs.Key);
        }

        KeyPress ToCarnacKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            try
            {
                return CreateKeyPress(interceptKeyEventArgs);
            }
            catch (Exception ex)
            {
                // The foreground process can exit while we look at it, the process filter can time out, ...
                // An exception in a Select ends the whole Rx key stream (OnError) and Carnac would silently stop
                // showing keys, so it is logged and only this key press is dropped.
                logger.Error("A key press could not be processed and was skipped", ex);
                return null;
            }
        }

        KeyPress CreateKeyPress(InterceptKeyEventArgs interceptKeyEventArgs)
        {
            var process = processProvider.GetAssociatedProcess();
            if (process == null)
            {
                return null;
            }

            // reading the name throws when the process has exited in the meantime
            var processName = process.ProcessName;

            // see if this process is one being filtered for
            Regex filterRegex;
            if (ShouldFilterProcess(out filterRegex) && !filterRegex.IsMatch(processName))
            {
                return null;
            }

            var isLetter = interceptKeyEventArgs.IsLetter();
            var inputs = ToInputs(isLetter, winKeyPressed, interceptKeyEventArgs).ToArray();
            return new KeyPress(new ProcessInfo(processName, GetProcessIcon(process, processName)), interceptKeyEventArgs, winKeyPressed, inputs);
        }

        ImageSource GetProcessIcon(Process process, string processName)
        {
            try
            {
                return IconUtilities.GetProcessIconAsImageSource(process.MainModule.FileName);
            }
            catch (Exception ex)
            {
                // Typically "access denied": the module list of an application that runs as administrator cannot be read.
                // The key is still shown, just without an icon; log it once per application, not once per key press.
                LogIconFailureOnce(processName, ex);
                return null;
            }
        }

        void LogIconFailureOnce(string processName, Exception exception)
        {
            lock (iconFailuresLogged)
            {
                if (iconFailuresLogged.Count >= MaxLoggedIconFailures || !iconFailuresLogged.Add(processName))
                {
                    return;
                }
            }

            logger.Warn(string.Format("No application icon for '{0}' ({1}: {2})", processName, exception.GetType().Name, exception.Message));
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
