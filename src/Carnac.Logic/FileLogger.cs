using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace Carnac.Logic
{
    /// <summary>
    /// Writes a plain text log, one file per day: <c>carnac-YYYYMMDD.log</c>.
    /// <list type="bullet">
    /// <item>Thread-safe: entries can arrive from the keyboard hook, the UI thread and thread-pool threads.</item>
    /// <item>Size-capped: when a day's file would grow beyond the cap it becomes <c>carnac-YYYYMMDD.old.log</c> (replacing an older
    /// one) and a new file is started, so a day never takes more than about twice the cap. Files older than the retention are deleted.</item>
    /// <item>De-duplicating: the same entry repeated within the duplicate window is written once, followed by a
    /// "repeated n more times" line, so a failure that happens on every key press cannot flood the disk.</item>
    /// </list>
    /// Logging never throws. When the log cannot be written (folder not writable, disk full) the entry is dropped and
    /// <see cref="WriteFailures"/> counts it, because there is nowhere else to report it.
    /// </summary>
    public sealed class FileLogger : ILogger, IDisposable
    {
        public const long DefaultMaxFileBytes = 1024 * 1024;
        public const int DefaultKeepDays = 14;

        // a single huge exception dump must not use up the whole file
        const int MaxEntryLength = 20000;
        const string FilePrefix = "carnac-";
        const string FileExtension = ".log";

        public static readonly TimeSpan DefaultDuplicateWindow = TimeSpan.FromMinutes(1);

        readonly object sync = new object();
        readonly string directory;
        readonly Func<DateTime> clock;
        readonly long maxFileBytes;
        readonly TimeSpan duplicateWindow;
        readonly int keepDays;
        string lastKey;
        DateTime lastWrittenAt;
        int suppressedCount;
        DateTime lastCleanupDay = DateTime.MinValue;
        int writeFailures;

        /// <summary>The folder Carnac logs to: <c>%APPDATA%\Carnac\logs</c>.</summary>
        public static string DefaultDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Carnac", "logs"); }
        }

        public FileLogger(string directory)
            : this(directory, () => DateTime.Now, DefaultMaxFileBytes, DefaultDuplicateWindow, DefaultKeepDays)
        {
        }

        /// <param name="directory">Folder for the log files; created on the first write.</param>
        /// <param name="clock">Source of the current local time.</param>
        /// <param name="maxFileBytes">Size at which the day's file is rotated.</param>
        /// <param name="duplicateWindow">How long an identical entry is suppressed after it was written.</param>
        /// <param name="keepDays">Log files that were last written to longer ago than this are deleted.</param>
        public FileLogger(string directory, Func<DateTime> clock, long maxFileBytes, TimeSpan duplicateWindow, int keepDays)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentException("A log folder is required.", "directory");
            if (clock == null)
                throw new ArgumentNullException("clock");
            if (maxFileBytes <= 0)
                throw new ArgumentOutOfRangeException("maxFileBytes");
            if (keepDays < 1)
                throw new ArgumentOutOfRangeException("keepDays");

            this.directory = directory;
            this.clock = clock;
            this.maxFileBytes = maxFileBytes;
            this.duplicateWindow = duplicateWindow;
            this.keepDays = keepDays;
        }

        public string Directory
        {
            get { return directory; }
        }

        /// <summary>Number of entries that could not be written.</summary>
        public int WriteFailures
        {
            get { return System.Threading.Volatile.Read(ref writeFailures); }
        }

        public string GetLogFilePath(DateTime day)
        {
            return Path.Combine(directory, FilePrefix + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + FileExtension);
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
            try
            {
                lock (sync)
                {
                    var now = clock();
                    var key = BuildKey(level, message, exception);

                    if (lastKey != null && string.Equals(key, lastKey, StringComparison.Ordinal) && now - lastWrittenAt < duplicateWindow)
                    {
                        suppressedCount++;
                        return;
                    }

                    WriteSuppressedSummary(now);
                    Append(now, Format(now, level, message, exception));
                    lastKey = key;
                    lastWrittenAt = now;
                }
            }
            catch (Exception ex)
            {
                // the clock or the formatting failed: a logger has to survive anything
                RecordFailure(ex);
            }
        }

        /// <summary>Writes the "repeated n more times" line that is still pending.</summary>
        public void Flush()
        {
            try
            {
                lock (sync)
                {
                    WriteSuppressedSummary(clock());
                    lastKey = null;
                }
            }
            catch (Exception ex)
            {
                RecordFailure(ex);
            }
        }

        public void Dispose()
        {
            Flush();
        }

        void WriteSuppressedSummary(DateTime now)
        {
            if (suppressedCount == 0)
                return;

            var text = string.Format(CultureInfo.InvariantCulture, "{0} [{1}] The previous entry was repeated {2} more time(s).{3}",
                Timestamp(now), LogLevel.Info, suppressedCount, Environment.NewLine);
            suppressedCount = 0;
            Append(now, text);
        }

        void Append(DateTime now, string text)
        {
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                var path = GetLogFilePath(now);
                var file = new FileInfo(path);
                if (file.Exists && file.Length + Encoding.UTF8.GetByteCount(text) > maxFileBytes)
                    Rotate(path);

                File.AppendAllText(path, text, Encoding.UTF8);
                CleanUpOncePerDay(now);
            }
            catch (IOException ex)
            {
                RecordFailure(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                RecordFailure(ex);
            }
            catch (SecurityException ex)
            {
                RecordFailure(ex);
            }
            catch (NotSupportedException ex)
            {
                RecordFailure(ex);
            }
            catch (ArgumentException ex)
            {
                RecordFailure(ex);
            }
        }

        static void Rotate(string path)
        {
            var rotated = Path.ChangeExtension(path, null) + ".old" + FileExtension;
            if (File.Exists(rotated))
                File.Delete(rotated);
            File.Move(path, rotated);
        }

        void CleanUpOncePerDay(DateTime now)
        {
            if (lastCleanupDay == now.Date)
                return;

            lastCleanupDay = now.Date;
            var oldest = now.Date.AddDays(-keepDays);
            foreach (var file in System.IO.Directory.GetFiles(directory, FilePrefix + "*" + FileExtension))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < oldest)
                        File.Delete(file);
                }
                catch (IOException ex)
                {
                    // a file that is open in an editor is simply deleted on a later day
                    RecordFailure(ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    RecordFailure(ex);
                }
            }
        }

        static string BuildKey(LogLevel level, string message, Exception exception)
        {
            return level + "|" + message + "|" + (exception == null ? string.Empty : exception.GetType().FullName + ": " + exception.Message);
        }

        static string Timestamp(DateTime time)
        {
            return time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        static string Format(DateTime now, LogLevel level, string message, Exception exception)
        {
            var text = new StringBuilder();
            text.Append(Timestamp(now)).Append(" [").Append(level).Append("] ").Append(message);
            if (exception != null)
                text.AppendLine().Append(Describe(exception));

            if (text.Length > MaxEntryLength)
            {
                text.Length = MaxEntryLength;
                text.Append("... (truncated)");
            }

            return text.AppendLine().ToString();
        }

        static string Describe(Exception exception)
        {
            try
            {
                return exception.ToString();
            }
            catch (Exception)
            {
                // ToString() of a custom exception may itself throw
                return exception.GetType().FullName;
            }
        }

        void RecordFailure(Exception exception)
        {
            System.Threading.Interlocked.Increment(ref writeFailures);
            Debug.WriteLine("Carnac could not write its log: " + exception.Message);
        }
    }
}
