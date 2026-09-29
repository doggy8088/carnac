using System;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Carnac.Logic.Models;

namespace Carnac.Logic
{
    public class MessageProvider : IMessageProvider
    {
        /// <summary>
        /// How long the first keys of a possible multi-key shortcut ("chord", for example the Ctrl+K of Ctrl+K,Ctrl+C)
        /// are held back waiting for the rest of it. When the next key does not arrive in time the held keys are shown
        /// as ordinary key presses. Same window as <see cref="Message.MergeIfNeeded"/> uses to merge key presses.
        /// </summary>
        public static readonly TimeSpan ChordTimeout = TimeSpan.FromSeconds(1);

        readonly IShortcutProvider shortcutProvider;
        readonly IKeyProvider keyProvider;
        readonly PopupSettings settings;
        readonly IConcurrencyService concurrencyService;

        public MessageProvider(IShortcutProvider shortcutProvider, IKeyProvider keyProvider, PopupSettings settings, IConcurrencyService concurrencyService)
        {
            if (shortcutProvider == null)
                throw new ArgumentNullException("shortcutProvider");
            if (keyProvider == null)
                throw new ArgumentNullException("keyProvider");
            if (settings == null)
                throw new ArgumentNullException("settings");
            if (concurrencyService == null)
                throw new ArgumentNullException("concurrencyService");

            this.shortcutProvider = shortcutProvider;
            this.keyProvider = keyProvider;
            this.settings = settings;
            this.concurrencyService = concurrencyService;
        }

        public IObservable<Message> GetMessageStream()
        {
            /*
            shortcut Acc stream:
               - ! before item means HasCompletedValue is false
               - [1 & 2] means multiple messages are returned from get messages (1 and 2 in this case), others are a single message returned
               - a pending accumulator is completed by the next key, or flushed (each held key becomes its own message) when
                 ChordTimeout passes without another key; only completed accumulators leave GetCompletedShortcuts

            message merger:
               - * before items indicates the previous message has been modified (key has been merged into acc), otherwise new acc is created

            keystream   :  a---b---ctrl+r----ctrl+r----------ctrl+r----a--------------↓---↓
            shortcut Acc:  a---b---!ctrl+r---ctrl+r,ctrl+r---!ctrl+r---[ctrl+r & a]---↓---↓
            sel many    :  a---b-------------ctrl+r,ctrl+r-------------ctrl+r---a-----↓---↓
            msg merger  :  a---*ab-----------ctrl+r,ctrl+r-------------ctrl+r---a-----↓---*'↓ x2'
            */
            return GetCompletedShortcuts()
                .SelectMany(c => c.GetMessages())
                .Scan(new Message(), (acc, key) => Message.MergeIfNeeded(acc, key))
                .Where(m =>
                {
                    if (settings.DetectShortcutsOnly && settings.ShowOnlyModifiers)
                    {
                        return m.IsShortcut && m.IsModifier;
                    }
                    if (settings.DetectShortcutsOnly)
                    {
                        return m.IsShortcut;
                    }
                    if (settings.ShowOnlyModifiers)
                    {
                        return m.IsModifier;
                    }
                    return true;
                });
        }

        /// <summary>
        /// Feeds the key stream through a <see cref="ShortcutAccumulator"/> and emits it whenever it has completed.
        /// A key that starts a possible shortcut leaves the accumulator pending; nothing is emitted until the shortcut
        /// is completed or broken by a later key, or the chord timeout flushes it.
        /// </summary>
        IObservable<ShortcutAccumulator> GetCompletedShortcuts()
        {
            return Observable.Create<ShortcutAccumulator>(observer =>
            {
                // Keys come from the low-level keyboard hook on the UI thread and the timeout runs on the main thread
                // scheduler, so both normally share a thread; the lock keeps the accumulator consistent regardless.
                var gate = new object();
                var accumulator = new ShortcutAccumulator();
                var keyCount = 0L;
                var flushTimer = new SerialDisposable();

                var keySubscription = keyProvider.GetKeyStream().Subscribe(
                    key =>
                    {
                        lock (gate)
                        {
                            keyCount++;
                            // The next key always supersedes a pending timeout; it is rescheduled below if needed.
                            flushTimer.Disposable = Disposable.Empty;

                            if (accumulator.IsPendingForOtherProcess(key))
                            {
                                // The held keys cannot become a shortcut any more, but the new key must not be lost.
                                accumulator.Flush();
                                observer.OnNext(accumulator);
                                accumulator = new ShortcutAccumulator();
                            }

                            accumulator = accumulator.ProcessKey(shortcutProvider, key);
                            if (accumulator.HasCompletedValue)
                            {
                                observer.OnNext(accumulator);
                                return;
                            }

                            var pending = accumulator;
                            var pendingKeyCount = keyCount;
                            flushTimer.Disposable = concurrencyService.MainThreadScheduler.Schedule(ChordTimeout, () =>
                            {
                                lock (gate)
                                {
                                    // A key that arrived while this callback waited for the lock has replaced the timeout.
                                    if (pendingKeyCount != keyCount || pending.HasCompletedValue)
                                        return;

                                    pending.Flush();
                                    observer.OnNext(pending);
                                }
                            });
                        }
                    },
                    error =>
                    {
                        lock (gate)
                        {
                            flushTimer.Dispose();
                            observer.OnError(error);
                        }
                    },
                    () =>
                    {
                        lock (gate)
                        {
                            flushTimer.Dispose();
                            observer.OnCompleted();
                        }
                    });

                return new CompositeDisposable(keySubscription, flushTimer);
            });
        }
    }
}
