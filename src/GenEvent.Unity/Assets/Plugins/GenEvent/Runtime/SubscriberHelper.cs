using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Helper methods for subscribing to events.
    /// Using extension methods to provide a fluent interface for event subscription.
    /// </summary>
    public static class SubscriberHelper
    {
        /// <summary>
        /// Starts listening for all event types handled by this subscriber.
        /// Remember to call <see cref="StopListening"/> when the subscriber is no longer needed.
        /// If not, the subscriber will continue to receive events until the application exits.
        /// And the subscriber will not be garbage collected until the application exits.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="subscriber">The subscriber to start listening for.</param>
        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }

        /// <summary>
        /// Stops listening for all event types handled by this subscriber.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="subscriber">The subscriber to stop listening for.</param>
        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }

        /// <summary>
        /// Starts listening for a specific event type handled by this subscriber.
        /// Remember to call <see cref="StopListening"/> when the subscriber is no longer needed.
        /// If not, the subscriber will continue to receive events until the application exits.
        /// And the subscriber will not be garbage collected until the application exits.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="subscriber">The subscriber to start listening for.</param>
        public static void StartListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StartListening<TSubscriber, TGenEvent>(subscriber);
        }

        /// <summary>
        /// Stops listening for a specific event type handled by this subscriber.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="subscriber">The subscriber to stop listening for.</param>
        public static void StopListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StopListening<TSubscriber, TGenEvent>(subscriber);
        }
    }
}