using System;
using Carnac.Logic.Enums;

namespace Carnac.Logic.Models
{
    /// <summary>
    /// Decides how many presses of the same key in a row it takes before they are shown as "x N".
    /// Immutable, so a <see cref="Message"/> can carry the policy it was built with and render the same way later.
    /// </summary>
    public sealed class RepeatedKeyPolicy
    {
        /// <summary>Typed characters ("ll" in "hello", "((", "www") read naturally as typed until the repeat is clearly deliberate.</summary>
        public const int DefaultTypedCharacterThreshold = 4;
        public const int MinimumTypedCharacterThreshold = 2;
        public const int MaximumTypedCharacterThreshold = 10;

        // Digits wait longer, because "1000000" would otherwise read as "10 x 6".
        const int MinimumTypedDigitRepeatToSummarise = 10;

        // Named keys ("Back", "Left"), and anything pressed with a modifier, always collapse from two.
        const int MinimumNamedKeyRepeatToSummarise = 2;

        static readonly RepeatedKeyPolicy defaultPolicy =
            new RepeatedKeyPolicy(RepeatedKeyGrouping.Threshold, DefaultTypedCharacterThreshold);
        static readonly RepeatedKeyPolicy neverPolicy =
            new RepeatedKeyPolicy(RepeatedKeyGrouping.Never, DefaultTypedCharacterThreshold);

        readonly RepeatedKeyGrouping grouping;
        readonly int typedCharacterThreshold;

        RepeatedKeyPolicy(RepeatedKeyGrouping grouping, int typedCharacterThreshold)
        {
            this.grouping = grouping;
            this.typedCharacterThreshold = typedCharacterThreshold;
        }

        /// <summary>The behaviour before the setting existed: typed characters are summarised from the 4th repeat.</summary>
        public static RepeatedKeyPolicy Default
        {
            get { return defaultPolicy; }
        }

        /// <summary>Typed characters are never summarised.</summary>
        public static RepeatedKeyPolicy Never
        {
            get { return neverPolicy; }
        }

        /// <summary>
        /// Creates the policy for the given settings. A threshold outside the supported range is
        /// moved to the nearest supported value, and is ignored altogether when grouping is off.
        /// </summary>
        public static RepeatedKeyPolicy Create(RepeatedKeyGrouping grouping, int typedCharacterThreshold)
        {
            if (grouping == RepeatedKeyGrouping.Never)
                return neverPolicy;

            var threshold = Math.Min(Math.Max(typedCharacterThreshold, MinimumTypedCharacterThreshold), MaximumTypedCharacterThreshold);
            return threshold == DefaultTypedCharacterThreshold
                ? defaultPolicy
                : new RepeatedKeyPolicy(RepeatedKeyGrouping.Threshold, threshold);
        }

        public RepeatedKeyGrouping Grouping
        {
            get { return grouping; }
        }

        /// <summary>The number of presses of a typed character in a row that is shown as "x N".</summary>
        public int TypedCharacterThreshold
        {
            get { return typedCharacterThreshold; }
        }

        /// <summary>
        /// The number of presses in a row it takes to show "x N" for a key that is not typed text
        /// (named keys, arrows, function keys, anything pressed with a modifier).
        /// </summary>
        public int MinimumNamedKeyRepeat
        {
            get { return MinimumNamedKeyRepeatToSummarise; }
        }

        /// <summary>
        /// The number of presses in a row it takes to show "x N" for a typed character.
        /// <see cref="int.MaxValue"/> when typed characters are never summarised.
        /// </summary>
        public int GetMinimumTypedCharacterRepeat(bool isDigit)
        {
            if (grouping == RepeatedKeyGrouping.Never)
                return int.MaxValue;

            return isDigit
                ? Math.Max(typedCharacterThreshold, MinimumTypedDigitRepeatToSummarise)
                : typedCharacterThreshold;
        }
    }
}
