using System.Collections.Generic;
using System.Linq;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class ShortcutCollectionFacts
    {
        static ShortcutCollection CollectionFor(string process)
        {
            return new ShortcutCollection(new List<KeyShortcut>()) { Process = process };
        }

        [Fact]
        public void keymap_without_a_process_applies_to_every_process()
        {
            Assert.True(CollectionFor(null).AppliesTo("chrome"));
            Assert.True(CollectionFor("").AppliesTo("chrome"));
            Assert.True(CollectionFor("   ").AppliesTo("chrome"));
            Assert.True(CollectionFor(" | ").AppliesTo("chrome"));
            Assert.Empty(CollectionFor(null).ProcessNames);
        }

        [Fact]
        public void a_single_name_matches_that_process()
        {
            var sut = CollectionFor("Code");

            Assert.True(sut.AppliesTo("Code"));
            Assert.False(sut.AppliesTo("devenv"));
        }

        [Fact]
        public void names_are_compared_case_insensitively()
        {
            Assert.True(CollectionFor("Code").AppliesTo("code"));
            Assert.True(CollectionFor("code").AppliesTo("Code"));
            Assert.True(CollectionFor("CODE").AppliesTo("cOdE"));
        }

        [Fact]
        public void names_must_match_in_full()
        {
            var sut = CollectionFor("chrome");

            Assert.False(sut.AppliesTo("chromedriver"));
            Assert.False(sut.AppliesTo("chrom"));
            Assert.False(sut.AppliesTo("chrome.exe"));
        }

        [Fact]
        public void alternatives_are_separated_by_a_bar()
        {
            var sut = CollectionFor("chrome|msedge");

            Assert.True(sut.AppliesTo("chrome"));
            Assert.True(sut.AppliesTo("msedge"));
            Assert.True(sut.AppliesTo("MSEdge"));
            Assert.False(sut.AppliesTo("firefox"));
            Assert.Equal(new[] { "chrome", "msedge" }, sut.ProcessNames.ToArray());
        }

        [Fact]
        public void whitespace_empty_alternatives_and_duplicates_are_ignored()
        {
            var sut = CollectionFor(" chrome | |msedge||Chrome ");

            Assert.Equal(new[] { "chrome", "msedge" }, sut.ProcessNames.ToArray());
            Assert.True(sut.AppliesTo("msedge"));
        }

        [Fact]
        public void a_keymap_with_a_process_does_not_apply_when_the_process_is_unknown()
        {
            Assert.False(CollectionFor("chrome").AppliesTo(null));
            Assert.True(CollectionFor(null).AppliesTo(null));
        }

        [Fact]
        public void process_text_is_kept_as_written()
        {
            Assert.Equal("chrome|msedge", CollectionFor("chrome|msedge").Process);
        }
    }
}
