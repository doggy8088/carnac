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
using Carnac.Logic.Overlay;
using SettingsProviderNet;

namespace Carnac.UI
{
    public class PreferencesViewModel : NotifyPropertyChanged
    {
        readonly ISettingsProvider settingsProvider;
        readonly IPreviewService previewService;
        IDisposable preview;
        AvailableColor fontColor;
        AvailableColor itemBackgroundColor;

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

            SaveCommand = new DelegateCommand(SaveSettings);
            ResetToDefaultsCommand = new DelegateCommand(() => settingsProvider.ResetToDefaults<PopupSettings>());
            VisitCommand = new DelegateCommand(Visit);
        }

        public ICommand VisitCommand { get; private set; }

        public ICommand ResetToDefaultsCommand { get; private set; }

        public ICommand SaveCommand { get; private set; }

        public ObservableCollection<AvailableColor> AvailableColors { get; private set; }

        public ObservableCollection<DetailedScreen> Screens { get; set; }

        /// <summary>The size of the drawing of all screens in their real arrangement (see <see cref="MonitorLayout"/>).</summary>
        public double ScreenLayoutWidth { get; private set; }

        public double ScreenLayoutHeight { get; private set; }

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
            settingsProvider.SaveSettings(Settings);
        }

        void PlaceScreen()
        {
            if (Screens == null) 
                return;

            SelectedScreen = OverlayPlacement.SelectScreen(Screens, Settings.ScreenDeviceName, Settings.Screen);

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
