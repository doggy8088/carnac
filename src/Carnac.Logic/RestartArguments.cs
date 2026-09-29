using System;

namespace Carnac.Logic
{
    /// <summary>
    /// The command line with which Carnac starts its elevated replacement. The new instance has to wait until the old one is gone,
    /// otherwise the single-instance mutex of the old one would make the new one quit right away.
    /// </summary>
    public static class RestartArguments
    {
        public const string WaitForProcessOption = "--wait-for-process";

        public static string Format(int processIdToWaitFor)
        {
            return WaitForProcessOption + " " + processIdToWaitFor;
        }

        /// <summary>Finds "--wait-for-process &lt;id&gt;" in the arguments; other arguments are ignored.</summary>
        public static bool TryGetProcessIdToWaitFor(string[] args, out int processId)
        {
            processId = 0;
            if (args == null)
                return false;

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], WaitForProcessOption, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(args[i + 1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out processId)
                    && processId > 0)
                {
                    return true;
                }
            }

            processId = 0;
            return false;
        }
    }
}
