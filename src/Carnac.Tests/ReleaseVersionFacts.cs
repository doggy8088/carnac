using System;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class ReleaseVersionFacts
    {
        static ReleaseVersion Parse(string text)
        {
            ReleaseVersion version;
            Assert.True(ReleaseVersion.TryParse(text, out version), "'" + text + "' should be a version");
            return version;
        }

        static void AssertMalformed(string text)
        {
            ReleaseVersion version;
            Assert.False(ReleaseVersion.TryParse(text, out version), "'" + text + "' should not be a version");
            Assert.Null(version);
        }

        static bool IsNewer(string candidate, string current)
        {
            return Parse(candidate).IsNewerThan(Parse(current));
        }

        // ---- parsing ----

        [Fact]
        public void parses_a_plain_version()
        {
            Assert.Equal("2.4.1", Parse("2.4.1").ToString());
        }

        [Fact]
        public void ignores_a_leading_v_in_either_case()
        {
            Assert.Equal(0, Parse("v2.4.1").CompareTo(Parse("2.4.1")));
            Assert.Equal(0, Parse("V2.4.1").CompareTo(Parse("2.4.1")));
            Assert.Equal("2.4.1", Parse("v2.4.1").ToString());
        }

        [Fact]
        public void ignores_whitespace_around_the_tag()
        {
            Assert.Equal("2.4.1", Parse("  v2.4.1\n").ToString());
        }

        [Fact]
        public void accepts_two_to_four_numbers_and_fills_up_with_zeros()
        {
            Assert.Equal(0, Parse("2.4").CompareTo(Parse("2.4.0")));
            Assert.Equal(0, Parse("2.4.0").CompareTo(Parse("2.4.0.0")));
            Assert.Equal("2.4.0", Parse("2.4").ToString());
            Assert.Equal("2.4.1.5", Parse("2.4.1.5").ToString());
        }

        [Fact]
        public void reads_a_pre_release_suffix()
        {
            var version = Parse("v2.5.0-beta.1");

            Assert.True(version.IsPreRelease);
            Assert.Equal("2.5.0-beta.1", version.ToString());
            Assert.False(Parse("2.5.0").IsPreRelease);
        }

        [Fact]
        public void ignores_build_metadata()
        {
            Assert.Equal(0, Parse("2.4.1+build.5").CompareTo(Parse("2.4.1")));
            Assert.Equal(0, Parse("2.4.1-rc.1+abc123").CompareTo(Parse("2.4.1-rc.1")));
        }

        [Fact]
        public void a_hyphen_inside_the_pre_release_is_part_of_it()
        {
            Assert.Equal("2.4.1-beta-2", Parse("2.4.1-beta-2").ToString());
        }

        [Fact]
        public void rejects_malformed_tags()
        {
            AssertMalformed(null);
            AssertMalformed("");
            AssertMalformed("   ");
            AssertMalformed("v");
            AssertMalformed("latest");
            AssertMalformed("release-2024");
            AssertMalformed("vv2.4.1");
            AssertMalformed("2");
            AssertMalformed("v2");
            AssertMalformed("2.x.1");
            AssertMalformed("2..1");
            AssertMalformed(".2.1");
            AssertMalformed("2.4.");
            AssertMalformed("2.4.1.5.6");
            AssertMalformed("2.4.1-");
            AssertMalformed("2.4.1-beta..1");
            AssertMalformed("2.4.1-be ta");
            AssertMalformed("-1.2.3");
            AssertMalformed("1.2.-3");
            AssertMalformed("2.4.1 beta");
            AssertMalformed("2,4,1");
        }

        [Fact]
        public void rejects_numbers_that_would_overflow()
        {
            AssertMalformed("99999999999.0.0");
            AssertMalformed("1.2.3.4294967296");
        }

        [Fact]
        public void reads_the_version_of_an_assembly()
        {
            var version = ReleaseVersion.FromVersion(new Version(2, 3, 0, 0));

            Assert.Equal("2.3.0", version.ToString());
            Assert.Equal(0, version.CompareTo(Parse("2.3.0")));
        }

        [Fact]
        public void an_assembly_version_with_missing_parts_is_filled_up_with_zeros()
        {
            Assert.Equal(0, ReleaseVersion.FromVersion(new Version(2, 3)).CompareTo(Parse("2.3.0.0")));
        }

        [Fact]
        public void from_version_requires_a_version()
        {
            Assert.Throws<ArgumentNullException>(() => ReleaseVersion.FromVersion(null));
        }

        // ---- ordering ----

        [Fact]
        public void compares_numbers_numerically_not_as_text()
        {
            Assert.True(IsNewer("2.10.0", "2.9.0"));
            Assert.True(IsNewer("10.0.0", "9.9.9"));
            Assert.False(IsNewer("2.9.0", "2.10.0"));
        }

        [Fact]
        public void a_higher_major_minor_or_patch_is_newer()
        {
            Assert.True(IsNewer("3.0.0", "2.9.9"));
            Assert.True(IsNewer("2.4.0", "2.3.9"));
            Assert.True(IsNewer("2.3.1", "2.3.0"));
            Assert.True(IsNewer("2.3.0.1", "2.3.0"));
        }

        [Fact]
        public void the_same_version_is_not_newer()
        {
            Assert.False(IsNewer("2.3.0", "2.3.0"));
            Assert.False(IsNewer("v2.3.0", "2.3.0.0"));
        }

        [Fact]
        public void an_older_version_is_not_newer()
        {
            Assert.False(IsNewer("2.2.9", "2.3.0"));
        }

        [Fact]
        public void a_pre_release_is_older_than_its_release()
        {
            Assert.True(IsNewer("2.5.0", "2.5.0-beta.1"));
            Assert.False(IsNewer("2.5.0-beta.1", "2.5.0"));
        }

        [Fact]
        public void a_pre_release_of_a_later_version_is_newer_than_an_earlier_release()
        {
            Assert.True(IsNewer("2.5.0-beta.1", "2.4.0"));
            Assert.False(IsNewer("2.4.0-beta.1", "2.4.0"));
        }

        [Fact]
        public void orders_pre_releases_like_semantic_versioning()
        {
            // the example chain of semver.org
            var ordered = new[]
            {
                "1.0.0-alpha", "1.0.0-alpha.1", "1.0.0-alpha.beta", "1.0.0-beta", "1.0.0-beta.2", "1.0.0-beta.11", "1.0.0-rc.1", "1.0.0"
            };

            for (var i = 0; i < ordered.Length - 1; i++)
            {
                Assert.True(IsNewer(ordered[i + 1], ordered[i]), ordered[i + 1] + " should be newer than " + ordered[i]);
                Assert.False(IsNewer(ordered[i], ordered[i + 1]), ordered[i] + " should not be newer than " + ordered[i + 1]);
            }
        }

        [Fact]
        public void numeric_pre_release_identifiers_are_older_than_words_and_compared_as_numbers()
        {
            Assert.True(IsNewer("1.0.0-alpha", "1.0.0-1"));
            Assert.True(IsNewer("1.0.0-beta.11", "1.0.0-beta.2"));
            Assert.Equal(0, Parse("1.0.0-rc.1").CompareTo(Parse("1.0.0-rc.01")));
        }

        [Fact]
        public void huge_numeric_identifiers_do_not_overflow()
        {
            Assert.True(IsNewer("1.0.0-beta.99999999999999999999999", "1.0.0-beta.9"));
        }

        [Fact]
        public void anything_is_newer_than_nothing()
        {
            Assert.Equal(1, Parse("1.0.0").CompareTo(null));
        }
    }
}
