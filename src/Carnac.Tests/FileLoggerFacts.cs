using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    /// <summary>All facts log into a temporary folder that is deleted afterwards, never into %APPDATA%.</summary>
    public class FileLoggerFacts : IDisposable
    {
        readonly string directory = Path.Combine(Path.GetTempPath(), "carnac-tests-" + Guid.NewGuid().ToString("N"));
        DateTime now = new DateTime(2026, 3, 14, 10, 30, 0);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                // a leftover temp folder is harmless
            }
        }

        FileLogger CreateLogger(long maxFileBytes = FileLogger.DefaultMaxFileBytes, int keepDays = FileLogger.DefaultKeepDays)
        {
            return new FileLogger(directory, () => now, maxFileBytes, TimeSpan.FromMinutes(1), keepDays);
        }

        string TodaysFile()
        {
            return Path.Combine(directory, "carnac-20260314.log");
        }

        string ReadTodaysLog()
        {
            return File.ReadAllText(TodaysFile());
        }

        static Exception Thrown(string message)
        {
            try
            {
                throw new InvalidOperationException(message);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }
        }

        [Fact]
        public void writes_the_level_and_message_with_a_timestamp()
        {
            var sut = CreateLogger();

            sut.Info("Carnac started");

            Assert.Equal("2026-03-14 10:30:00.000 [Info] Carnac started" + Environment.NewLine, ReadTodaysLog());
        }

        [Fact]
        public void writes_the_type_message_and_stack_trace_of_an_exception()
        {
            var sut = CreateLogger();

            sut.Error("Something failed", Thrown("the reason"));

            var log = ReadTodaysLog();
            Assert.Contains("[Error] Something failed", log);
            Assert.Contains("System.InvalidOperationException: the reason", log);
            Assert.Contains("Thrown", log);   // this method is in the stack trace
        }

        [Fact]
        public void writes_warnings_with_their_level()
        {
            var sut = CreateLogger();

            sut.Warn("Careful");

            Assert.Contains("[Warning] Careful", ReadTodaysLog());
        }

        [Fact]
        public void creates_the_log_folder_on_the_first_write_and_not_before()
        {
            var sut = CreateLogger();
            Assert.False(Directory.Exists(directory));

            sut.Info("first");

            Assert.True(File.Exists(TodaysFile()));
        }

        [Fact]
        public void uses_one_file_per_day_named_after_the_date()
        {
            var sut = CreateLogger();
            sut.Info("today");

            now = now.AddDays(1);
            sut.Info("tomorrow");

            Assert.Equal(new[] { "carnac-20260314.log", "carnac-20260315.log" },
                Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(name => name).ToArray());
            Assert.Contains("tomorrow", File.ReadAllText(Path.Combine(directory, "carnac-20260315.log")));
        }

        [Fact]
        public void appends_to_the_existing_file_of_the_day()
        {
            CreateLogger().Info("from the first run");
            CreateLogger().Info("from the second run");

            var log = ReadTodaysLog();
            Assert.Contains("from the first run", log);
            Assert.Contains("from the second run", log);
        }

        [Fact]
        public void the_default_folder_is_carnac_logs_in_appdata()
        {
            var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Carnac", "logs");

            Assert.Equal(expected, FileLogger.DefaultDirectory);
            Assert.Equal(expected, new FileLogger(FileLogger.DefaultDirectory).Directory);
        }

        // ---- de-duplication ----

        [Fact]
        public void writes_an_entry_that_repeats_within_the_window_only_once()
        {
            var sut = CreateLogger();

            for (var i = 0; i < 50; i++)
                sut.Error("Same problem", Thrown("same reason"));

            Assert.Equal(1, CountOccurrences(ReadTodaysLog(), "Same problem"));
        }

        [Fact]
        public void reports_how_often_an_entry_was_repeated_when_something_else_is_logged()
        {
            var sut = CreateLogger();
            for (var i = 0; i < 5; i++)
                sut.Warn("Same problem");

            sut.Warn("Another problem");

            var log = ReadTodaysLog();
            Assert.Contains("repeated 4 more time(s)", log);
            Assert.True(log.IndexOf("repeated 4", StringComparison.Ordinal) < log.IndexOf("Another problem", StringComparison.Ordinal));
        }

        [Fact]
        public void writes_the_entry_again_after_the_window_has_passed()
        {
            var sut = CreateLogger();
            sut.Warn("Same problem");
            sut.Warn("Same problem");

            now = now.AddMinutes(2);
            sut.Warn("Same problem");

            var log = ReadTodaysLog();
            Assert.Equal(2, CountOccurrences(log, "Same problem"));
            Assert.Contains("repeated 1 more time(s)", log);
        }

        [Fact]
        public void does_not_merge_entries_that_differ_in_level_message_or_exception()
        {
            var sut = CreateLogger();

            sut.Info("problem");
            sut.Warn("problem");
            sut.Warn("other problem");
            sut.Error("other problem", Thrown("a"));
            sut.Error("other problem", Thrown("b"));

            var log = ReadTodaysLog();
            Assert.Equal(1, CountOccurrences(log, "[Info] problem"));
            Assert.Equal(1, CountOccurrences(log, "[Warning] problem"));
            Assert.Equal(1, CountOccurrences(log, "[Warning] other problem"));
            Assert.Equal(2, CountOccurrences(log, "[Error] other problem"));
            Assert.DoesNotContain("repeated", log);
        }

        [Fact]
        public void flush_writes_the_pending_repeat_count()
        {
            var sut = CreateLogger();
            sut.Warn("Same problem");
            sut.Warn("Same problem");
            sut.Warn("Same problem");

            sut.Flush();

            Assert.Contains("repeated 2 more time(s)", ReadTodaysLog());
        }

        [Fact]
        public void flush_without_pending_repeats_writes_nothing()
        {
            var sut = CreateLogger();
            sut.Warn("once");
            var before = ReadTodaysLog();

            sut.Flush();

            Assert.Equal(before, ReadTodaysLog());
        }

        [Fact]
        public void dispose_writes_the_pending_repeat_count()
        {
            var sut = CreateLogger();
            sut.Warn("twice");
            sut.Warn("twice");

            sut.Dispose();

            Assert.Contains("repeated 1 more time(s)", ReadTodaysLog());
        }

        // ---- size cap and retention ----

        [Fact]
        public void keeps_the_files_of_a_day_within_the_size_cap()
        {
            const int cap = 2000;
            var sut = CreateLogger(cap);

            for (var i = 0; i < 300; i++)
                sut.Info("entry number " + i + " " + new string('x', 40));

            var files = Directory.GetFiles(directory).ToArray();
            Assert.Equal(new[] { "carnac-20260314.log", "carnac-20260314.old.log" },
                files.Select(Path.GetFileName).OrderBy(name => name).ToArray());
            foreach (var file in files)
                Assert.True(new FileInfo(file).Length <= cap, file + " is " + new FileInfo(file).Length + " bytes");
        }

        [Fact]
        public void the_newest_entries_survive_a_rotation()
        {
            var sut = CreateLogger(2000);

            for (var i = 0; i < 300; i++)
                sut.Info("entry number " + i + " " + new string('x', 40));

            Assert.Contains("entry number 299 ", ReadTodaysLog());
        }

        [Fact]
        public void truncates_a_huge_entry()
        {
            var sut = CreateLogger();

            sut.Error("huge", new InvalidOperationException(new string('y', 100000)));

            var log = ReadTodaysLog();
            Assert.True(log.Length < 30000);
            Assert.Contains("(truncated)", log);
        }

        [Fact]
        public void deletes_log_files_older_than_the_retention_but_nothing_else()
        {
            Directory.CreateDirectory(directory);
            var old = Path.Combine(directory, "carnac-20260101.log");
            var oldRotated = Path.Combine(directory, "carnac-20260101.old.log");
            var recent = Path.Combine(directory, "carnac-20260310.log");
            var unrelated = Path.Combine(directory, "notes.txt");
            foreach (var file in new[] { old, oldRotated, recent, unrelated })
                File.WriteAllText(file, "x");
            File.SetLastWriteTime(old, now.AddDays(-40));
            File.SetLastWriteTime(oldRotated, now.AddDays(-40));
            File.SetLastWriteTime(unrelated, now.AddDays(-40));
            File.SetLastWriteTime(recent, now.AddDays(-4));
            var sut = CreateLogger(keepDays: 14);

            sut.Info("first entry of the day");

            Assert.False(File.Exists(old));
            Assert.False(File.Exists(oldRotated));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(unrelated));
            Assert.True(File.Exists(TodaysFile()));
        }

        // ---- robustness ----

        [Fact]
        public void survives_multiple_threads_logging_at_once()
        {
            var sut = CreateLogger();
            const int threadCount = 8;
            const int entriesPerThread = 100;
            var threads = new List<Thread>();
            for (var t = 0; t < threadCount; t++)
            {
                var id = t;
                var thread = new Thread(() =>
                {
                    for (var i = 0; i < entriesPerThread; i++)
                        sut.Info("thread " + id + " entry " + i);
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
                thread.Join();

            var lines = File.ReadAllLines(TodaysFile());
            Assert.Equal(threadCount * entriesPerThread, lines.Length);
            Assert.True(lines.All(line => line.StartsWith("2026-03-14 10:30:00.000 [Info] thread ", StringComparison.Ordinal)), "no line may be torn");
            Assert.Equal(0, sut.WriteFailures);
        }

        [Fact]
        public void never_throws_when_the_folder_cannot_be_created()
        {
            Directory.CreateDirectory(directory);
            var blocker = Path.Combine(directory, "not-a-folder");
            File.WriteAllText(blocker, "this is a file");
            var sut = new FileLogger(Path.Combine(blocker, "logs"), () => now, FileLogger.DefaultMaxFileBytes, TimeSpan.FromMinutes(1), 14);

            sut.Error("cannot be written", Thrown("x"));

            Assert.Equal(1, sut.WriteFailures);
        }

        [Fact]
        public void never_throws_when_the_clock_fails()
        {
            var sut = new FileLogger(directory, () => { throw new InvalidOperationException("no clock"); }, FileLogger.DefaultMaxFileBytes, TimeSpan.FromMinutes(1), 14);

            sut.Info("hello");

            Assert.Equal(1, sut.WriteFailures);
        }

        [Fact]
        public void tolerates_a_null_message()
        {
            var sut = CreateLogger();

            sut.Log(LogLevel.Info, null, null);

            Assert.Contains("[Info] ", ReadTodaysLog());
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentException>(() => new FileLogger(""));
            Assert.Throws<ArgumentException>(() => new FileLogger(null));
            Assert.Throws<ArgumentNullException>(() => new FileLogger(directory, null, 1000, TimeSpan.FromMinutes(1), 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FileLogger(directory, () => now, 0, TimeSpan.FromMinutes(1), 14));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FileLogger(directory, () => now, 1000, TimeSpan.FromMinutes(1), 0));
        }

        static int CountOccurrences(string text, string value)
        {
            var count = 0;
            var index = 0;
            while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }

            return count;
        }
    }
}
