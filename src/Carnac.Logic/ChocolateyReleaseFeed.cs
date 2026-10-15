using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;

namespace Carnac.Logic
{
    /// <summary>
    /// Queries Chocolatey for the latest approved version of <c>carnac</c>, so Chocolatey users are only notified
    /// once the package version has passed Chocolatey moderation.
    /// </summary>
    public sealed class ChocolateyReleaseFeed : IReleaseFeed
    {
        public const string PackageId = "carnac";
        public const string ChocoArguments = "search " + PackageId + " --exact --limit-output";
        public const string ODataLatestUrl = "https://community.chocolatey.org/api/v2/Packages()?$filter=(Id%20eq%20'carnac')%20and%20IsLatestVersion";

        readonly Func<string> runChocoSearch;
        readonly Func<string> fetchODataXml;

        public ChocolateyReleaseFeed(string userAgent)
            : this(
                () => WinGetReleaseFeed.RunCommand("choco", ChocoArguments, TimeSpan.FromSeconds(20)),
                () => FetchUrl(new Uri(ODataLatestUrl), userAgent, TimeSpan.FromSeconds(15)))
        {
        }

        public ChocolateyReleaseFeed(Func<string> runChocoSearch, Func<string> fetchODataXml)
        {
            if (runChocoSearch == null)
                throw new ArgumentNullException("runChocoSearch");
            if (fetchODataXml == null)
                throw new ArgumentNullException("fetchODataXml");

            this.runChocoSearch = runChocoSearch;
            this.fetchODataXml = fetchODataXml;
        }

        public string GetLatestReleaseJson()
        {
            ReleaseVersion latest = null;
            try
            {
                latest = ParseChocoLimitOutput(runChocoSearch());
            }
            catch
            {
                // Fallback to Chocolatey OData HTTP endpoint if choco CLI is unavailable or fails
            }

            if (latest == null)
            {
                latest = ParseODataVersion(fetchODataXml());
            }

            if (latest == null)
                throw new SerializationException("Chocolatey did not return any valid version for " + PackageId + ".");

            var tag = "v" + latest;
            return string.Format(
                @"{{""tag_name"":""{0}"",""html_url"":""https://github.com/doggy8088/carnac/releases/tag/{0}"",""draft"":false,""prerelease"":false}}",
                tag);
        }

        public static ReleaseVersion ParseChocoLimitOutput(string output)
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
                    var parts = trimmed.Split('|');
                    if (parts.Length < 2 || !string.Equals(parts[0].Trim(), PackageId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    ReleaseVersion parsed;
                    if (ReleaseVersion.TryParse(parts[1].Trim(), out parsed) && !parsed.IsPreRelease)
                    {
                        if (highest == null || parsed.IsNewerThan(highest))
                            highest = parsed;
                    }
                }
            }

            return highest;
        }

        public static ReleaseVersion ParseODataVersion(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                return null;

            try
            {
                var doc = XDocument.Parse(xml);
                XNamespace d = "http://schemas.microsoft.com/ado/2007/08/dataservices";
                ReleaseVersion highest = null;
                foreach (var versionElement in doc.Descendants(d + "Version"))
                {
                    ReleaseVersion parsed;
                    if (ReleaseVersion.TryParse(versionElement.Value.Trim(), out parsed) && !parsed.IsPreRelease)
                    {
                        if (highest == null || parsed.IsNewerThan(highest))
                            highest = parsed;
                    }
                }
                return highest;
            }
            catch
            {
                return null;
            }
        }

        static string FetchUrl(Uri url, string userAgent, TimeSpan timeout)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "Carnac" : userAgent;
            request.Timeout = (int)timeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)timeout.TotalMilliseconds;

            var proxy = WebRequest.GetSystemWebProxy();
            proxy.Credentials = CredentialCache.DefaultCredentials;
            request.Proxy = proxy;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
