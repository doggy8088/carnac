using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Carnac.Logic;
using Carnac.Logic.Enums;
using Carnac.Logic.Models;
using Carnac.Logic.Native;
using Carnac.Logic.Overlay;
using SettingsProviderNet;
using Keys = System.Windows.Forms.Keys;

namespace Carnac.UI
{
    public class PreferencesViewModel : NotifyPropertyChanged
    {
        readonly ISettingsProvider settingsProvider;
        readonly IPreviewService previewService;
        IDisposable preview;
        AvailableColor fontColor;
        AvailableColor itemBackgroundColor;

        public PreferencesViewModel(ISettingsProvider settingsProvider, IScreenManager screenManager)
            : this(settingsProvider, screenManager, new PreviewService(new ObservableCollection<Message>(), PreviewService.CreateSampleProcess()))
        {
        }

        public PreferencesViewModel(ISettingsProvider settingsProvider, IScreenManager screenManager, IPreviewService previewService)
        {
            if (previewService == null) throw new ArgumentNullException("previewService");

            this.settingsProvider = settingsProvider;
            this.previewService = previewService;
            
            Screens = new ObservableCollection<DetailedScreen>(screenManager.GetScreens());
            var screenLayout = MonitorLayout.Create(Screens);
            ScreenLayoutWidth = screenLayout.Width;
            ScreenLayoutHeight = screenLayout.Height;

            Settings = settingsProvider.GetSettings<PopupSettings>();

            KeyCategoryOptions = CreateKeyCategoryOptions(Settings);

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

            RepeatedKeyGroupingOptions = new[]
            {
                new KeyValuePair<RepeatedKeyGrouping, string>(RepeatedKeyGrouping.Threshold, Properties.Resources.Preferences_RepeatedKeys_Group),
                new KeyValuePair<RepeatedKeyGrouping, string>(RepeatedKeyGrouping.Never, Properties.Resources.Preferences_RepeatedKeys_Never)
            };

            // The mouse colors are the ones in the settings, also after they were reset to their defaults. Weak, because
            // the settings live as long as the application and this view model as long as the Preferences window.
            WeakEventManager<PopupSettings, PropertyChangedEventArgs>.AddHandler(Settings, "PropertyChanged", OnSettingsPropertyChanged);

            AvailableFontFamilies = FontFamilyOption.CreateList(
                Fonts.SystemFontFamilies, SystemFonts.MessageFontFamily, Properties.Resources.Preferences_SystemDefaultFont, CultureInfo.CurrentUICulture);

            AvailableLanguages = LanguageOption.CreateList();

            LoadHotkeys();

            SaveCommand = new DelegateCommand(SaveSettings);
            ResetToDefaultsCommand = new DelegateCommand(ResetToDefaults);
            VisitCommand = new DelegateCommand(Visit);
            ClearSilentModeHotkeyCommand = new DelegateCommand(() => SetHotkey(h => SilentModeHotkey = h, string.Empty));
            ClearPauseHotkeyCommand = new DelegateCommand(() => SetHotkey(h => PauseHotkey = h, string.Empty));
        }

        void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LeftClickColor" || e.PropertyName == "MiddleClickColor" || e.PropertyName == "RightClickColor")
                OnPropertyChanged(e.PropertyName);
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

        public IEnumerable<KeyValuePair<RepeatedKeyGrouping, string>> RepeatedKeyGroupingOptions { get; private set; }

        public IList<FontFamilyOption> AvailableFontFamilies { get; private set; }

        public IList<LanguageOption> AvailableLanguages { get; private set; }

        public ObservableCollection<DetailedScreen> Screens { get; set; }

        /// <summary>The size of the drawing of all screens in their real arrangement (see <see cref="MonitorLayout"/>).</summary>
        public double ScreenLayoutWidth { get; private set; }

        public double ScreenLayoutHeight { get; private set; }

        public DetailedScreen SelectedScreen { get; set; }

        public PopupSettings Settings { get; set; }

        /// <summary>The check boxes of the "Keys to show" group, one per key category.</summary>
        public IList<KeyCategoryOption> KeyCategoryOptions { get; private set; }

        static IList<KeyCategoryOption> CreateKeyCategoryOptions(PopupSettings settings)
        {
            return new List<KeyCategoryOption>
            {
                new KeyCategoryOption(settings, KeyCategory.Letters, Properties.Resources.Preferences_KeyCategory_Letters, Properties.Resources.Preferences_KeyCategory_LettersDescription),
                new KeyCategoryOption(settings, KeyCategory.Digits, Properties.Resources.Preferences_KeyCategory_Digits, Properties.Resources.Preferences_KeyCategory_DigitsDescription),
                new KeyCategoryOption(settings, KeyCategory.Punctuation, Properties.Resources.Preferences_KeyCategory_Punctuation, Properties.Resources.Preferences_KeyCategory_PunctuationDescription),
                new KeyCategoryOption(settings, KeyCategory.Whitespace, Properties.Resources.Preferences_KeyCategory_Whitespace, Properties.Resources.Preferences_KeyCategory_WhitespaceDescription),
                new KeyCategoryOption(settings, KeyCategory.Editing, Properties.Resources.Preferences_KeyCategory_Editing, Properties.Resources.Preferences_KeyCategory_EditingDescription),
                new KeyCategoryOption(settings, KeyCategory.Navigation, Properties.Resources.Preferences_KeyCategory_Navigation, Properties.Resources.Preferences_KeyCategory_NavigationDescription),
                new KeyCategoryOption(settings, KeyCategory.Function, Properties.Resources.Preferences_KeyCategory_Function, Properties.Resources.Preferences_KeyCategory_FunctionDescription),
                new KeyCategoryOption(settings, KeyCategory.Other, Properties.Resources.Preferences_KeyCategory_Other, Properties.Resources.Preferences_KeyCategory_OtherDescription)
            };
        }

        /// <summary>The keys can only be named after the keyboard layout on Windows 10 version 1607 or later.</summary>
        public bool IsKeyboardLayoutSupported
        {
            get { return KeyboardLayoutTranslator.IsSupported; }
        }

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
                                                         "Henrik Andersson",
                                                         "Will 保哥"
                                                     };
        readonly List<string> components = new List<string>
                                                       {
                                                         "MahApps.Metro",
                                                         "Fody",
                                                         "NSubstitute",
                                                         "Reactive Extensions",
                                                         "Squirrel.Windows"
                                                     };
        public string Authors
        {
            get { return string.Join(", ", authors); }
        }

        public string Components
        {
            get { return string.Join(", ", components); }
        }

        // The overlay shows the shared settings, so a colour is previewed as soon as it is picked
        // (like the sliders, which change the settings directly); Save persists them.
        public AvailableColor FontColor
        {
            get { return fontColor; }
            set
            {
                fontColor = value;
                if (value != null)
                    Settings.FontColor = value.Name;
            }
        }

        public AvailableColor ItemBackgroundColor
        {
            get { return itemBackgroundColor; }
            set
            {
                itemBackgroundColor = value;
                if (value != null)
                    Settings.ItemBackgroundColor = value.Name;
            }
        }

        /// <summary>Shows the sample popups on the overlay until <see cref="StopPreview"/>. Calling it again while showing does nothing.</summary>
        public void StartPreview()
        {
            if (preview == null)
                preview = previewService.Show();
        }

        /// <summary>Removes the sample popups again. Does nothing when they are not showing.</summary>
        public void StopPreview()
        {
            if (preview == null)
                return;

            preview.Dispose();
            preview = null;
        }

        // These change the settings as soon as they are picked, like the checkbox and the sliders of the Mouse tab do
        public AvailableColor LeftClickColor
        {
            get { return FindColor(Settings.LeftClickColor, ClickHighlightSettings.DefaultLeftColor); }
            set { if (value != null) Settings.LeftClickColor = value.Name; }
        }

        public AvailableColor MiddleClickColor
        {
            get { return FindColor(Settings.MiddleClickColor, ClickHighlightSettings.DefaultMiddleColor); }
            set { if (value != null) Settings.MiddleClickColor = value.Name; }
        }

        public AvailableColor RightClickColor
        {
            get { return FindColor(Settings.RightClickColor, ClickHighlightSettings.DefaultRightColor); }
            set { if (value != null) Settings.RightClickColor = value.Name; }
        }

        AvailableColor FindColor(string name, string fallback)
        {
            var colorName = ClickHighlightSettings.GetColorName(name, fallback);
            return AvailableColors.FirstOrDefault(color => color.Name == colorName);
        }

        void Visit()
        {
            try
            {
                Process.Start("https://carnac.gh.miniasp.com/");
            }
            catch
            {
                //I forget what exceptions can be raised if the browser is crashed?
            }
        }

        public void SelectScreenAndPlacement(DetailedScreen screen, NotificationPlacement placement)
        {
            if (screen == null)
                return;

            SelectedScreen = screen;
            Settings.ScreenDeviceName = screen.DeviceName;
            Settings.Screen = screen.Index;
            Settings.Placement = placement;
            PlaceScreen();
        }

        void SaveSettings()
        {
            if (Screens.Count < 1)
                return;

            if (SelectedScreen == null)
                SelectedScreen = Screens.First();

            // The device name first: it is what selects the screen, the number is only kept for older versions.
            Settings.ScreenDeviceName = SelectedScreen.DeviceName;
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

            SelectedScreen = OverlayPlacement.SelectScreen(Screens, Settings.ScreenDeviceName, Settings.Screen);

            if (SelectedScreen == null) 
                return;

            foreach (var screen in Screens)
            {
                if (!ReferenceEquals(screen, SelectedScreen))
                {
                    screen.NotificationPlacementTopLeft = false;
                    screen.NotificationPlacementBottomLeft = false;
                    screen.NotificationPlacementTopRight = false;
                    screen.NotificationPlacementBottomRight = false;
                }
            }

            SelectedScreen.NotificationPlacementTopLeft = Settings.Placement == NotificationPlacement.TopLeft;
            SelectedScreen.NotificationPlacementBottomLeft = Settings.Placement == NotificationPlacement.BottomLeft ||
                (Settings.Placement != NotificationPlacement.TopLeft &&
                 Settings.Placement != NotificationPlacement.TopRight &&
                 Settings.Placement != NotificationPlacement.BottomRight);
            SelectedScreen.NotificationPlacementTopRight = Settings.Placement == NotificationPlacement.TopRight;
            SelectedScreen.NotificationPlacementBottomRight = Settings.Placement == NotificationPlacement.BottomRight;
        }
    }
}
