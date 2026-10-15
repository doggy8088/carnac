using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Carnac.Logic
{
    public interface IInstallEnvironment
    {
        string AppDirectory { get; }
        string RegistryInstallLocation { get; }
        string RegistryInstallSource { get; }
        bool HasUninstallerInAppDirectory { get; }
        bool HasChocolateyPackage { get; }
        bool HasWinGetInstallerLog { get; }
    }

    public sealed class SystemInstallEnvironment : IInstallEnvironment
    {
        public const string UninstallRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{DB5C24A2-1558-4272-8C3C-676D68A1E405}_is1";

        readonly string appDirectory;

        public SystemInstallEnvironment()
            : this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        public SystemInstallEnvironment(string appDirectory)
        {
            this.appDirectory = appDirectory ?? string.Empty;
        }

        public string AppDirectory
        {
            get { return appDirectory; }
        }

        public string RegistryInstallLocation
        {
            get { return ReadUninstallRegistryValue("InstallLocation"); }
        }

        public string RegistryInstallSource
        {
            get { return ReadUninstallRegistryValue("InstallSource"); }
        }

        public bool HasUninstallerInAppDirectory
        {
            get
            {
                try
                {
                    return !string.IsNullOrWhiteSpace(appDirectory)
                        && Directory.Exists(appDirectory)
                        && Directory.GetFiles(appDirectory, "unins*.exe").Length > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool HasChocolateyPackage
        {
            get
            {
                try
                {
                    var chocoInstall = Environment.GetEnvironmentVariable("ChocolateyInstall");
                    if (string.IsNullOrWhiteSpace(chocoInstall))
                    {
                        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        if (!string.IsNullOrWhiteSpace(programData))
                            chocoInstall = Path.Combine(programData, "chocolatey");
                    }

                    if (string.IsNullOrWhiteSpace(chocoInstall))
                        return false;

                    var carnacLibDir = Path.Combine(chocoInstall, "lib", "carnac");
                    return Directory.Exists(carnacLibDir);
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool HasWinGetInstallerLog
        {
            get
            {
                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (string.IsNullOrWhiteSpace(localAppData))
                        return false;

                    var diagDir = Path.Combine(
                        localAppData,
                        "Packages",
                        "Microsoft.DesktopAppInstaller_8wekyb3d8bbwe",
                        "LocalState",
                        "DiagOutputDir");

                    if (!Directory.Exists(diagDir))
                        return false;

                    return Directory.GetFiles(diagDir, "*doggy8088.Carnac*").Any();
                }
                catch
                {
                    return false;
                }
            }
        }

        static string ReadUninstallRegistryValue(string valueName)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(UninstallRegistrySubKey))
                {
                    if (key == null)
                        return null;

                    return key.GetValue(valueName) as string;
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public static class InstallMethodDetector
    {
        public static InstallMethod Detect()
        {
            return Detect(new SystemInstallEnvironment());
        }

        public static InstallMethod Detect(IInstallEnvironment env)
        {
            if (env == null)
                throw new ArgumentNullException("env");

            var isInstalledAppDir = env.HasUninstallerInAppDirectory || PathsEqual(env.AppDirectory, env.RegistryInstallLocation);
            if (!isInstalledAppDir)
                return InstallMethod.Portable;

            var source = (env.RegistryInstallSource ?? string.Empty).Trim();
            if (string.Equals(source, "chocolatey", StringComparison.OrdinalIgnoreCase))
                return InstallMethod.Chocolatey;
            if (string.Equals(source, "winget", StringComparison.OrdinalIgnoreCase))
                return InstallMethod.WinGet;
            if (string.Equals(source, "innosetup", StringComparison.OrdinalIgnoreCase))
                return InstallMethod.InnoSetup;

            if (env.HasChocolateyPackage)
                return InstallMethod.Chocolatey;

            if (env.HasWinGetInstallerLog)
                return InstallMethod.WinGet;

            return InstallMethod.InnoSetup;
        }

        public static string FormatDisplayName(InstallMethod method)
        {
            switch (method)
            {
                case InstallMethod.WinGet:
                    return "WinGet (doggy8088.Carnac)";
                case InstallMethod.Chocolatey:
                    return "Chocolatey (carnac)";
                case InstallMethod.Portable:
                    return "Portable (.zip)";
                default:
                    return "Inno Setup Installer";
            }
        }

        static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
                return false;

            try
            {
                var normalizedFirst = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var normalizedSecond = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
