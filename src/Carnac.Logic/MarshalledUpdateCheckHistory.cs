using System;
using System.Runtime.ExceptionServices;

namespace Carnac.Logic
{
    /// <summary>
    /// Lets another <see cref="IUpdateCheckHistory"/> be used from a background thread: every call runs on the thread that owns the
    /// settings (the UI thread), because the settings provider is not thread-safe and the preferences window uses it at the same time.
    /// </summary>
    public sealed class MarshalledUpdateCheckHistory : IUpdateCheckHistory
    {
        readonly IUpdateCheckHistory inner;
        readonly Action<Action> invokeAndWait;

        /// <param name="invokeAndWait">Runs an action on the settings' thread and returns after it has finished.</param>
        public MarshalledUpdateCheckHistory(IUpdateCheckHistory inner, Action<Action> invokeAndWait)
        {
            if (inner == null)
                throw new ArgumentNullException("inner");
            if (invokeAndWait == null)
                throw new ArgumentNullException("invokeAndWait");

            this.inner = inner;
            this.invokeAndWait = invokeAndWait;
        }

        public DateTime? LastCheckUtc
        {
            get
            {
                DateTime? last = null;
                Run(() => last = inner.LastCheckUtc);
                return last;
            }
        }

        public void RecordCheck(DateTime utcNow)
        {
            Run(() => inner.RecordCheck(utcNow));
        }

        void Run(Action action)
        {
            // An exception thrown inside Dispatcher.Invoke does not come back to the caller (it goes to the dispatcher's
            // unhandled-exception handling), so it is carried over by hand.
            ExceptionDispatchInfo failure = null;
            invokeAndWait(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
            });

            if (failure != null)
                failure.Throw();
        }
    }
}
