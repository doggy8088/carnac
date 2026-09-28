namespace Carnac.Logic.Enums
{
    /// <summary>
    /// How runs of the same key are summarised as "x N" in a popup.
    /// </summary>
    public enum RepeatedKeyGrouping
    {
        /// <summary>
        /// Typed characters are summarised once they repeat often enough (the threshold setting).
        /// </summary>
        Threshold = 0,

        /// <summary>
        /// Typed characters are always shown as typed. Named keys ("Back", arrows) and shortcuts are still summarised.
        /// </summary>
        Never = 1
    }
}
