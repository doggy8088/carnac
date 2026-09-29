using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Carnac.Properties;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>
    /// Uses TestResources.resx (English: A, B, C), TestResources_fr.resx (A, B) and TestResources_fr-CA.resx (A),
    /// which are embedded in this assembly the same way the translations are embedded in Carnac.exe.
    /// </summary>
    public class EmbeddedResourceManagerFacts
    {
        static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr");
        static readonly CultureInfo FrenchCanada = CultureInfo.GetCultureInfo("fr-CA");
        static readonly CultureInfo FrenchFrance = CultureInfo.GetCultureInfo("fr-FR");

        static EmbeddedResourceManager Create()
        {
            return new EmbeddedResourceManager("Carnac.Tests.TestResources", typeof(EmbeddedResourceManagerFacts).Assembly);
        }

        [Fact]
        public void CultureWithItsOwnResourceUsesIt()
        {
            Assert.Equal("fr-CA A", Create().GetString("A", FrenchCanada));
            Assert.Equal("fr A", Create().GetString("A", French));
        }

        [Fact]
        public void KeyThatTheCultureDoesNotHaveComesFromTheParentCulture()
        {
            Assert.Equal("fr B", Create().GetString("B", FrenchCanada));
        }

        [Fact]
        public void KeyThatNoTranslationHasComesFromEnglish()
        {
            Assert.Equal("neutral C", Create().GetString("C", FrenchCanada));
            Assert.Equal("neutral C", Create().GetString("C", French));
        }

        [Fact]
        public void CultureWithoutAResourceUsesTheOneOfItsParent()
        {
            Assert.Equal("fr A", Create().GetString("A", FrenchFrance));
        }

        [Fact]
        public void CultureWithoutAnyTranslationUsesEnglish()
        {
            var manager = Create();

            Assert.Equal("neutral A", manager.GetString("A", CultureInfo.GetCultureInfo("de-DE")));
            Assert.Equal("neutral A", manager.GetString("A", CultureInfo.GetCultureInfo("en-US")));
            Assert.Equal("neutral A", manager.GetString("A", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void KeyThatDoesNotExistAnywhereIsNull()
        {
            Assert.Null(Create().GetString("Nope", FrenchCanada));
        }

        [Fact]
        public void CultureOfTheThreadIsUsedWhenNoCultureIsGiven()
        {
            var manager = Create();
            var previous = Thread.CurrentThread.CurrentUICulture;
            try
            {
                Thread.CurrentThread.CurrentUICulture = FrenchCanada;
                Assert.Equal("fr-CA A", manager.GetString("A"));
            }
            finally
            {
                Thread.CurrentThread.CurrentUICulture = previous;
            }
        }

        [Fact]
        public void ResourceSetWithoutParentsExistsOnlyForCulturesWithTheirOwnResource()
        {
            var manager = Create();

            Assert.NotNull(manager.GetResourceSet(FrenchCanada, true, false));
            Assert.NotNull(manager.GetResourceSet(French, true, false));
            Assert.Null(manager.GetResourceSet(FrenchFrance, true, false));
            Assert.Null(manager.GetResourceSet(CultureInfo.GetCultureInfo("de-DE"), true, false));
        }

        [Fact]
        public void ResourceSetWithParentsFallsBackToTheParentAndEnglish()
        {
            var manager = Create();

            Assert.Same(manager.GetResourceSet(French, true, false), manager.GetResourceSet(FrenchFrance, true, true));
            Assert.Same(manager.GetResourceSet(CultureInfo.InvariantCulture, true, false), manager.GetResourceSet(CultureInfo.GetCultureInfo("de-DE"), true, true));
        }

        [Fact]
        public void ResourcesAreOnlyLoadedWhenRequested()
        {
            var manager = Create();

            Assert.Null(manager.GetResourceSet(French, false, false));

            manager.GetString("A", French);

            Assert.NotNull(manager.GetResourceSet(French, false, false));
        }

        [Fact]
        public void ResourceSetOfACultureIsLoadedOnce()
        {
            var manager = Create();

            Assert.Same(manager.GetResourceSet(French, true, false), manager.GetResourceSet(French, true, false));
        }

        [Fact]
        public void ResourcesCanBeReleasedAndAreLoadedAgain()
        {
            var manager = Create();
            var first = manager.GetResourceSet(French, true, false);

            manager.ReleaseAllResources();

            Assert.Equal("fr A", manager.GetString("A", French));
            Assert.NotSame(first, manager.GetResourceSet(French, true, false));
        }

        [Fact]
        public void ResourceNameIsTheBaseNameAndTheCulture()
        {
            Assert.Equal("Carnac.Tests.TestResources_fr-CA.resources", Create().GetResourceName(FrenchCanada));
            Assert.Equal("Carnac.Tests.TestResources_zh-TW.resources", Create().GetResourceName(CultureInfo.GetCultureInfo("zh-TW")));
        }

        [Fact]
        public void LookupsFromSeveralThreadsAllSucceed()
        {
            var manager = Create();
            var results = new List<string>();
            var threads = Enumerable.Range(0, 8).Select(i => new Thread(() =>
            {
                var value = manager.GetString("A", i % 2 == 0 ? FrenchCanada : French) + "/" + manager.GetString("B", FrenchCanada);
                lock (results)
                {
                    results.Add(value);
                }
            })).ToList();

            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());

            Assert.Equal(4, results.Count(r => r == "fr-CA A/fr B"));
            Assert.Equal(4, results.Count(r => r == "fr A/fr B"));
        }
    }
}
