using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Carnac.Utilities
{
    /// <summary>
    /// Converts the font family name stored in the settings to a <see cref="FontFamily"/>. An empty name, or the name
    /// of a font that is not installed (any more), gives the fallback font, so the popups never end up with a
    /// font that was silently substituted by WPF.
    /// </summary>
    public class FontFamilyNameConverter : IValueConverter
    {
        /// <summary>Converter for XAML: checks the installed fonts and falls back to the system font of the user interface.</summary>
        public static readonly FontFamilyNameConverter Instance = new FontFamilyNameConverter();

        readonly Func<string, bool> isInstalled;
        readonly Func<FontFamily> fallback;

        public FontFamilyNameConverter()
            : this(InstalledFonts.Contains, () => SystemFonts.MessageFontFamily)
        {
        }

        public FontFamilyNameConverter(Func<string, bool> isInstalled, Func<FontFamily> fallback)
        {
            if (isInstalled == null)
                throw new ArgumentNullException("isInstalled");
            if (fallback == null)
                throw new ArgumentNullException("fallback");

            this.isInstalled = isInstalled;
            this.fallback = fallback;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var name = value as string;
            if (string.IsNullOrWhiteSpace(name))
                return fallback();

            name = name.Trim();
            return isInstalled(name) ? new FontFamily(name) : fallback();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
