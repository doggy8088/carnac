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
using SettingsProviderNet;

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

            SaveCommand = new DelegateCommand(SaveSettings);
            ResetToDefaultsCommand = new DelegateCommand(() => settingsProvider.ResetToDefaults<PopupSettings>());
            VisitCommand = new DelegateCommand(Visit);
        }

        void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "LeftClickColor" || e.PropertyName == "MiddleClickColor" || e.PropertyName == "RightClickColor")
                OnPropertyChanged(e.PropertyName);
        }

        public ICommand VisitCommand { get; private set; }

        public ICommand ResetToDefaultsCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }

        public ObservableCollection<AvailableColor> AvailableColors { get; private set; }

        public IEnumerable<KeyValuePair<RepeatedKeyGrouping, string>> RepeatedKeyGroupingOptions { get; private set; }

        public IList<FontFamilyOption> AvailableFontFamilies { get; private set; }

        public IList<LanguageOption> AvailableLanguages { get; private set; }

        public ObservableCollection<DetailedScreen> Screens { get; set; }

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

        public AvailableColor FontColor { get; set; }

        public AvailableColor ItemBackgroundColor { get; set; }

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
            settingsProvider.SaveSettings(Settings);
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
        }
    }
}
