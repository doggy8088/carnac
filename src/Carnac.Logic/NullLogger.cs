using System;

namespace Carnac.Logic
{
    /// <summary>Logger that discards everything; used where no logger was supplied.</summary>
    public sealed class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new NullLogger();

        NullLogger()
        {
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
        }
    }
}
