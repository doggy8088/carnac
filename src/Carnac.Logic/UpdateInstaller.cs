using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Carnac.Logic
{
    public static class ReleaseFeedFactory
    {
        public static IReleaseFeed Create(InstallMethod method, string userAgent)
        {
            switch (method)
            {
                case InstallMethod.WinGet:
                    return new WinGetReleaseFeed();
                case InstallMethod.Chocolatey:
                    return new ChocolateyReleaseFeed(userAgent);
                default:
                    return new GitHubReleaseFeed(userAgent);
            }
        }
    }

    public sealed class UpdateInstaller
    {
        public const string TrustedHost = "github.com";
        public const string TrustedDownloadPrefix = "/doggy8088/carnac/releases/download/";

        readonly Action<string, string> downloadFile;
        readonly Action<ProcessStartInfo> startProcess;
        readonly Action shutdownApplication;
        readonly string executablePath;
        readonly int currentProcessId;

        public UpdateInstaller(string executablePath, int currentProcessId, Action shutdownApplication)
            : this(executablePath, currentProcessId, shutdownApplication, DownloadFileOverHttps, StartSystemProcess)
        {
        }

        public UpdateInstaller(
            string executablePath,
            int currentProcessId,
            Action shutdownApplication,
            Action<string, string> downloadFile,
            Action<ProcessStartInfo> startProcess)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new ArgumentException("Executable path is required.", "executablePath");
            if (shutdownApplication == null)
                throw new ArgumentNullException("shutdownApplication");
            if (downloadFile == null)
                throw new ArgumentNullException("downloadFile");
            if (startProcess == null)
                throw new ArgumentNullException("startProcess");

            this.executablePath = executablePath;
            this.currentProcessId = currentProcessId;
            this.shutdownApplication = shutdownApplication;
            this.downloadFile = downloadFile;
            this.startProcess = startProcess;
        }

        public void ExecuteUpgrade(InstallMethod method, ReleaseVersion latestVersion, LatestRelease release)
        {
            if (latestVersion == null)
                throw new ArgumentNullException("latestVersion");

            switch (method)
            {
                case InstallMethod.WinGet:
                    StartWinGetUpgrade();
                    break;
                case InstallMethod.Chocolatey:
                    StartChocolateyUpgrade();
                    break;
                case InstallMethod.Portable:
                    StartPortableUpgrade(latestVersion, release);
                    break;
                default:
                    StartInnoSetupUpgrade(latestVersion, release);
                    break;
            }
        }

        void StartInnoSetupUpgrade(ReleaseVersion latestVersion, LatestRelease release)
        {
            var setupFileName = string.Format("Carnac-{0}-Setup.exe", latestVersion);
            var asset = release != null ? release.FindAsset(setupFileName) : null;
            var downloadUrl = ResolveTrustedAssetUrl(
                asset != null ? asset.BrowserDownloadUrl : null,
                string.Format("https://github.com/doggy8088/carnac/releases/download/v{0}/{1}", latestVersion, setupFileName));

            var tempSetupPath = Path.Combine(Path.GetTempPath(), setupFileName);
            downloadFile(downloadUrl, tempSetupPath);

            try
            {
                VerifySha256IfPresent(tempSetupPath, asset != null ? asset.Digest : null);
            }
            catch
            {
                TryDeleteFile(tempSetupPath);
                throw;
            }

            var script = string.Format(
                "Wait-Process -Id {0} -Timeout 15 -ErrorAction SilentlyContinue; " +
                "Start-Process -FilePath '{1}' -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /CLOSEAPPLICATIONS /INSTALLSOURCE=innosetup' -Wait; " +
                "Remove-Item -LiteralPath '{1}' -Force -ErrorAction SilentlyContinue; " +
                "if (Test-Path -LiteralPath '{2}') {{ Start-Process -FilePath '{2}' }}",
                currentProcessId,
                EscapePowerShellSingleQuoted(tempSetupPath),
                EscapePowerShellSingleQuoted(executablePath));

            startProcess(CreatePowerShellStartInfo(script, elevated: false, hidden: true));
            shutdownApplication();
        }

        void StartPortableUpgrade(ReleaseVersion latestVersion, LatestRelease release)
        {
            var zipFileName = string.Format("Carnac-{0}-portable.zip", latestVersion);
            var asset = release != null ? release.FindAsset(zipFileName) : null;
            var downloadUrl = ResolveTrustedAssetUrl(
                asset != null ? asset.BrowserDownloadUrl : null,
                string.Format("https://github.com/doggy8088/carnac/releases/download/v{0}/{1}", latestVersion, zipFileName));

            var stagingDir = Path.Combine(Path.GetTempPath(), string.Format("Carnac-Portable-Update-{0}-{1:N}", latestVersion, Guid.NewGuid()));
            var zipPath = Path.Combine(stagingDir, zipFileName);
            var extractedDir = Path.Combine(stagingDir, "extracted");

            Directory.CreateDirectory(stagingDir);
            try
            {
                downloadFile(downloadUrl, zipPath);
                VerifySha256IfPresent(zipPath, asset != null ? asset.Digest : null);
                ZipFile.ExtractToDirectory(zipPath, extractedDir);

                if (!File.Exists(Path.Combine(extractedDir, "Carnac.exe")))
                    throw new InvalidDataException("The portable archive does not contain Carnac.exe.");
            }
            catch
            {
                TryDeleteDirectory(stagingDir);
                throw;
            }

            var targetAppDir = Path.GetDirectoryName(executablePath);
            var script = string.Format(
                "Wait-Process -Id {0} -Timeout 15 -ErrorAction SilentlyContinue; " +
                "Copy-Item -Path '{1}\\*' -Destination '{2}' -Recurse -Force; " +
                "if (Test-Path -LiteralPath '{3}') {{ Start-Process -FilePath '{3}' }}; " +
                "Remove-Item -LiteralPath '{4}' -Recurse -Force -ErrorAction SilentlyContinue",
                currentProcessId,
                EscapePowerShellSingleQuoted(extractedDir),
                EscapePowerShellSingleQuoted(targetAppDir),
                EscapePowerShellSingleQuoted(executablePath),
                EscapePowerShellSingleQuoted(stagingDir));

            startProcess(CreatePowerShellStartInfo(script, elevated: false, hidden: true));
            shutdownApplication();
        }

        void StartWinGetUpgrade()
        {
            var script = string.Format(
                "Wait-Process -Id {0} -Timeout 15 -ErrorAction SilentlyContinue; " +
                "winget upgrade --id doggy8088.Carnac --exact --accept-source-agreements --accept-package-agreements --disable-interactivity; " +
                "if (Test-Path -LiteralPath '{1}') {{ Start-Process -FilePath '{1}' }}",
                currentProcessId,
                EscapePowerShellSingleQuoted(executablePath));

            startProcess(CreatePowerShellStartInfo(script, elevated: false, hidden: false));
            shutdownApplication();
        }

        void StartChocolateyUpgrade()
        {
            var script = string.Format(
                "Wait-Process -Id {0} -Timeout 15 -ErrorAction SilentlyContinue; " +
                "choco upgrade carnac -y; " +
                "if (Test-Path -LiteralPath '{1}') {{ Start-Process -FilePath '{1}' }}",
                currentProcessId,
                EscapePowerShellSingleQuoted(executablePath));

            startProcess(CreatePowerShellStartInfo(script, elevated: true, hidden: false));
            shutdownApplication();
        }

        public static string ResolveTrustedAssetUrl(string candidateUrl, string fallbackUrl)
        {
            Uri uri;
            if (!string.IsNullOrWhiteSpace(candidateUrl)
                && Uri.TryCreate(candidateUrl, UriKind.Absolute, out uri)
                && uri.Scheme == Uri.UriSchemeHttps
                && string.Equals(uri.Host, TrustedHost, StringComparison.OrdinalIgnoreCase)
                && uri.AbsolutePath.StartsWith(TrustedDownloadPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return uri.AbsoluteUri;
            }

            return fallbackUrl;
        }

        public static void VerifySha256IfPresent(string filePath, string digest)
        {
            if (string.IsNullOrWhiteSpace(digest))
                return;

            var expectedHex = digest.Trim();
            if (expectedHex.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                expectedHex = expectedHex.Substring("sha256:".Length).Trim();

            if (expectedHex.Length == 0)
                return;

            string actualHex;
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha256.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                actualHex = sb.ToString();
            }

            if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(string.Format("SHA-256 mismatch for {0}: expected {1}, got {2}.", Path.GetFileName(filePath), expectedHex, actualHex));
        }

        static ProcessStartInfo CreatePowerShellStartInfo(string script, bool elevated, bool hidden)
        {
            var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var arguments = string.Format(
                "-NoProfile -NonInteractive {0}-ExecutionPolicy Bypass -EncodedCommand {1}",
                hidden ? "-WindowStyle Hidden " : string.Empty,
                encodedCommand);

            var psi = new ProcessStartInfo("powershell.exe", arguments)
            {
                UseShellExecute = elevated,
                CreateNoWindow = hidden && !elevated,
                WindowStyle = hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            if (elevated)
                psi.Verb = "runas";

            return psi;
        }

        static string EscapePowerShellSingleQuoted(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        static void DownloadFileOverHttps(string url, string destinationPath)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "Carnac-Updater";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;

            var proxy = WebRequest.GetSystemWebProxy();
            proxy.Credentials = CredentialCache.DefaultCredentials;
            request.Proxy = proxy;

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                responseStream.CopyTo(fileStream);
            }
        }

        static void StartSystemProcess(ProcessStartInfo psi)
        {
            using (Process.Start(psi))
            {
            }
        }

        static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
