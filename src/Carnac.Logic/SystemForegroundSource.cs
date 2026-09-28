using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Carnac.Logic
{
    /// <summary>Asks Windows for the window in the foreground and the process it belongs to.</summary>
    public sealed class SystemForegroundSource : IForegroundSource
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        public ForegroundProcess GetForegroundProcess()
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero)
                return null;

            uint processId;
            GetWindowThreadProcessId(window, out processId);
            if (processId == 0)
                return null;

            try
            {
                using (var process = Process.GetProcessById((int)processId))
                {
                    return new ForegroundProcess(process.Id, process.ProcessName);
                }
            }
            catch (ArgumentException)
            {
                // the process ended in the meantime
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (Win32Exception)
            {
                return null;
            }
        }
    }
}
