using System.Collections.Generic;
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
    /// Registry for event handling.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
    public static class GenEventRegistry<TGenEvent, TSubscriber>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        /// <summary>
        /// List of subscribers for the event.
        /// </summary>
        private static readonly List<TSubscriber> SubscriberList = new() { };

        /// <summary>
        /// Delegate for handling events.
        /// </summary>
        public static GenEventDelegate<TGenEvent, TSubscriber> GenEvent { get; private set; }

        public static IReadOnlyList<TSubscriber> Subscribers => SubscriberList;

        /// <summary>
        /// Initializes the event registry.
        /// </summary>
        /// <param name="genEventDelegate">The delegate for handling events.</param>
        public static void Initialize(GenEventDelegate<TGenEvent, TSubscriber> genEventDelegate)
        {
            GenEvent = genEventDelegate;
        }

        /// <summary>
        /// Registers a subscriber for the event.
        /// </summary>
        /// <param name="observer">The subscriber to register.</param>
        public static void Register(TSubscriber observer)
        {
            SubscriberList.Add(observer);
        }

        /// <summary>
        /// Unregisters a subscriber for the event.
        /// </summary>
        /// <param name="observer">The subscriber to unregister.</param>
        public static void UnRegister(TSubscriber observer)
        {
            SubscriberList.Remove(observer);
        }
    }
}