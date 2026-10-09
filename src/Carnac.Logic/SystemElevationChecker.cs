using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Carnac.Logic
{
    /// <summary>
    /// Asks Windows whether a process runs elevated: the elevation flag of its access token. A normal (limited) process cannot open
    /// the process or the token of an elevated one, so "access denied" is taken as "runs with more rights than we do", which is
    /// exactly the case that matters here.
    /// </summary>
    public sealed class SystemElevationChecker : IElevationChecker
    {
        const uint ProcessQueryLimitedInformation = 0x1000;
        const uint TokenQuery = 0x8;
        const int TokenElevation = 20;
        const int ErrorAccessDenied = 5;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        // TOKEN_ELEVATION is a struct with a single DWORD (TokenIsElevated)
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, out int tokenInformation, int tokenInformationLength, out int returnLength);

        public bool IsCurrentProcessElevated
        {
            get
            {
                using (var current = Process.GetCurrentProcess())
                {
                    return IsTokenElevated(current.Handle) ?? false;
                }
            }
        }

        public bool? IsProcessElevated(int processId)
        {
            var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
                return Marshal.GetLastWin32Error() == ErrorAccessDenied ? (bool?)true : null;

            try
            {
                return IsTokenElevated(process);
            }
            finally
            {
                CloseHandle(process);
            }
        }

        static bool? IsTokenElevated(IntPtr process)
        {
            IntPtr token;
            if (!OpenProcessToken(process, TokenQuery, out token))
                return Marshal.GetLastWin32Error() == ErrorAccessDenied ? (bool?)true : null;

            try
            {
                int isElevated;
                int returnLength;
                if (!GetTokenInformation(token, TokenElevation, out isElevated, sizeof(int), out returnLength))
                    return null;

                return isElevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }
}
