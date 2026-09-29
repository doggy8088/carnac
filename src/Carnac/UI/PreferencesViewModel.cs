using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using SettingsProviderNet;
using Keys = System.Windows.Forms.Keys;

namespace Carnac.UI
{
    public class PreferencesViewModel : NotifyPropertyChanged
    {
        readonly ISettingsProvider settingsProvider;
        
        public PreferencesViewModel(ISettingsProvider settingsProvider, IScreenManager screenManager)
        {
            this.settingsProvider = settingsProvider;
            
            Screens = new ObservableCollection<DetailedScreen>(screenManager.GetScreens());

            Settings = settingsProvider.GetSettings<PopupSettings>();

            PlaceScreen();

            AvailableColors = new ObservableCollection<AvailableColor>();
            var properties = typeof(Colors).GetProperties(BindingFlags.Static | BindingFlags.Public);
            foreach (var prop in properties)
            {
                var name = prop.Name;
                var value = (Color)prop.GetValue(null, null);

                var availableColor = new AvailableColor(name, value);
                if (Settings.FontColor == name)
                    FontColor = availableColor;
                if (Settings.ItemBackgroundColor == name)
                    ItemBackgroundColor = availableColor;

                AvailableColors.Add(availableColor);
            }

            LoadHotkeys();

            SaveCommand = new DelegateCommand(SaveSettings);
            ResetToDefaultsCommand = new DelegateCommand(ResetToDefaults);
            VisitCommand = new DelegateCommand(Visit);
            ClearSilentModeHotkeyCommand = new DelegateCommand(() => SetHotkey(h => SilentModeHotkey = h, string.Empty));
            ClearPauseHotkeyCommand = new DelegateCommand(() => SetHotkey(h => PauseHotkey = h, string.Empty));
        }

        public ICommand VisitCommand { get; private set; }

        public ICommand ResetToDefaultsCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }

        public ICommand ClearSilentModeHotkeyCommand { get; private set; }

        public ICommand ClearPauseHotkeyCommand { get; private set; }

        /// <summary>The silent-mode hotkey as shown in the preferences, for example "Ctrl+Alt+P"; empty means none. Applied on Save.</summary>
        public string SilentModeHotkey { get; private set; }

        /// <summary>The pause hotkey as shown in the preferences; empty means none. Applied on Save.</summary>
        public string PauseHotkey { get; private set; }

        /// <summary>Why the last captured key combination was refused; empty while there is nothing to complain about.</summary>
        public string HotkeyMessage { get; private set; }

        public ObservableCollection<AvailableColor> AvailableColors { get; private set; }

        public ObservableCollection<DetailedScreen> Screens { get; set; }

        public DetailedScreen SelectedScreen { get; set; }

        public PopupSettings Settings { get; set; }

        public string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }

        readonly List<string> authors = new List<string>
                                                    {
                                                         "Brendan Forster",
                                                         "Alex Friedman",
                                                         "Jon Galloway",
                                                         "Jake Ginnivan",
                                                         "Paul Jenkins",
                                                         "Dmitry Pursanov",
                                                         "Chris Sainty",
                                                         "Andrew Tobin",
                                                         "Henrik Andersson"
                                                     };
        readonly List<string> components = new List<string>
                                                       {
                                                         "MahApps.Metro",
                                                         "Fody",
                                                         "NSubstitute",
                                                         "Reactive Extensions"
                                                     };
        public string Authors
        {
            get { return string.Join(", ", authors); }
        }

        public string Components
        {
            get { return string.Join(", ", components); }
        }

        public AvailableColor FontColor { get; set; }

        public AvailableColor ItemBackgroundColor { get; set; }

        void Visit()
        {
            try
            {
                Process.Start("http://code52.org/carnac/");
            }
            catch
            {
                //I forget what exceptions can be raised if the browser is crashed?
            }
        }

        void SaveSettings()
        {
            if (Screens.Count < 1)
                return;

            if (SelectedScreen == null)
                SelectedScreen = Screens.First();

            Settings.Screen = SelectedScreen.Index;

            if (SelectedScreen.NotificationPlacementTopLeft)
                Settings.Placement = NotificationPlacement.TopLeft;
            else if (SelectedScreen.NotificationPlacementBottomLeft)
                Settings.Placement = NotificationPlacement.BottomLeft;
            else if (SelectedScreen.NotificationPlacementTopRight)
                Settings.Placement = NotificationPlacement.TopRight;
            else if (SelectedScreen.NotificationPlacementBottomRight)
                Settings.Placement = NotificationPlacement.BottomRight;
            else
                Settings.Placement = NotificationPlacement.BottomLeft;

            PlaceScreen();

            Settings.SettingsConfigured = true;
            Settings.FontColor = FontColor.Name;
            Settings.ItemBackgroundColor = ItemBackgroundColor.Name;
            // The hotkeys are read from the settings on every key press, so they are in effect right away.
            Settings.SilentModeHotkey = SilentModeHotkey;
            Settings.PauseHotkey = PauseHotkey;
            settingsProvider.SaveSettings(Settings);
        }

        void ResetToDefaults()
        {
            settingsProvider.ResetToDefaults<PopupSettings>();
            LoadHotkeys();
        }

        /// <summary>Called by the hotkey box for a key combination the user pressed.</summary>
        public void CaptureSilentModeHotkey(Keys key, bool control, bool alt, bool shift, bool windows)
        {
            Capture(key, control, alt, shift, windows, h => SilentModeHotkey = h, PauseHotkey);
        }

        /// <summary>Called by the hotkey box for a key combination the user pressed.</summary>
        public void CapturePauseHotkey(Keys key, bool control, bool alt, bool shift, bool windows)
        {
            Capture(key, control, alt, shift, windows, h => PauseHotkey = h, SilentModeHotkey);
        }

        void Capture(Keys key, bool control, bool alt, bool shift, bool windows, Action<string> assign, string otherHotkey)
        {
            KeyPressDefinition hotkey;
            string error;
            if (!HotkeyParser.TryCreate(key, control, alt, shift, windows, out hotkey, out error))
            {
                HotkeyMessage = error;
                return;
            }

            var text = HotkeyParser.Format(hotkey);
            if (string.Equals(text, otherHotkey, StringComparison.Ordinal))
            {
                HotkeyMessage = string.Format("{0} is already used by the other hotkey.", text);
                return;
            }

            SetHotkey(assign, text);
        }

        void SetHotkey(Action<string> assign, string text)
        {
            HotkeyMessage = string.Empty;
            assign(text);
        }

        void LoadHotkeys()
        {
            SilentModeHotkey = Describe(ConfiguredHotkeys.ResolveSilentMode(Settings.SilentModeHotkey));
            PauseHotkey = Describe(ConfiguredHotkeys.ResolvePause(Settings.PauseHotkey));
            HotkeyMessage = string.Empty;
        }

        static string Describe(KeyPressDefinition hotkey)
        {
            return hotkey == null ? string.Empty : HotkeyParser.Format(hotkey);
        }

        void PlaceScreen()
        {
            if (Screens == null) 
                return;

            SelectedScreen = Screens.FirstOrDefault(s => s.Index == Settings.Screen);

            if (SelectedScreen == null) 
                return;

            switch (Settings.Placement)
            {
                case NotificationPlacement.TopLeft:
                    SelectedScreen.NotificationPlacementTopLeft = true;
                    break;
                case NotificationPlacement.BottomLeft:
                    SelectedScreen.NotificationPlacementBottomLeft = true;
                    break;
                case NotificationPlacement.TopRight:
                    SelectedScreen.NotificationPlacementTopRight = true;
                    break;
                case NotificationPlacement.BottomRight:
                    SelectedScreen.NotificationPlacementBottomRight = true;
                    break;
                default:
                    SelectedScreen.NotificationPlacementBottomLeft = true;
                    break;
            }

            Settings.Left = SelectedScreen.Left;
            Settings.Top = SelectedScreen.Top;
        }
    }
}
