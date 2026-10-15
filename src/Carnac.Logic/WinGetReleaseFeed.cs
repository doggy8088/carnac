using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace Carnac.Logic
{
    /// <summary>
    /// Queries WinGet for the latest available version of <c>doggy8088.Carnac</c>, so WinGet users are only notified
    /// once the WinGet manifest PR has merged and is available in their WinGet source.
    /// </summary>
    public sealed class WinGetReleaseFeed : IReleaseFeed
    {
        public const string PackageId = "doggy8088.Carnac";
        public const string Arguments = "show --id " + PackageId + " --versions --accept-source-agreements --disable-interactivity";

        readonly Func<string> runWinGetVersions;

        public WinGetReleaseFeed()
            : this(() => RunCommand("winget", Arguments, TimeSpan.FromSeconds(20)))
        {
        }

        public WinGetReleaseFeed(Func<string> runWinGetVersions)
        {
            if (runWinGetVersions == null)
                throw new ArgumentNullException("runWinGetVersions");

            this.runWinGetVersions = runWinGetVersions;
        }

        public string GetLatestReleaseJson()
        {
            var output = runWinGetVersions();
            var latest = ParseLatestVersion(output);
            if (latest == null)
                throw new SerializationException("WinGet did not return any valid version for " + PackageId + ".");

            var tag = "v" + latest;
            return string.Format(
                @"{{""tag_name"":""{0}"",""html_url"":""https://github.com/doggy8088/carnac/releases/tag/{0}"",""draft"":false,""prerelease"":false}}",
                tag);
        }

        public static ReleaseVersion ParseLatestVersion(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            ReleaseVersion highest = null;
            using (var reader = new StringReader(output))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || !char.IsDigit(trimmed[0]))
                        continue;

                    ReleaseVersion parsed;
                    if (ReleaseVersion.TryParse(trimmed, out parsed) && !parsed.IsPreRelease)
                    {
                        if (highest == null || parsed.IsNewerThan(highest))
                            highest = parsed;
                    }
                }
            }

            return highest;
        }

        internal static string RunCommand(string fileName, string arguments, TimeSpan timeout)
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                    throw new InvalidOperationException("Could not start " + fileName + ".");

                var outputTask = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException(fileName + " timed out.");
                }

                return outputTask.Result;
            }
        }
    }
}
