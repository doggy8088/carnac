using System;
using System.Globalization;
using System.Linq;

namespace Carnac.Logic
{
    /// <summary>
    /// A version as it appears in a release tag: <c>2.4.1</c>, <c>v2.4.1</c>, <c>v2.4.1-beta.1</c>, <c>2.4.1+build5</c>.
    /// Two to four numeric parts, an optional pre-release suffix (<c>-beta.1</c>, ordered like Semantic Versioning: a pre-release
    /// is older than the release it precedes) and optional build metadata (<c>+...</c>, ignored).
    /// </summary>
    public sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        readonly int[] numbers;
        readonly string[] preRelease;

        ReleaseVersion(int[] numbers, string[] preRelease)
        {
            this.numbers = numbers;
            this.preRelease = preRelease;
        }

        public bool IsPreRelease
        {
            get { return preRelease.Length > 0; }
        }

        /// <summary>Parses a release tag; false for anything that is not a version (a malformed tag never counts as an update).</summary>
        public static bool TryParse(string text, out ReleaseVersion version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();
            if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V') && char.IsDigit(text[1]))
                text = text.Substring(1);

            var plus = text.IndexOf('+');
            if (plus >= 0)
                text = text.Substring(0, plus);

            var core = text;
            var preRelease = new string[0];
            var dash = text.IndexOf('-');
            if (dash >= 0)
            {
                core = text.Substring(0, dash);
                preRelease = text.Substring(dash + 1).Split('.');
                if (preRelease.Any(id => !IsValidIdentifier(id)))
                    return false;
            }

            var parts = core.Split('.');
            if (parts.Length < 2 || parts.Length > 4)
                return false;

            var numbers = new int[4];
            for (var i = 0; i < parts.Length; i++)
            {
                if (!IsAsciiNumber(parts[i]) || parts[i].Length > 9)
                    return false;

                numbers[i] = int.Parse(parts[i], CultureInfo.InvariantCulture);
            }

            version = new ReleaseVersion(numbers, preRelease);
            return true;
        }

        /// <summary>The version of an assembly, for example <c>2.3.0.0</c>.</summary>
        public static ReleaseVersion FromVersion(Version version)
        {
            if (version == null)
                throw new ArgumentNullException("version");

            return new ReleaseVersion(new[] { version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0) }, new string[0]);
        }

        public int CompareTo(ReleaseVersion other)
        {
            if (other == null)
                return 1;

            for (var i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] != other.numbers[i])
                    return numbers[i].CompareTo(other.numbers[i]);
            }

            // 2.4.0-beta.1 comes before 2.4.0
            if (preRelease.Length == 0 || other.preRelease.Length == 0)
                return other.preRelease.Length.CompareTo(preRelease.Length);

            for (var i = 0; i < Math.Min(preRelease.Length, other.preRelease.Length); i++)
            {
                var result = CompareIdentifiers(preRelease[i], other.preRelease[i]);
                if (result != 0)
                    return result;
            }

            return preRelease.Length.CompareTo(other.preRelease.Length);
        }

        public bool IsNewerThan(ReleaseVersion other)
        {
            return CompareTo(other) > 0;
        }

        /// <summary>For example <c>2.4.1</c> or <c>2.4.1-beta.1</c>: at least three numbers, a fourth one only when it is not zero.</summary>
        public override string ToString()
        {
            var count = numbers[3] != 0 ? 4 : 3;
            var text = string.Join(".", numbers.Take(count).Select(n => n.ToString(CultureInfo.InvariantCulture)));
            return preRelease.Length == 0 ? text : text + "-" + string.Join(".", preRelease);
        }

        static int CompareIdentifiers(string left, string right)
        {
            var leftIsNumber = IsAsciiNumber(left);
            var rightIsNumber = IsAsciiNumber(right);
            if (leftIsNumber && rightIsNumber)
            {
                // compared as numbers without parsing them, so that huge values cannot overflow
                left = left.TrimStart('0');
                right = right.TrimStart('0');
                return left.Length != right.Length
                    ? left.Length.CompareTo(right.Length)
                    : string.CompareOrdinal(left, right);
            }

            // numbers are older than words: 1.0.0-1 < 1.0.0-alpha
            if (leftIsNumber)
                return -1;
            if (rightIsNumber)
                return 1;

            return string.CompareOrdinal(left, right);
        }

        static bool IsAsciiNumber(string text)
        {
            return text.Length > 0 && text.All(c => c >= '0' && c <= '9');
        }

        static bool IsValidIdentifier(string identifier)
        {
            return identifier.Length > 0 && identifier.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-');
        }
    }
}
