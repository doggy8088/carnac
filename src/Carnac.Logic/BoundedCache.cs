using System;
using System.Collections.Generic;

namespace Carnac.Logic
{
    /// <summary>
    /// A small thread-safe cache with a hard size limit. When it is full the entry that was added first is dropped.
    /// Entries can go stale (the value says so, or it is older than <c>maxAge</c>) and are then created again on the next request.
    /// </summary>
    /// <remarks>
    /// The value factory runs while the cache is locked. A factory that throws caches nothing, so the next request tries again;
    /// a factory that returns <c>null</c> caches the <c>null</c> (a "known to have no value" marker, for example an executable
    /// without an icon).
    /// </remarks>
    public sealed class BoundedCache<TKey, TValue>
    {
        readonly object sync = new object();
        readonly int capacity;
        readonly Func<TKey, TValue> factory;
        readonly Func<TValue, bool> isStale;
        readonly Action<TValue> onRemoved;
        readonly TimeSpan? maxAge;
        readonly Func<DateTime> utcNow;
        readonly Dictionary<TKey, LinkedListNode<Entry>> nodes = new Dictionary<TKey, LinkedListNode<Entry>>();
        readonly LinkedList<Entry> entries = new LinkedList<Entry>();

        /// <param name="capacity">Maximum number of entries.</param>
        /// <param name="factory">Creates the value for a key.</param>
        /// <param name="isStale">Tells whether a cached value must be replaced; may be null.</param>
        /// <param name="onRemoved">Called for every value that leaves the cache (evicted, stale or cleared), for example to dispose it; may be null.</param>
        /// <param name="maxAge">Values older than this are replaced even when they do not look stale; null for no limit.</param>
        /// <param name="utcNow">Clock; defaults to <see cref="DateTime.UtcNow"/>.</param>
        public BoundedCache(int capacity, Func<TKey, TValue> factory, Func<TValue, bool> isStale = null, Action<TValue> onRemoved = null,
            TimeSpan? maxAge = null, Func<DateTime> utcNow = null)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException("capacity");
            if (factory == null)
                throw new ArgumentNullException("factory");

            this.capacity = capacity;
            this.factory = factory;
            this.isStale = isStale;
            this.onRemoved = onRemoved;
            this.maxAge = maxAge;
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public int Count
        {
            get
            {
                lock (sync)
                {
                    return nodes.Count;
                }
            }
        }

        public TValue Get(TKey key)
        {
            lock (sync)
            {
                var now = utcNow();
                LinkedListNode<Entry> node;
                if (nodes.TryGetValue(key, out node))
                {
                    if (!IsExpired(node.Value, now))
                        return node.Value.Value;

                    Remove(node);
                }

                var value = factory(key);

                while (nodes.Count >= capacity)
                    Remove(entries.First);

                nodes[key] = entries.AddLast(new Entry(key, value, now));
                return value;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                while (entries.First != null)
                    Remove(entries.First);
            }
        }

        bool IsExpired(Entry entry, DateTime now)
        {
            if (maxAge.HasValue && now - entry.CreatedAt > maxAge.Value)
                return true;

            return isStale != null && isStale(entry.Value);
        }

        void Remove(LinkedListNode<Entry> node)
        {
            nodes.Remove(node.Value.Key);
            entries.Remove(node);
            if (onRemoved != null)
                onRemoved(node.Value.Value);
        }

        sealed class Entry
        {
            public Entry(TKey key, TValue value, DateTime createdAt)
            {
                Key = key;
                Value = value;
                CreatedAt = createdAt;
            }

            public TKey Key { get; private set; }
            public TValue Value { get; private set; }
            public DateTime CreatedAt { get; private set; }
        }
    }
}
