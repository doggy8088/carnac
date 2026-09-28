using System;
using System.Globalization;
using System.Threading;
using Carnac.Logic;
using Carnac.Logic.Models;

namespace Carnac.Utilities
{
    /// <summary>
    /// Applies the language of the user interface: the resources (Preferences window, tray menu) and the key labels.
    /// </summary>
    public static class UiLanguage
    {
        // The display language of Windows. It has to be read before the first language is applied, because
        // afterwards CurrentUICulture is the culture that was applied.
        static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;

        /// <summary>
        /// Applies the language setting (see <see cref="PopupSettings.Language"/>) and applies it again whenever it changes,
        /// for example when it is edited in Preferences or reset to its default.
        /// A window that is already open keeps the language it was created with; the tray menu and the key labels change
        /// at once (<paramref name="languageChanged"/> lets the caller refresh what was already created).
        /// </summary>
        public static void Follow(PopupSettings settings, Action languageChanged)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");

            Apply(settings.Language);

            settings.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != "Language")
                    return;

                Apply(settings.Language);
                if (languageChanged != null)
                    languageChanged();
            };
        }

        public static void Apply(string configuredLanguage)
        {
            Apply(configuredLanguage, SystemUiCulture);
        }

        public static void Apply(string configuredLanguage, CultureInfo systemUiCulture)
        {
            var culture = UiLanguages.Resolve(configuredLanguage, systemUiCulture);

            // Resources.Culture makes the strongly typed resource class (and so every {x:Static} binding) use the
            // culture no matter which thread reads it. The other two are for code that uses the culture of the thread.
            Properties.Resources.Culture = culture;
            KeyLabels.Culture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}
