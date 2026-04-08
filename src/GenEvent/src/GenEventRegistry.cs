using System.Collections.Generic;
using System.Threading.Tasks;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Delegate for handling events.
    /// Returns true to continue event propagation, or false to cancel propagation.
    /// If all subscribers return true, Publish returns true.
    /// If any subscriber returns false, Publish returns false immediately.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
    /// <param name="gameEvent">The event to publish.</param>
    /// <param name="subscriber">The subscriber handling the event.</param>
    /// <returns>
    /// True to continue dispatching the event to other subscribers;
    /// false to stop event propagation.
    /// </returns>
    public delegate bool GenEventDelegate<in TGenEvent, in TSubscriber>(TGenEvent gameEvent, TSubscriber subscriber);

    /// <summary>
    /// Async delegate for handling events.
    /// Returns a task that completes with true to continue propagation, or false to cancel.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
    /// <param name="gameEvent">The event to publish.</param>
    /// <param name="subscriber">The subscriber handling the event.</param>
    /// <returns>Task that completes with true to continue, false to stop propagation.</returns>
    public delegate Task<bool> GenEventAsyncDelegate<in TGenEvent, in TSubscriber>(TGenEvent gameEvent, TSubscriber subscriber);

    /// <summary>
    /// Registry for event handling.
    /// Uses List + Dictionary for O(1) removal by subscriber instance.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
    public static class GenEventRegistry<TGenEvent, TSubscriber>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private const int SnapshotPoolCapacity = 16;

        /// <summary>
        /// List of subscribers for iteration (no holes; removal uses swap-with-last).
        /// </summary>
        private static readonly List<TSubscriber> SubscriberList = new();

        /// <summary>
        /// Map from subscriber to index in SubscriberList, for O(1) removal.
        /// </summary>
        private static readonly Dictionary<TSubscriber, int> SubscriberIndex = new();

        /// <summary>
        /// Reusable snapshot buffers for publish-time iteration stability.
        /// </summary>
        private static readonly List<List<TSubscriber>> SnapshotPool = new(SnapshotPoolCapacity);

        /// <summary>
        /// Delegate for handling events.
        /// </summary>
        public static GenEventDelegate<TGenEvent, TSubscriber> GenEvent { get; private set; }

        /// <summary>
        /// Async delegate for handling events. May be null if only sync handler is registered.
        /// </summary>
        public static GenEventAsyncDelegate<TGenEvent, TSubscriber> GenEventAsync { get; private set; }

        public static IReadOnlyList<TSubscriber> Subscribers => SubscriberList;

        /// <summary>
        /// Takes a stable snapshot of current subscribers using a pooled list.
        /// </summary>
        public static List<TSubscriber> TakeSubscribersSnapshot()
        {
            List<TSubscriber> snapshot;
            if (SnapshotPool.Count > 0)
            {
                var index = SnapshotPool.Count - 1;
                snapshot = SnapshotPool[index];
                SnapshotPool.RemoveAt(index);
            }
            else
            {
                snapshot = new List<TSubscriber>(SubscriberList.Count);
            }

            snapshot.AddRange(SubscriberList);
            return snapshot;
        }

        /// <summary>
        /// Clears and returns a subscriber snapshot buffer to the pool.
        /// </summary>
        public static void ReturnSubscribersSnapshot(List<TSubscriber> snapshot)
        {
            snapshot.Clear();
            if (SnapshotPool.Count < SnapshotPoolCapacity)
            {
                SnapshotPool.Add(snapshot);
            }
        }

        /// <summary>
        /// Initializes the event registry with a sync delegate.
        /// </summary>
        /// <param name="genEventDelegate">The delegate for handling events.</param>
        public static void Initialize(GenEventDelegate<TGenEvent, TSubscriber> genEventDelegate)
        {
            GenEvent = genEventDelegate;
        }

        /// <summary>
        /// Initializes the async delegate for the event registry.
        /// May be used in addition to Initialize when a class has both sync and async handlers for the same event.
        /// </summary>
        /// <param name="genEventAsyncDelegate">The async delegate for handling events.</param>
        public static void InitializeAsync(GenEventAsyncDelegate<TGenEvent, TSubscriber> genEventAsyncDelegate)
        {
            GenEventAsync = genEventAsyncDelegate;
        }

        /// <summary>
        /// Registers a subscriber for the event.
        /// Duplicate registration is ignored (idempotent).
        /// </summary>
        /// <param name="observer">The subscriber to register.</param>
        public static void Register(TSubscriber observer)
        {
            if (SubscriberIndex.ContainsKey(observer))
                return;
            var index = SubscriberList.Count;
            SubscriberList.Add(observer);
            SubscriberIndex.Add(observer, index);
        }

        /// <summary>
        /// Unregisters a subscriber for the event.
        /// O(1) removal. No-op if not registered.
        /// </summary>
        /// <param name="observer">The subscriber to unregister.</param>
        public static void UnRegister(TSubscriber observer)
        {
            if (!SubscriberIndex.TryGetValue(observer, out var index))
                return;
            var lastIndex = SubscriberList.Count - 1;
            SubscriberIndex.Remove(observer);
            if (index != lastIndex)
            {
                var last = SubscriberList[lastIndex];
                SubscriberList[index] = last;
                SubscriberIndex[last] = index;
            }
            SubscriberList.RemoveAt(lastIndex);
        }
    }
}
