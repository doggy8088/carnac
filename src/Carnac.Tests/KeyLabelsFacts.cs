using System;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Xunit;
using Message = Carnac.Logic.Models.Message;

namespace Carnac.Tests
{
    public class KeyLabelsFacts
    {
        static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        static KeyPress Press(bool control, params string[] input)
        {
            return new KeyPress(new ProcessInfo("foo"),
                new InterceptKeyEventArgs(Keys.A, KeyDirection.Down, false, control, false), false, input);
        }

        static string Join(System.Collections.Generic.IEnumerable<string> parts)
        {
            return string.Join(string.Empty, parts);
        }

        [Fact]
        public void GermanLabelsAreUsedForGermanCultures()
        {
            Assert.Equal("Strg", KeyLabels.Localize("Ctrl", German));
            Assert.Equal("Entf", KeyLabels.Localize("Delete", German));
            Assert.Equal("Einfg", KeyLabels.Localize("Insert", German));
            Assert.Equal("Pos1", KeyLabels.Localize("Home", German));
            Assert.Equal("Ende", KeyLabels.Localize("End", German));
            Assert.Equal("Bild↑", KeyLabels.Localize("PageUp", German));
            Assert.Equal("Bild↓", KeyLabels.Localize("PageDown", German));
            Assert.Equal("Leertaste", KeyLabels.Localize("Space", German));
        }

        [Fact]
        public void EveryGermanRegionUsesTheGermanLabels()
        {
            Assert.Equal("Strg", KeyLabels.Localize("Ctrl", CultureInfo.GetCultureInfo("de-AT")));
            Assert.Equal("Strg", KeyLabels.Localize("Ctrl", CultureInfo.GetCultureInfo("de-CH")));
            Assert.Equal("Strg", KeyLabels.Localize("Ctrl", CultureInfo.GetCultureInfo("de")));
        }

        [Fact]
        public void LanguagesWithoutEntriesKeepTheCanonicalNames()
        {
            foreach (var name in new[] { "en-US", "zh-TW", "zh-CN", "fr-FR", "ja-JP", "" })
            {
                Assert.Equal("Ctrl", KeyLabels.Localize("Ctrl", CultureInfo.GetCultureInfo(name)));
                Assert.Equal("Delete", KeyLabels.Localize("Delete", CultureInfo.GetCultureInfo(name)));
            }
        }

        [Fact]
        public void NamesWithoutAnEntryAreKept()
        {
            Assert.Equal("a", KeyLabels.Localize("a", German));
            Assert.Equal(" ", KeyLabels.Localize(" ", German));
            Assert.Equal("Alt", KeyLabels.Localize("Alt", German));
            Assert.Null(KeyLabels.Localize(null, German));
        }

        [Fact]
        public void WithoutACultureTheCanonicalNamesAreKept()
        {
            Assert.Equal("Ctrl", KeyLabels.Localize("Ctrl", null));
        }

        [Fact]
        public void KeyPressUsesTheGivenCulture()
        {
            var keyPress = Press(true, "Ctrl", "Delete");

            Assert.Equal("Strg + Entf", Join(keyPress.GetTextParts(German)));
            Assert.Equal("Ctrl + Delete", Join(keyPress.GetTextParts(CultureInfo.GetCultureInfo("en-US"))));
            Assert.Equal("Ctrl + Delete", Join(keyPress.GetTextParts(null)));
        }

        [Fact]
        public void KeyPressKeepsTheCanonicalNamesByDefault()
        {
            Assert.Null(KeyLabels.Culture);
            Assert.Equal("Ctrl + Delete", Join(Press(true, "Ctrl", "Delete").GetTextParts()));
        }

        [Fact]
        public void KeyPressUsesTheCultureOfTheApplication()
        {
            var keyPress = Press(true, "Ctrl", "Delete");
            KeyLabels.Culture = German;
            try
            {
                Assert.Equal("Strg + Entf", Join(keyPress.GetTextParts()));
                Assert.Equal("Strg + Entf", Join(new Message(keyPress).Text));
            }
            finally
            {
                KeyLabels.Culture = null;
            }
        }

        [Fact]
        public void SpaceInAShortcutIsLocalizedButSpaceInASentenceIsNot()
        {
            Assert.Equal("Strg + Leertaste", Join(Press(true, "Ctrl", " ").GetTextParts(German)));
            Assert.Equal("Ctrl + Space", Join(Press(true, "Ctrl", " ").GetTextParts(null)));
            Assert.Equal(" ", Join(Press(false, " ").GetTextParts(German)));
        }

        [Fact]
        public void ArrowsAreNotChanged()
        {
            Assert.Equal("←↑→↓", Join(Press(false, "Left", "Up", "Right", "Down").GetTextParts(German)).Replace(" + ", string.Empty));
        }
    }
}
