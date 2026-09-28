using System;
using System.Globalization;
using System.Windows.Data;
using Carnac.Logic;

namespace Carnac.UI
{
    /// <summary>
    /// Converts the text of a key into whether it should be drawn as a key cap.
    /// </summary>
    public class SpecialKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return SpecialKeys.IsSpecialKey(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
