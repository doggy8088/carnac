using System;

namespace Carnac.Logic
{
    /// <summary>
    /// The text of the tray balloon that announces a newer release. It does not assume one install channel (WinGet, Chocolatey,
    /// portable zip): the release page is always named and is what a click opens.
    /// </summary>
    public sealed class UpdateNotice
    {
        public const string ReleasesPageUrl = "https://github.com/doggy8088/carnac/releases/latest";

        // limits of a tray balloon: 63 characters for the title, 255 for the text
        const int MaxTitleLength = 63;
        const int MaxTextLength = 255;
        const string TrustedHost = "github.com";
        const string TrustedPathPrefix = "/doggy8088/carnac/";

        UpdateNotice(string title, string text, string url)
        {
            Title = title;
            Text = text;
            Url = url;
        }

        public string Title { get; private set; }

        public string Text { get; private set; }

        /// <summary>The page a click on the balloon opens.</summary>
        public string Url { get; private set; }

        public static UpdateNotice Create(ReleaseVersion currentVersion, ReleaseVersion latestVersion, LatestRelease release)
        {
            if (currentVersion == null)
                throw new ArgumentNullException("currentVersion");
            if (latestVersion == null)
                throw new ArgumentNullException("latestVersion");
            if (release == null)
                throw new ArgumentNullException("release");

            var url = TrustedReleasePage(release.HtmlUrl);
            var title = Truncate("Carnac " + latestVersion + " is available", MaxTitleLength);

            var text = string.Format("You have {0}. Click to open the release page ({1}) or upgrade with your package manager, for example: winget upgrade doggy8088.Carnac",
                currentVersion, url);
            if (text.Length > MaxTextLength)
                text = string.Format("You have {0}. Click to open the release page: {1}", currentVersion, url);

            return new UpdateNotice(title, Truncate(text, MaxTextLength), url);
        }

        /// <summary>
        /// The release page from the answer of the API, but only when it really is a page of this repository on github.com.
        /// The balloon opens it in the browser, so an unexpected address is replaced by the general releases page.
        /// </summary>
        public static string TrustedReleasePage(string htmlUrl)
        {
            Uri uri;
            if (Uri.TryCreate(htmlUrl, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && string.Equals(uri.Host, TrustedHost, StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith(TrustedPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return uri.AbsoluteUri;
            }

            return ReleasesPageUrl;
        }

        static string Truncate(string text, int maxLength)
        {
            return text.Length <= maxLength ? text : text.Substring(0, maxLength);
        }
    }
}
