using System;
using System.Collections.Generic;
using System.Threading;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class KeyDisplayStateFacts
    {
        readonly KeyDisplayState sut = new KeyDisplayState();

        [Fact]
        public void starts_neither_paused_nor_silent()
        {
            Assert.False(sut.IsPaused);
            Assert.False(sut.IsSilent);
        }

        [Fact]
        public void toggling_pause_returns_the_new_value()
        {
            Assert.True(sut.TogglePaused());
            Assert.True(sut.IsPaused);

            Assert.False(sut.TogglePaused());
            Assert.False(sut.IsPaused);
        }

        [Fact]
        public void toggling_silent_mode_returns_the_new_value()
        {
            Assert.True(sut.ToggleSilent());
            Assert.True(sut.IsSilent);

            Assert.False(sut.ToggleSilent());
            Assert.False(sut.IsSilent);
        }

        [Fact]
        public void pause_and_silent_mode_are_independent()
        {
            sut.SetPaused(true);

            Assert.True(sut.IsPaused);
            Assert.False(sut.IsSilent);

            sut.SetSilent(true);
            sut.SetPaused(false);

            Assert.False(sut.IsPaused);
            Assert.True(sut.IsSilent);
        }

        [Fact]
        public void raises_changed_for_every_toggle()
        {
            var changes = 0;
            sut.Changed += (sender, args) => changes++;

            sut.TogglePaused();
            sut.ToggleSilent();
            sut.TogglePaused();

            Assert.Equal(3, changes);
        }

        [Fact]
        public void raises_changed_only_when_a_setter_really_changes_the_value()
        {
            var changes = 0;
            sut.Changed += (sender, args) => changes++;

            sut.SetPaused(false);
            sut.SetSilent(false);
            Assert.Equal(0, changes);

            sut.SetPaused(true);
            sut.SetPaused(true);
            sut.SetSilent(true);
            sut.SetSilent(true);
            Assert.Equal(2, changes);
        }

        [Fact]
        public void changed_is_raised_with_the_state_as_sender_and_the_new_values_are_readable_in_the_handler()
        {
            object actualSender = null;
            var pausedInHandler = false;
            sut.Changed += (sender, args) =>
            {
                actualSender = sender;
                pausedInHandler = sut.IsPaused;
            };

            sut.SetPaused(true);

            Assert.Same(sut, actualSender);
            Assert.True(pausedInHandler);
        }

        [Fact]
        public void concurrent_toggles_from_several_threads_are_not_lost()
        {
            const int threadCount = 8;
            const int togglesPerThread = 1000;
            var changes = 0;
            sut.Changed += (sender, args) => Interlocked.Increment(ref changes);
            var threads = new List<Thread>();
            for (var i = 0; i < threadCount; i++)
            {
                var thread = new Thread(() =>
                {
                    for (var j = 0; j < togglesPerThread; j++)
                        sut.TogglePaused();
                });
                threads.Add(thread);
                thread.Start();
            }

            foreach (var thread in threads)
                thread.Join();

            // an even number of toggles always ends where it started, whatever the interleaving was
            Assert.False(sut.IsPaused);
            Assert.Equal(threadCount * togglesPerThread, changes);
        }
    }
}
