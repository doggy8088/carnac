using System;
using System.IO;
using System.Net;
using System.Text;

namespace Carnac.Logic
{
    /// <summary>
    /// Asks the GitHub API for the latest release of Carnac. Only used when the user switched on "Check for updates on startup".
    /// Nothing is downloaded except the small JSON answer (at most <see cref="MaxResponseBytes"/>).
    /// </summary>
    public sealed class GitHubReleaseFeed : IReleaseFeed
    {
        public const string LatestReleaseUrl = "https://api.github.com/repos/doggy8088/carnac/releases/latest";
        public const int MaxResponseBytes = 1024 * 1024;

        readonly Uri url;
        readonly string userAgent;
        readonly int timeoutMilliseconds;

        /// <param name="userAgent">GitHub rejects requests without one, for example "Carnac/2.3.0".</param>
        public GitHubReleaseFeed(string userAgent)
            : this(new Uri(LatestReleaseUrl), userAgent, TimeSpan.FromSeconds(15))
        {
        }

        public GitHubReleaseFeed(Uri url, string userAgent, TimeSpan timeout)
        {
            if (url == null)
                throw new ArgumentNullException("url");
            if (string.IsNullOrWhiteSpace(userAgent))
                throw new ArgumentException("A user agent is required.", "userAgent");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException("timeout");

            this.url = url;
            this.userAgent = userAgent;
            timeoutMilliseconds = (int)Math.Min(timeout.TotalMilliseconds, int.MaxValue);
        }

        /// <exception cref="WebException">No connection, a timeout or an HTTP error status such as 404 or 403 (rate limit).</exception>
        /// <exception cref="InvalidDataException">The answer is larger than <see cref="MaxResponseBytes"/>.</exception>
        public string GetLatestReleaseJson()
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = userAgent;
            request.Accept = "application/vnd.github+json";
            request.Timeout = timeoutMilliseconds;
            request.ReadWriteTimeout = timeoutMilliseconds;

            // the system proxy (with the current user's credentials) so that this also works behind a company proxy
            var proxy = WebRequest.GetSystemWebProxy();
            proxy.Credentials = CredentialCache.DefaultCredentials;
            request.Proxy = proxy;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[4096];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    if (buffer.Length + read > MaxResponseBytes)
                        throw new InvalidDataException("The answer is larger than " + MaxResponseBytes + " bytes.");

                    buffer.Write(chunk, 0, read);
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }
    }
}
