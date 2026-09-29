using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Carnac.Logic
{
    public static class RetryWithDelayExtensions
    {
        /// <summary>
        /// Like Retry(), but waits <paramref name="delay"/> before it subscribes again and reports every error to
        /// <paramref name="onError"/>, so that a failing source is neither retried in a busy loop nor retried silently.
        /// The sequence never ends because of an error; it ends when the source completes or the subscription is disposed.
        /// </summary>
        /// <remarks>Each retry subscribes to <paramref name="source"/> again: use Observable.Defer when a fresh pipeline is needed.</remarks>
        public static IObservable<T> RetryWithDelay<T>(this IObservable<T> source, TimeSpan delay, IScheduler scheduler, Action<Exception> onError)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (scheduler == null)
                throw new ArgumentNullException("scheduler");
            if (onError == null)
                throw new ArgumentNullException("onError");

            return Observable.Create<T>(observer =>
            {
                var subscription = new SerialDisposable();
                var retryTimer = new SerialDisposable();
                Action subscribe = null;
                subscribe = () =>
                {
                    subscription.Disposable = source.Subscribe(
                        observer.OnNext,
                        ex =>
                        {
                            onError(ex);
                            retryTimer.Disposable = scheduler.Schedule(delay, subscribe);
                        },
                        observer.OnCompleted);
                };

                subscribe();
                return new CompositeDisposable(subscription, retryTimer);
            });
        }
    }
}
