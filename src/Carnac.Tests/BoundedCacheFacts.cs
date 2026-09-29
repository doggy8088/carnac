using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class BoundedCacheFacts
    {
        DateTime utcNow = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
        readonly List<int> created = new List<int>();
        readonly List<string> removed = new List<string>();

        BoundedCache<int, string> CreateCache(int capacity = 3, Func<string, bool> isStale = null, TimeSpan? maxAge = null)
        {
            return new BoundedCache<int, string>(capacity, key => { created.Add(key); return "value" + key + "#" + created.Count; },
                isStale, removed.Add, maxAge, () => utcNow);
        }

        [Fact]
        public void creates_a_value_once_and_then_returns_the_cached_one()
        {
            var sut = CreateCache();

            var first = sut.Get(1);
            var second = sut.Get(1);

            Assert.Equal(first, second);
            Assert.Equal(new[] { 1 }, created.ToArray());
        }

        [Fact]
        public void keeps_values_of_different_keys_apart()
        {
            var sut = CreateCache();

            Assert.NotEqual(sut.Get(1), sut.Get(2));
            Assert.Equal(2, sut.Count);
        }

        [Fact]
        public void never_grows_beyond_its_capacity_and_drops_the_oldest_entry_first()
        {
            var sut = CreateCache(3);

            for (var key = 1; key <= 10; key++)
                sut.Get(key);

            Assert.Equal(3, sut.Count);
            created.Clear();
            sut.Get(10);
            sut.Get(9);
            sut.Get(8);
            Assert.Empty(created);          // the three newest are still cached
            sut.Get(1);
            Assert.Equal(new[] { 1 }, created.ToArray());   // an old one had to be created again
        }

        [Fact]
        public void tells_the_owner_about_every_evicted_value()
        {
            var sut = CreateCache(2);
            var first = sut.Get(1);
            sut.Get(2);

            sut.Get(3);

            Assert.Equal(new[] { first }, removed.ToArray());
        }

        [Fact]
        public void replaces_a_value_that_went_stale()
        {
            var stale = new HashSet<string>();
            var sut = CreateCache(3, value => stale.Contains(value));
            var first = sut.Get(1);
            stale.Add(first);

            var second = sut.Get(1);

            Assert.NotEqual(first, second);
            Assert.Equal(new[] { first }, removed.ToArray());
            Assert.Equal(1, sut.Count);
            Assert.Equal(second, sut.Get(1));
        }

        [Fact]
        public void replaces_values_that_are_older_than_the_maximum_age()
        {
            var sut = CreateCache(3, null, TimeSpan.FromSeconds(30));
            var first = sut.Get(1);

            utcNow = utcNow.AddSeconds(29);
            Assert.Equal(first, sut.Get(1));

            utcNow = utcNow.AddSeconds(2);
            Assert.NotEqual(first, sut.Get(1));
        }

        [Fact]
        public void does_not_cache_a_failing_factory()
        {
            var attempts = 0;
            var sut = new BoundedCache<int, string>(3, key =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("not yet");
                return "ok";
            });

            Assert.Throws<InvalidOperationException>(() => sut.Get(1));
            Assert.Equal(0, sut.Count);

            Assert.Equal("ok", sut.Get(1));
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void caches_null_so_that_a_failed_lookup_is_not_repeated()
        {
            var attempts = 0;
            var sut = new BoundedCache<string, string>(3, key => { attempts++; return null; });

            Assert.Null(sut.Get("app without an icon.exe"));
            Assert.Null(sut.Get("app without an icon.exe"));
            Assert.Null(sut.Get("app without an icon.exe"));

            Assert.Equal(1, attempts);
        }

        [Fact]
        public void clear_removes_everything_and_reports_the_values()
        {
            var sut = CreateCache();
            var first = sut.Get(1);
            var second = sut.Get(2);

            sut.Clear();

            Assert.Equal(0, sut.Count);
            Assert.Equal(new[] { first, second }, removed.ToArray());
        }

        [Fact]
        public void can_be_used_from_several_threads()
        {
            var sut = new BoundedCache<int, string>(8, key => "value" + key);
            var errors = new List<Exception>();
            var threads = new List<Thread>();
            for (var t = 0; t < 6; t++)
            {
                var offset = t;
                var thread = new Thread(() =>
                {
                    try
                    {
                        for (var i = 0; i < 2000; i++)
                            Assert.Equal("value" + ((i + offset) % 20), sut.Get((i + offset) % 20));
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                            errors.Add(ex);
                    }
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
                thread.Join();

            Assert.Empty(errors);
            Assert.True(sut.Count <= 8);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoundedCache<int, string>(0, key => "x"));
            Assert.Throws<ArgumentNullException>(() => new BoundedCache<int, string>(1, null));
        }
    }

    public class AssociatedProcessUtilitiesFacts
    {
        [Fact]
        public void the_current_process_has_not_exited()
        {
            using (var current = Process.GetCurrentProcess())
            {
                Assert.False(AssociatedProcessUtilities.HasExited(current));
            }
        }

        [Fact]
        public void a_finished_process_has_exited()
        {
            using (var finished = StartAndWaitForExit())
            {
                Assert.True(AssociatedProcessUtilities.HasExited(finished));
            }
        }

        internal static Process StartAndWaitForExit()
        {
            var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var process = Process.Start(new ProcessStartInfo(cmd, "/c exit 0") { CreateNoWindow = true, UseShellExecute = false });
            process.WaitForExit();
            return process;
        }
    }
}
