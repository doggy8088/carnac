using System;

namespace Carnac.Logic
{
    public enum LogLevel
    {
        Info,
        Warning,
        /// <summary>An unexpected failure. Repeated errors make Carnac show a notice in the tray.</summary>
        Error
    }

    /// <summary>
    /// Where Carnac writes down what went wrong. Implementations must never throw and must be safe to call from any thread
    /// (for example the keyboard hook's).
    /// </summary>
    public interface ILogger
    {
        void Log(LogLevel level, string message, Exception exception);
    }

    public static class LoggerExtensions
    {
        public static void Info(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Info, message, null);
        }

        public static void Warn(this ILogger logger, string message)
        {
            logger.Log(LogLevel.Warning, message, null);
        }

        public static void Warn(this ILogger logger, string message, Exception exception)
        {
            logger.Log(LogLevel.Warning, message, exception);
        }

        public static void Error(this ILogger logger, string message, Exception exception)
        {
            logger.Log(LogLevel.Error, message, exception);
        }
    }
}
