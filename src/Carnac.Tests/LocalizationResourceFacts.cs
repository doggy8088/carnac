using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using Carnac.Logic;
using Carnac.Properties;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>
    /// Checks the translations as they are compiled: the neutral (English) resources and one satellite assembly per language.
    /// </summary>
    public class LocalizationResourceFacts
    {
        static ResourceSet Load(CultureInfo culture)
        {
            // Only the resources of exactly this culture, no fallback to the parent or English.
            return Resources.ResourceManager.GetResourceSet(culture, true, false);
        }

        static IDictionary<string, string> ReadStrings(ResourceSet set)
        {
            return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value);
        }

        static IEnumerable<string> TranslatedLanguages
        {
            get { return UiLanguages.Codes.Where(c => c != "en"); }
        }

        static string FindSourceDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Carnac", "Carnac.csproj")))
                directory = directory.Parent;

            Assert.True(directory != null, "The src folder was not found above " + AppDomain.CurrentDomain.BaseDirectory);
            return directory.FullName;
        }

        [Fact]
        public void NeutralResourcesAreEnglishAndNeverEmpty()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));

            Assert.NotEmpty(neutral);
            foreach (var entry in neutral)
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.Value), "Resources.resx: '" + entry.Key + "' is empty");
            }
            Assert.Equal("Exit", neutral["ShellView_Exit"]);
        }

        [Fact]
        public void EveryTranslatedLanguageHasAResourceSet()
        {
            foreach (var code in TranslatedLanguages)
            {
                Assert.True(Load(CultureInfo.GetCultureInfo(code)) != null,
                    "No satellite resources for '" + code + "': add Properties\\Resources." + code + ".resx to Carnac.csproj");
            }
        }

        [Fact]
        public void EveryTranslationHasEveryKeyOfTheNeutralResources()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));

            foreach (var code in TranslatedLanguages)
            {
                var translated = ReadStrings(Load(CultureInfo.GetCultureInfo(code)));

                var missing = neutral.Keys.Where(k => !translated.ContainsKey(k)).ToArray();
                Assert.True(missing.Length == 0, "Resources." + code + ".resx misses: " + string.Join(", ", missing));

                var empty = neutral.Keys.Where(k => string.IsNullOrWhiteSpace(translated[k])).ToArray();
                Assert.True(empty.Length == 0, "Resources." + code + ".resx has empty translations: " + string.Join(", ", empty));
            }
        }

        [Fact]
        public void TranslationsDoNotHaveKeysThatAreNotInTheNeutralResources()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));

            foreach (var code in TranslatedLanguages)
            {
                var translated = ReadStrings(Load(CultureInfo.GetCultureInfo(code)));

                var unknown = translated.Keys.Where(k => !neutral.ContainsKey(k)).ToArray();
                Assert.True(unknown.Length == 0, "Resources." + code + ".resx has keys that Resources.resx does not have: " + string.Join(", ", unknown));
            }
        }

        [Fact]
        public void ChineseTranslationsAreTranslated()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));

            foreach (var code in new[] { "zh-TW", "zh-CN" })
            {
                var translated = ReadStrings(Load(CultureInfo.GetCultureInfo(code)));

                var same = neutral.Keys.Where(k => translated[k] == neutral[k]).ToArray();
                Assert.True(same.Length == 0, "Resources." + code + ".resx still has the English text of: " + string.Join(", ", same));
            }
        }

        [Fact]
        public void TrayMenuIsTranslated()
        {
            Assert.Equal("Exit", Resources.ResourceManager.GetString("ShellView_Exit", CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal("結束", Resources.ResourceManager.GetString("ShellView_Exit", CultureInfo.GetCultureInfo("zh-TW")));
            Assert.Equal("退出", Resources.ResourceManager.GetString("ShellView_Exit", CultureInfo.GetCultureInfo("zh-CN")));
            // Languages without a translation use English.
            Assert.Equal("Exit",Resources.ResourceManager.GetString("ShellView_Exit", CultureInfo.GetCultureInfo("de-DE")));
        }

        [Fact]
        public void StronglyTypedClassHasAPropertyForEveryResource()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));
            var properties = typeof(Resources)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name)
                .ToArray();

            var missing = neutral.Keys.Where(k => !properties.Contains(k)).ToArray();
            Assert.True(missing.Length == 0, "Resources.Designer.cs misses (regenerate it from Resources.resx): " + string.Join(", ", missing));

            var stale = properties.Where(p => !neutral.ContainsKey(p)).ToArray();
            Assert.True(stale.Length == 0, "Resources.Designer.cs has properties that Resources.resx does not have: " + string.Join(", ", stale));
        }

        [Fact]
        public void StronglyTypedClassIsPublicSoThatXamlCanUseIt()
        {
            Assert.True(typeof(Resources).IsPublic);
        }

        [Fact]
        public void XamlOnlyUsesResourcesThatExist()
        {
            var neutral = ReadStrings(Load(CultureInfo.InvariantCulture));
            var xamlFiles = Directory.GetFiles(Path.Combine(FindSourceDirectory(), "Carnac"), "*.xaml", SearchOption.AllDirectories)
                .Where(f => f.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0
                            && f.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .ToArray();
            var reference = new Regex(@"\{x:Static\s+\w+:Resources\.(?<key>\w+)\s*\}");

            Assert.True(xamlFiles.Any(f => f.EndsWith("PreferencesView.xaml", StringComparison.OrdinalIgnoreCase)), "PreferencesView.xaml was not found");
            var used = new HashSet<string>();
            foreach (var file in xamlFiles)
            {
                foreach (Match match in reference.Matches(File.ReadAllText(file)))
                {
                    var key = match.Groups["key"].Value;
                    Assert.True(neutral.ContainsKey(key), Path.GetFileName(file) + " uses Resources." + key + ", which is not in Resources.resx");
                    used.Add(key);
                }
            }

            Assert.True(used.Count > 20, "The Preferences window should be bound to the resources, found " + used.Count + " references");
        }
    }
}
