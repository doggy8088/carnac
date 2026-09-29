using System;
using System.Threading;
using System.Threading.Tasks;
using Carnac.Logic;
using Xunit;

namespace Carnac.Tests
{
    public class MarshalledUpdateCheckHistoryFacts
    {
        readonly RecordingHistory inner = new RecordingHistory();
        int owningThreadId;
        int invocations;

        // stands in for Dispatcher.Invoke: runs the action on another thread and waits for it
        void InvokeOnOtherThread(Action action)
        {
            invocations++;
            Task.Factory.StartNew(() =>
            {
                owningThreadId = Thread.CurrentThread.ManagedThreadId;
                action();
            }).Wait();
        }

        [Fact]
        public void reads_the_last_check_on_the_owning_thread()
        {
            inner.Last = new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc);
            var sut = new MarshalledUpdateCheckHistory(inner, InvokeOnOtherThread);

            var last = sut.LastCheckUtc;

            Assert.Equal(new DateTime(2026, 3, 14, 10, 0, 0, DateTimeKind.Utc), last);
            Assert.Equal(owningThreadId, inner.ThreadOfLastCall);
            Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, inner.ThreadOfLastCall);
        }

        [Fact]
        public void records_the_check_on_the_owning_thread()
        {
            var sut = new MarshalledUpdateCheckHistory(inner, InvokeOnOtherThread);

            sut.RecordCheck(new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc));

            Assert.Equal(new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Utc), inner.Last);
            Assert.Equal(owningThreadId, inner.ThreadOfLastCall);
            Assert.Equal(1, invocations);
        }

        [Fact]
        public void an_exception_of_the_inner_history_reaches_the_caller()
        {
            inner.Failure = new InvalidOperationException("settings are broken");
            var sut = new MarshalledUpdateCheckHistory(inner, InvokeOnOtherThread);

            var read = Assert.Throws<InvalidOperationException>(() => { var ignored = sut.LastCheckUtc; });
            var write = Assert.Throws<InvalidOperationException>(() => sut.RecordCheck(DateTime.UtcNow));

            Assert.Equal("settings are broken", read.Message);
            Assert.Equal("settings are broken", write.Message);
        }

        [Fact]
        public void constructor_validates_its_arguments()
        {
            Assert.Throws<ArgumentNullException>(() => new MarshalledUpdateCheckHistory(null, InvokeOnOtherThread));
            Assert.Throws<ArgumentNullException>(() => new MarshalledUpdateCheckHistory(inner, null));
        }

        class RecordingHistory : IUpdateCheckHistory
        {
            public DateTime? Last;
            public Exception Failure;
            public int ThreadOfLastCall;

            public DateTime? LastCheckUtc
            {
                get
                {
                    ThreadOfLastCall = Thread.CurrentThread.ManagedThreadId;
                    if (Failure != null)
                        throw Failure;
                    return Last;
                }
            }

            public void RecordCheck(DateTime utcNow)
            {
                ThreadOfLastCall = Thread.CurrentThread.ManagedThreadId;
                if (Failure != null)
                    throw Failure;
                Last = utcNow;
            }
        }
    }
}
