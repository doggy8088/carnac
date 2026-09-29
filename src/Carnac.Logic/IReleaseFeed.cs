namespace Carnac.Logic
{
    /// <summary>Where the newest release is looked up; the real one asks GitHub, tests use a fake and never touch the network.</summary>
    public interface IReleaseFeed
    {
        /// <summary>The JSON of the latest release. Throws when it cannot be fetched.</summary>
        string GetLatestReleaseJson();
    }
}
