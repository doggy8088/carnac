using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Carnac.Logic;
using Microsoft.Reactive.Testing;
using Xunit;

namespace Carnac.Tests
{
    public class RetryWithDelayFacts
    {
        static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);
        readonly TestScheduler scheduler = new TestScheduler();
        readonly List<int> received = new List<int>();
        readonly List<Exception> errors = new List<Exception>();
        bool completed;
        Exception terminalError;

        IDisposable Subscribe(IObservable<int> source)
        {
            return source
                .RetryWithDelay(Delay, scheduler, errors.Add)
                .Subscribe(received.Add, ex => terminalError = ex, () => completed = true);
        }

        [Fact]
        public void passes_values_through_when_nothing_fails()
        {
            Subscribe(new[] { 1, 2, 3 }.ToObservable());

            Assert.Equal(new[] { 1, 2, 3 }, received.ToArray());
            Assert.Empty(errors);
            Assert.True(completed);
        }

        [Fact]
        public void subscribes_again_after_the_delay_when_the_source_fails()
        {
            var attempts = 0;
            var source = Observable.Defer(() =>
            {
                attempts++;
                return attempts == 1
                    ? Observable.Throw<int>(new InvalidOperationException("first attempt"))
                    : new[] { 7, 8 }.ToObservable();
            });

            Subscribe(source);

            Assert.Equal(1, attempts);
            Assert.Empty(received);

            scheduler.AdvanceBy(Delay.Ticks - 1);
            Assert.Equal(1, attempts);

            scheduler.AdvanceBy(1);
            Assert.Equal(2, attempts);
            Assert.Equal(new[] { 7, 8 }, received.ToArray());
            Assert.True(completed);
        }

        [Fact]
        public void reports_every_error_and_never_ends_because_of_one()
        {
            var attempts = 0;
            var source = Observable.Defer(() =>
            {
                attempts++;
                return attempts <= 3
                    ? Observable.Throw<int>(new InvalidOperationException("attempt " + attempts))
                    : Observable.Return(attempts);
            });

            Subscribe(source);
            scheduler.AdvanceBy(Delay.Ticks * 10);

            Assert.Equal(4, attempts);
            Assert.Equal(new[] { "attempt 1", "attempt 2", "attempt 3" }, errors.ConvertAll(ex => ex.Message).ToArray());
            Assert.Equal(new[] { 4 }, received.ToArray());
            Assert.Null(terminalError);
        }

        [Fact]
        public void keeps_the_values_that_arrived_before_a_failure()
        {
            var subject = new Subject<int>();
            Subscribe(subject);

            subject.OnNext(1);
            subject.OnError(new InvalidOperationException("dropped"));

            Assert.Equal(new[] { 1 }, received.ToArray());
            Assert.Equal(1, errors.Count);
        }

        [Fact]
        public void waits_between_attempts_instead_of_looping_at_full_speed()
        {
            var attempts = 0;
            var source = Observable.Defer(() =>
            {
                attempts++;
                return Observable.Throw<int>(new InvalidOperationException("always"));
            });

            Subscribe(source);
            scheduler.AdvanceBy(Delay.Ticks * 5);

            Assert.Equal(6, attempts);   // the first one plus one per second
        }

        [Fact]
        public void disposing_cancels_a_pending_retry()
        {
            var attempts = 0;
            var source = Observable.Defer(() =>
            {
                attempts++;
                return Observable.Throw<int>(new InvalidOperationException("always"));
            });
            var subscription = Subscribe(source);

            subscription.Dispose();
            scheduler.AdvanceBy(Delay.Ticks * 5);

            Assert.Equal(1, attempts);
        }

        [Fact]
        public void the_source_completing_ends_the_sequence_without_a_retry()
        {
            var attempts = 0;
            var source = Observable.Defer(() =>
            {
                attempts++;
                return Observable.Empty<int>();
            });

            Subscribe(source);
            scheduler.AdvanceBy(Delay.Ticks * 5);

            Assert.True(completed);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public void validates_its_arguments()
        {
            var source = Observable.Return(1);

            Assert.Throws<ArgumentNullException>(() => ((IObservable<int>)null).RetryWithDelay(Delay, scheduler, ex => { }));
            Assert.Throws<ArgumentNullException>(() => source.RetryWithDelay(Delay, null, ex => { }));
            Assert.Throws<ArgumentNullException>(() => source.RetryWithDelay(Delay, scheduler, null));
        }
    }
}
