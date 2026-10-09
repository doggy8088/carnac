namespace Carnac.Logic
{
    /// <summary>
    /// Tells whether processes run with administrator rights ("elevated"). Windows does not deliver the keys typed into an elevated
    /// application to the keyboard hook of a normal process (UIPI), so Carnac cannot show them.
    /// </summary>
    public interface IElevationChecker
    {
        /// <summary>True when Carnac itself runs elevated: then it can see the keys of every application.</summary>
        bool IsCurrentProcessElevated { get; }

        /// <summary>
        /// True when the process runs elevated, false when it does not, and null when that cannot be told (for example
        /// because the process has just ended).
        /// </summary>
        bool? IsProcessElevated(int processId);
    }
}
