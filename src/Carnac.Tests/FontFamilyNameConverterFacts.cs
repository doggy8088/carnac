using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Carnac.Utilities;
using Xunit;

namespace Carnac.Tests
{
    public class FontFamilyNameConverterFacts
    {
        static readonly FontFamily Fallback = new FontFamily("Arial");

        readonly List<string> installed = new List<string> { "Consolas", "Segoe UI" };
        readonly List<string> checkedNames = new List<string>();

        FontFamilyNameConverter CreateConverter()
        {
            return new FontFamilyNameConverter(
                name =>
                {
                    checkedNames.Add(name);
                    return installed.Contains(name, StringComparer.OrdinalIgnoreCase);
                },
                () => Fallback);
        }

        object Convert(object value)
        {
            return CreateConverter().Convert(value, typeof(FontFamily), null, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void InstalledFontIsUsed()
        {
            var result = Assert.IsType<FontFamily>(Convert("Consolas"));

            Assert.Equal("Consolas", result.Source);
        }

        [Fact]
        public void InstalledFontIsFoundIgnoringCaseAndSurroundingSpaces()
        {
            var result = Assert.IsType<FontFamily>(Convert("  segoe ui "));

            Assert.Equal("segoe ui", result.Source);
        }

        [Fact]
        public void FontThatIsNotInstalledFallsBackToTheDefaultFont()
        {
            Assert.Same(Fallback, Convert("Deleted Font"));
        }

        [Fact]
        public void EmptyNameFallsBackToTheDefaultFontWithoutLookingAtTheInstalledFonts()
        {
            Assert.Same(Fallback, Convert(string.Empty));
            Assert.Same(Fallback, Convert("   "));
            Assert.Same(Fallback, Convert(null));
            Assert.Empty(checkedNames);
        }

        [Fact]
        public void ValueThatIsNotAStringFallsBackToTheDefaultFont()
        {
            Assert.Same(Fallback, Convert(42));
            Assert.Same(Fallback, Convert(DependencyProperty.UnsetValue));
        }

        [Fact]
        public void ConvertingBackIsNotSupported()
        {
            Assert.Throws<NotSupportedException>(
                () => CreateConverter().ConvertBack(Fallback, typeof(string), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void DelegatesAreRequired()
        {
            Assert.Throws<ArgumentNullException>(() => new FontFamilyNameConverter(null, () => Fallback));
            Assert.Throws<ArgumentNullException>(() => new FontFamilyNameConverter(name => true, null));
        }

        [Fact]
        public void ConverterUsedByTheOverlayFallsBackToTheSystemFontForAFontThatIsNotInstalled()
        {
            var result = Assert.IsType<FontFamily>(
                FontFamilyNameConverter.Instance.Convert("No Such Font (Carnac test)", typeof(FontFamily), null, CultureInfo.InvariantCulture));

            Assert.Equal(SystemFonts.MessageFontFamily.Source, result.Source);
        }

        [Fact]
        public void ConverterUsedByTheOverlayFallsBackToTheSystemFontForTheDefaultSetting()
        {
            var result = Assert.IsType<FontFamily>(
                FontFamilyNameConverter.Instance.Convert(string.Empty, typeof(FontFamily), null, CultureInfo.InvariantCulture));

            Assert.Equal(SystemFonts.MessageFontFamily.Source, result.Source);
        }

        [Fact]
        public void ConverterUsedByTheOverlayUsesAnInstalledFont()
        {
            var family = Fonts.SystemFontFamilies.FirstOrDefault();
            if (family == null)
                return; // a machine without any font: nothing to look up

            var result = Assert.IsType<FontFamily>(
                FontFamilyNameConverter.Instance.Convert(family.Source, typeof(FontFamily), null, CultureInfo.InvariantCulture));

            Assert.Equal(family.Source, result.Source);
        }

        [Fact]
        public void InstalledFontsFindsTheSystemFontsIgnoringCase()
        {
            // Only names with plain ASCII letters, so that changing the case is unambiguous.
            var family = Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source.Length > 0 && f.Source.All(c => c >= 'A' && c <= 'z' && char.IsLetter(c)));
            if (family == null)
                return;

            Assert.True(InstalledFonts.Contains(family.Source));
            Assert.True(InstalledFonts.Contains(family.Source.ToUpperInvariant()));
            Assert.True(InstalledFonts.Contains(" " + family.Source.ToLowerInvariant() + " "));
        }

        [Fact]
        public void InstalledFontsDoesNotFindMissingFontsOrEmptyNames()
        {
            Assert.False(InstalledFonts.Contains("No Such Font (Carnac test)"));
            Assert.False(InstalledFonts.Contains(string.Empty));
            Assert.False(InstalledFonts.Contains("  "));
            Assert.False(InstalledFonts.Contains(null));
        }
    }
}
