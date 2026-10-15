using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Carnac.Logic
{
    [DataContract]
    public sealed class ReleaseAsset
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; }

        [DataMember(Name = "digest")]
        public string Digest { get; set; }
    }

    /// <summary>The parts of the GitHub "latest release" API answer that Carnac needs.</summary>
    [DataContract]
    public sealed class LatestRelease
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        [DataMember(Name = "prerelease")]
        public bool IsPreRelease { get; set; }

        [DataMember(Name = "draft")]
        public bool IsDraft { get; set; }

        [DataMember(Name = "assets")]
        public List<ReleaseAsset> Assets { get; set; }

        public ReleaseAsset FindAsset(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Assets == null)
                return null;

            return Assets.FirstOrDefault(a => a != null && string.Equals(a.Name, fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Reads the JSON of <c>GET /repos/{owner}/{repo}/releases/latest</c>; other members are ignored.</summary>
        /// <exception cref="SerializationException">The text is not the JSON of a release.</exception>
        public static LatestRelease Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new SerializationException("The release information is empty.");

            LatestRelease release;
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                release = new DataContractJsonSerializer(typeof(LatestRelease)).ReadObject(stream) as LatestRelease;
            }

            if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                throw new SerializationException("The release information has no tag_name.");

            return release;
        }
    }
}
