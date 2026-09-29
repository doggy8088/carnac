using System.Diagnostics;
using System.IO;
using Carnac.Logic;

namespace Carnac
{
    /// <summary>Starts a program through the Windows shell with the "runas" verb, which shows the UAC prompt.</summary>
    public sealed class ShellElevatedLauncher : IElevatedLauncher
    {
        /// <exception cref="System.ComponentModel.Win32Exception">The user declined the UAC prompt (error 1223) or the program could not be started.</exception>
        public void Launch(string executablePath, string arguments)
        {
            var startInfo = new ProcessStartInfo(executablePath, arguments)
            {
                Verb = "runas",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath)
            };

            // the elevated process is not needed here, only that it started
            using (Process.Start(startInfo))
            {
            }
        }
    }
}
