// The texts of the user interface, one property per entry of Resources.resx (English).
//
// This class has the shape of the class that Visual Studio generates from a .resx file, but it is written by hand:
// the generated class always creates a plain ResourceManager, which can only read translations from satellite
// assemblies. EmbeddedResourceManager reads them from Carnac.exe instead (see CONTRIBUTING.md).
// When you add an entry to Resources.resx (and to every Resources_<culture>.resx), add its property here as well;
// LocalizationResourceFacts fails when the two do not match.

namespace Carnac.Properties {
    using System;


    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    public class Resources {

        private static global::System.Resources.ResourceManager resourceMan;

        private static global::System.Globalization.CultureInfo resourceCulture;

        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }

        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new EmbeddedResourceManager("Carnac.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        public static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Carnac uses.
        /// </summary>
        public static string About_CarnacUses {
            get {
                return ResourceManager.GetString("About_CarnacUses", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to a code52 project.
        /// </summary>
        public static string About_Code52Project {
            get {
                return ResourceManager.GetString("About_Code52Project", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to a project by.
        /// </summary>
        public static string About_ProjectBy {
            get {
                return ResourceManager.GetString("About_ProjectBy", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to version.
        /// </summary>
        public static string About_Version {
            get {
                return ResourceManager.GetString("About_Version", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Visit Carnac Website.
        /// </summary>
        public static string About_VisitWebsite {
            get {
                return ResourceManager.GetString("About_VisitWebsite", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Auto Update.
        /// </summary>
        public static string Preferences_AutoUpdate {
            get {
                return ResourceManager.GetString("Preferences_AutoUpdate", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to auto check updates from GitHub (need to restart this program).
        /// </summary>
        public static string Preferences_AutoUpdateDescription {
            get {
                return ResourceManager.GetString("Preferences_AutoUpdateDescription", resourceCulture);
            }
        }
        
        public static string Preferences_AutoUpdateToolTip {
            get {
                return ResourceManager.GetString("Preferences_AutoUpdateToolTip", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Background Color.
        /// </summary>
        public static string Preferences_BackgroundColor {
            get {
                return ResourceManager.GetString("Preferences_BackgroundColor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Bottom Offset.
        /// </summary>
        public static string Preferences_BottomOffset {
            get {
                return ResourceManager.GetString("Preferences_BottomOffset", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Capture for OBS.
        /// </summary>
        public static string Preferences_CaptureForObs {
            get {
                return ResourceManager.GetString("Preferences_CaptureForObs", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to let OBS / XSplit capture the overlay as a window.
        /// </summary>
        public static string Preferences_CaptureForObsDescription {
            get {
                return ResourceManager.GetString("Preferences_CaptureForObsDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Corner Radius.
        /// </summary>
        public static string Preferences_CornerRadius {
            get {
                return ResourceManager.GetString("Preferences_CornerRadius", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Font Color.
        /// </summary>
        public static string Preferences_FontColor {
            get {
                return ResourceManager.GetString("Preferences_FontColor", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Font Family.
        /// </summary>
        public static string Preferences_FontFamily {
            get {
                return ResourceManager.GetString("Preferences_FontFamily", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Font Size.
        /// </summary>
        public static string Preferences_FontSize {
            get {
                return ResourceManager.GetString("Preferences_FontSize", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Language.
        /// </summary>
        public static string Preferences_Language {
            get {
                return ResourceManager.GetString("Preferences_Language", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reopen Preferences to see this window in the new language..
        /// </summary>
        public static string Preferences_LanguageHint {
            get {
                return ResourceManager.GetString("Preferences_LanguageHint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to System default.
        /// </summary>
        public static string Preferences_LanguageSystemDefault {
            get {
                return ResourceManager.GetString("Preferences_LanguageSystemDefault", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Left Offset.
        /// </summary>
        public static string Preferences_LeftOffset {
            get {
                return ResourceManager.GetString("Preferences_LeftOffset", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only keys with Modifiers.
        /// </summary>
        public static string Preferences_OnlyModifiers {
            get {
                return ResourceManager.GetString("Preferences_OnlyModifiers", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only show keys that have Ctrl, Shift, Alt or Windows.
        /// </summary>
        public static string Preferences_OnlyModifiersDescription {
            get {
                return ResourceManager.GetString("Preferences_OnlyModifiersDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Popup Fade Delay (sec).
        /// </summary>
        public static string Preferences_PopupFadeDelay {
            get {
                return ResourceManager.GetString("Preferences_PopupFadeDelay", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Popup Opacity.
        /// </summary>
        public static string Preferences_PopupOpacity {
            get {
                return ResourceManager.GetString("Preferences_PopupOpacity", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Popup Padding.
        /// </summary>
        public static string Preferences_PopupPadding {
            get {
                return ResourceManager.GetString("Preferences_PopupPadding", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Popup Text Width.
        /// </summary>
        public static string Preferences_PopupTextWidth {
            get {
                return ResourceManager.GetString("Preferences_PopupTextWidth", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Process Filter.
        /// </summary>
        public static string Preferences_ProcessFilter {
            get {
                return ResourceManager.GetString("Preferences_ProcessFilter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only show keys from processes matching this regular expression.
        /// </summary>
        public static string Preferences_ProcessFilterDescription {
            get {
                return ResourceManager.GetString("Preferences_ProcessFilterDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Reset to Defaults.
        /// </summary>
        public static string Preferences_ResetToDefaults {
            get {
                return ResourceManager.GetString("Preferences_ResetToDefaults", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Right Offset.
        /// </summary>
        public static string Preferences_RightOffset {
            get {
                return ResourceManager.GetString("Preferences_RightOffset", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Save.
        /// </summary>
        public static string Preferences_Save {
            get {
                return ResourceManager.GetString("Preferences_Save", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Shortcuts Only.
        /// </summary>
        public static string Preferences_ShortcutsOnly {
            get {
                return ResourceManager.GetString("Preferences_ShortcutsOnly", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Only show keys that are listed in the Keymaps folder.
        /// </summary>
        public static string Preferences_ShortcutsOnlyDescription {
            get {
                return ResourceManager.GetString("Preferences_ShortcutsOnlyDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show Application Icon.
        /// </summary>
        public static string Preferences_ShowApplicationIcon {
            get {
                return ResourceManager.GetString("Preferences_ShowApplicationIcon", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Display the active application&apos;s icon.
        /// </summary>
        public static string Preferences_ShowApplicationIconDescription {
            get {
                return ResourceManager.GetString("Preferences_ShowApplicationIconDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show Space as ␣.
        /// </summary>
        public static string Preferences_ShowSpaceAsUnicode {
            get {
                return ResourceManager.GetString("Preferences_ShowSpaceAsUnicode", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Show Space as Unicode character &apos;␣&apos;.
        /// </summary>
        public static string Preferences_ShowSpaceAsUnicodeDescription {
            get {
                return ResourceManager.GetString("Preferences_ShowSpaceAsUnicodeDescription", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to System default.
        /// </summary>
        public static string Preferences_SystemDefaultFont {
            get {
                return ResourceManager.GetString("Preferences_SystemDefaultFont", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to About.
        /// </summary>
        public static string Preferences_TabAbout {
            get {
                return ResourceManager.GetString("Preferences_TabAbout", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Appearance.
        /// </summary>
        public static string Preferences_TabAppearance {
            get {
                return ResourceManager.GetString("Preferences_TabAppearance", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to General.
        /// </summary>
        public static string Preferences_TabGeneral {
            get {
                return ResourceManager.GetString("Preferences_TabGeneral", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Top Offset.
        /// </summary>
        public static string Preferences_TopOffset {
            get {
                return ResourceManager.GetString("Preferences_TopOffset", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Exit.
        /// </summary>
        public static string ShellView_Exit {
            get {
                return ResourceManager.GetString("ShellView_Exit", resourceCulture);
            }
        }

        public static string Preferences_OnlyModifiersToolTip {
            get {
                return ResourceManager.GetString("Preferences_OnlyModifiersToolTip", resourceCulture);
            }
        }

        public static string Preferences_ModifierKeys {
            get {
                return ResourceManager.GetString("Preferences_ModifierKeys", resourceCulture);
            }
        }

        public static string Preferences_ModifierKeysDescription {
            get {
                return ResourceManager.GetString("Preferences_ModifierKeysDescription", resourceCulture);
            }
        }

        public static string Preferences_KeysToShow {
            get {
                return ResourceManager.GetString("Preferences_KeysToShow", resourceCulture);
            }
        }

        public static string Preferences_KeysToShowHint {
            get {
                return ResourceManager.GetString("Preferences_KeysToShowHint", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Letters {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Letters", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_LettersDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_LettersDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Digits {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Digits", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_DigitsDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_DigitsDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Punctuation {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Punctuation", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_PunctuationDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_PunctuationDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Whitespace {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Whitespace", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_WhitespaceDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_WhitespaceDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Editing {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Editing", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_EditingDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_EditingDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Navigation {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Navigation", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_NavigationDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_NavigationDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Function {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Function", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_FunctionDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_FunctionDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_Other {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_Other", resourceCulture);
            }
        }

        public static string Preferences_KeyCategory_OtherDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyCategory_OtherDescription", resourceCulture);
            }
        }

        public static string Preferences_IgnoredKeys {
            get {
                return ResourceManager.GetString("Preferences_IgnoredKeys", resourceCulture);
            }
        }

        public static string Preferences_IgnoredKeysHint {
            get {
                return ResourceManager.GetString("Preferences_IgnoredKeysHint", resourceCulture);
            }
        }

        public static string Preferences_KeyboardLayout {
            get {
                return ResourceManager.GetString("Preferences_KeyboardLayout", resourceCulture);
            }
        }

        public static string Preferences_KeyboardLayoutDescription {
            get {
                return ResourceManager.GetString("Preferences_KeyboardLayoutDescription", resourceCulture);
            }
        }

        public static string Preferences_KeyboardLayoutHint {
            get {
                return ResourceManager.GetString("Preferences_KeyboardLayoutHint", resourceCulture);
            }
        }

        public static string Preferences_RepeatedKeys {
            get {
                return ResourceManager.GetString("Preferences_RepeatedKeys", resourceCulture);
            }
        }

        public static string Preferences_RepeatedKeysToolTip {
            get {
                return ResourceManager.GetString("Preferences_RepeatedKeysToolTip", resourceCulture);
            }
        }

        public static string Preferences_RepeatedKeys_Group {
            get {
                return ResourceManager.GetString("Preferences_RepeatedKeys_Group", resourceCulture);
            }
        }

        public static string Preferences_RepeatedKeys_Never {
            get {
                return ResourceManager.GetString("Preferences_RepeatedKeys_Never", resourceCulture);
            }
        }

        public static string Preferences_GroupRepeatsFrom {
            get {
                return ResourceManager.GetString("Preferences_GroupRepeatsFrom", resourceCulture);
            }
        }

        public static string Preferences_GroupRepeatsFromToolTip {
            get {
                return ResourceManager.GetString("Preferences_GroupRepeatsFromToolTip", resourceCulture);
            }
        }

        public static string Preferences_ShortcutDescriptions {
            get {
                return ResourceManager.GetString("Preferences_ShortcutDescriptions", resourceCulture);
            }
        }

        public static string Preferences_ShortcutDescriptionsDescription {
            get {
                return ResourceManager.GetString("Preferences_ShortcutDescriptionsDescription", resourceCulture);
            }
        }

        public static string Preferences_ProcessFilterExampleContains {
            get {
                return ResourceManager.GetString("Preferences_ProcessFilterExampleContains", resourceCulture);
            }
        }

        public static string Preferences_ProcessFilterExampleExact {
            get {
                return ResourceManager.GetString("Preferences_ProcessFilterExampleExact", resourceCulture);
            }
        }

        public static string Preferences_ProcessFilterExampleExclude {
            get {
                return ResourceManager.GetString("Preferences_ProcessFilterExampleExclude", resourceCulture);
            }
        }

        public static string Preferences_TabMouse {
            get {
                return ResourceManager.GetString("Preferences_TabMouse", resourceCulture);
            }
        }

        public static string Preferences_ShowMouseClicks {
            get {
                return ResourceManager.GetString("Preferences_ShowMouseClicks", resourceCulture);
            }
        }

        public static string Preferences_ShowMouseClicksDescription {
            get {
                return ResourceManager.GetString("Preferences_ShowMouseClicksDescription", resourceCulture);
            }
        }

        public static string Preferences_ShowMouseClicksHint {
            get {
                return ResourceManager.GetString("Preferences_ShowMouseClicksHint", resourceCulture);
            }
        }

        public static string Preferences_LeftButton {
            get {
                return ResourceManager.GetString("Preferences_LeftButton", resourceCulture);
            }
        }

        public static string Preferences_MiddleButton {
            get {
                return ResourceManager.GetString("Preferences_MiddleButton", resourceCulture);
            }
        }

        public static string Preferences_RightButton {
            get {
                return ResourceManager.GetString("Preferences_RightButton", resourceCulture);
            }
        }

        public static string Preferences_CircleSize {
            get {
                return ResourceManager.GetString("Preferences_CircleSize", resourceCulture);
            }
        }

        public static string Preferences_CircleDuration {
            get {
                return ResourceManager.GetString("Preferences_CircleDuration", resourceCulture);
            }
        }

        public static string TrayMenu_Pause {
            get {
                return ResourceManager.GetString("TrayMenu_Pause", resourceCulture);
            }
        }

        public static string TrayMenu_RestartAsAdministrator {
            get {
                return ResourceManager.GetString("TrayMenu_RestartAsAdministrator", resourceCulture);
            }
        }

        public static string TrayMenu_Resume {
            get {
                return ResourceManager.GetString("TrayMenu_Resume", resourceCulture);
            }
        }

        public static string TrayMenu_Settings {
            get {
                return ResourceManager.GetString("TrayMenu_Settings", resourceCulture);
            }
        }

        public static string TrayMenu_SilentMode {
            get {
                return ResourceManager.GetString("TrayMenu_SilentMode", resourceCulture);
            }
        }

        public static string TrayStatus_Paused {
            get {
                return ResourceManager.GetString("TrayStatus_Paused", resourceCulture);
            }
        }

        public static string TrayStatus_Silent {
            get {
                return ResourceManager.GetString("TrayStatus_Silent", resourceCulture);
            }
        }

        public static string Preferences_SilentModeHotkey {
            get {
                return ResourceManager.GetString("Preferences_SilentModeHotkey", resourceCulture);
            }
        }

        public static string Preferences_PauseHotkey {
            get {
                return ResourceManager.GetString("Preferences_PauseHotkey", resourceCulture);
            }
        }

        public static string Preferences_HotkeyToolTip {
            get {
                return ResourceManager.GetString("Preferences_HotkeyToolTip", resourceCulture);
            }
        }

        public static string Preferences_ClearHotkey {
            get {
                return ResourceManager.GetString("Preferences_ClearHotkey", resourceCulture);
            }
        }
    }
}
