using System;
using System.ComponentModel;
using System.Windows;
using Carnac.Logic.Enums;

namespace Carnac.Logic.Models
{
    public class PopupSettings : NotifyPropertyChanged
    {
        public PopupSettings()
        {
            // not the type's default of 0, which is outside the supported range
            RepeatedKeyThreshold = RepeatedKeyPolicy.DefaultTypedCharacterThreshold;
        }

        [DefaultValue(350)]
        public int ItemMaxWidth { get; set; }

        [DefaultValue(0.5)]
        public double ItemOpacity { get; set; }

        [DefaultValue(5)]
        public double ItemFadeDelay { get; set; }

        [DefaultValue("Black")]
        public string ItemBackgroundColor { get; set; }

        [DefaultValue("White")]
        public string FontColor { get; set; }

        [DefaultValue(40)]
        public int FontSize { get; set; }

        /// <summary>
        /// Family name of the popup font. Empty (the default) uses the system font of the user interface,
        /// which is also used when the named font is not installed.
        /// </summary>
        [DefaultValue("")]
        public string FontFamily { get; set; }

        // Upper limits of the sliders of the Appearance tab. Typed values beyond them are limited when the popup is drawn.
        public const double MaxItemCornerRadius = 50;
        public const double MaxItemPadding = 30;

        [DefaultValue(15D)]
        public double ItemCornerRadius { get; set; }

        [DefaultValue(3D)]
        public double ItemPadding { get; set; }

        public CornerRadius ItemBorderCornerRadius
        {
            get { return new CornerRadius(Limit(ItemCornerRadius, MaxItemCornerRadius)); }
        }

        public Thickness ItemBorderPadding
        {
            get { return new Thickness(Limit(ItemPadding, MaxItemPadding)); }
        }

        // A negative or NaN value would throw when it is applied to the border.
        static double Limit(double value, double max)
        {
            if (double.IsNaN(value) || value < 0)
                return 0;

            return Math.Min(value, max);
        }

        /// <summary>The number of the screen; the screen is found by <see cref="ScreenDeviceName"/> first and this is only used when that is empty.</summary>
        public int Screen { get; set; }

        /// <summary>
        /// The device name of the screen the popups appear on, such as <c>\\.\DISPLAY2</c>. Unlike <see cref="Screen"/> it
        /// survives monitors being plugged and unplugged. When it is empty and the number matches no screen, or when no
        /// screen has this name (the monitor is unplugged), the popups appear on the primary screen.
        /// </summary>
        public string ScreenDeviceName { get; set; }

        [NotifyProperty(AlsoNotifyFor = new[] { "ScaleTransform", "Alignment" })]
        public NotificationPlacement Placement { get; set; }

        [DefaultValue(false)]
        public bool AutoUpdate { get; set; }

        /// <summary>
        /// Language of the user interface: one of <see cref="UiLanguages.Codes"/>, or empty (the default) to follow
        /// the display language of Windows.
        /// </summary>
        [DefaultValue("")]
        public string Language { get; set; }

        /// <summary>
        /// Lists the overlay window in window lists so that capture tools such as OBS can select it
        /// (a tool window, which is what the overlay is otherwise, is not listed).
        /// </summary>
        [DefaultValue(false)]
        public bool CaptureFriendlyWindow { get; set; }

        [NotifyProperty(AlsoNotifyFor = new[] { "Margins" })]
        public int TopOffset { get; set; }

        [NotifyProperty(AlsoNotifyFor = new[] { "Margins" })]
        public int BottomOffset { get; set; }

        [NotifyProperty(AlsoNotifyFor = new[] { "Margins" })]
        public int LeftOffset { get; set; }

        [NotifyProperty(AlsoNotifyFor = new[] { "Margins" })]
        public int RightOffset { get; set; }

        [DefaultValue("")]
        public string ProcessFilterExpression { get; set;  }

        // A new PopupSettings has to show every key, so the field starts at All instead of relying on [DefaultValue]
        // (which only the settings provider applies).
        KeyCategory visibleKeyCategories = KeyCategory.All;

        /// <summary>
        /// The key groups that are shown. Keys pressed with Ctrl, Alt or the Windows key, and shortcuts recognised from
        /// the keymaps, are always shown; this only decides about the other key presses. It is applied in addition to
        /// <see cref="DetectShortcutsOnly"/> and <see cref="ShowOnlyModifiers"/>, which can only hide more.
        /// </summary>
        [DefaultValue(KeyCategory.All)]
        public KeyCategory VisibleKeyCategories
        {
            get { return visibleKeyCategories; }
            set { visibleKeyCategories = value; }
        }

        /// <summary>
        /// Keys that are never shown, separated by commas or line breaks, in the keymap key syntax: "W,A,S,D" or
        /// "Ctrl+Alt+Delete". Modifiers must match exactly.
        /// </summary>
        [DefaultValue("")]
        public string IgnoredKeys { get; set; }

        [DefaultValue(false)]
        public bool ShowMouseClicks { get; set; }

        [DefaultValue(ClickHighlightSettings.DefaultLeftColor)]
        public string LeftClickColor { get; set; }

        [DefaultValue(ClickHighlightSettings.DefaultMiddleColor)]
        public string MiddleClickColor { get; set; }

        [DefaultValue(ClickHighlightSettings.DefaultRightColor)]
        public string RightClickColor { get; set; }

        [DefaultValue(ClickHighlightSettings.DefaultDiameter)]
        public int ClickCircleSize { get; set; }

        [DefaultValue(ClickHighlightSettings.DefaultDurationMilliseconds)]
        public int ClickCircleDuration { get; set; }

        public double ScaleTransform
        {
            get { return Placement == NotificationPlacement.TopLeft || Placement == NotificationPlacement.TopRight ? 1 : -1; }
        }

        public string Alignment
        {
            get { return Placement == NotificationPlacement.TopLeft || Placement == NotificationPlacement.BottomLeft ? "Left" : "Right"; }
        }

        public Thickness Margins
        {
            get { return new Thickness(LeftOffset, TopOffset, RightOffset, BottomOffset); }
        }

        /// <summary>Name the keys as they are on the keyboard layout that is in use instead of as on a US keyboard.</summary>
        [DefaultValue(true)]
        public bool UseKeyboardLayoutNames { get; set; }

        public string SortDescription
        {
            get { return Placement == NotificationPlacement.TopLeft || Placement == NotificationPlacement.TopRight ? "Ascending" : "Descending"; }
        }

        public bool DetectShortcutsOnly { get; set; }
        public bool ShowApplicationIcon { get; set; }
        public bool SettingsConfigured { get; set; }
        public bool ShowOnlyModifiers { get; set; }
        public bool ShowSpaceAsUnicode { get; set; }

        /// <summary>
        /// Shows the name of a recognised keymap shortcut (for example "Open the Find Bar") next to the keys.
        /// Read at display time, so switching it takes effect immediately.
        /// </summary>
        [DefaultValue(true)]
        public bool ShowShortcutDescription { get; set; }

        [DefaultValue(RepeatedKeyGrouping.Threshold)]
        public RepeatedKeyGrouping RepeatedKeyGrouping { get; set; }

        [DefaultValue(RepeatedKeyPolicy.DefaultTypedCharacterThreshold)]
        public int RepeatedKeyThreshold { get; set; }

        [DefaultValue(false)]
        public bool ShowModifierKeyPresses { get; set; }
    }
}
