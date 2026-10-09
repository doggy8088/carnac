using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Carnac.Logic
{
    /// <summary>
    /// Finds the process that owns the foreground window. Process objects are cached by process id, because looking one up
    /// enumerates all processes. A cached process is dropped when it has exited (a process id can be reused by another
    /// program), after <see cref="MaxCacheAge"/> (exit cannot be checked for elevated processes), and the cache never grows
    /// beyond <see cref="CacheCapacity"/> entries. The returned process is only valid until the cache evicts it, so callers
    /// must not keep it.
    /// </summary>
    public static class AssociatedProcessUtilities
    {
        public const int CacheCapacity = 32;
        public static readonly TimeSpan MaxCacheAge = TimeSpan.FromSeconds(30);

        private static readonly BoundedCache<int, Process> processes = new BoundedCache<int, Process>(
            CacheCapacity, Process.GetProcessById, HasExited, process => process.Dispose(), MaxCacheAge);

        [DllImport("User32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static Process GetAssociatedProcess()
        {
            uint processId;
            GetWindowThreadProcessId(GetForegroundWindow(), out processId);
            try
            {
                return processes.Get(Convert.ToInt32(processId));
            }
            catch (ArgumentException)
            {
                // the process is not running (any more)
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>True when the process is known to have exited; false when it runs or when that cannot be checked.</summary>
        public static bool HasExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                // access denied: processes that run elevated cannot be queried from a normal process
                return false;
            }
        }
    }
}
