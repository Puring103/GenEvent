using System;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// A disposable handle that cancels an event subscription when disposed.
    /// Returned by <see cref="SubscriberHelper.StartListening{TSubscriber}"/> and its overloads.
    /// <para>
    /// Calling <see cref="Dispose"/> is equivalent to calling the corresponding
    /// <c>StopListening</c> method and is safe to call multiple times; subsequent calls are no-ops.
    /// </para>
    /// <para>
    /// The return value of <c>StartListening</c> may be discarded if manual lifecycle management
    /// via <c>StopListening</c> is preferred — existing call sites remain fully compatible.
    /// </para>
    /// </summary>
    public sealed class SubscriptionHandle : IDisposable
    {
        private readonly Action _stop;
        private bool _disposed;

        internal SubscriptionHandle(Action stop)
        {
            _stop = stop;
        }

        /// <summary>
        /// Cancels the subscription. Equivalent to calling <c>StopListening</c> on the subscriber.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _stop?.Invoke();
        }
    }

    /// <summary>
    /// Helper methods for subscribing to events.
    /// Using extension methods to provide a fluent interface for event subscription.
    /// </summary>
    public static class SubscriberHelper
    {
        /// <summary>
        /// Starts listening for all event types handled by this subscriber.
        /// <para>
        /// Returns a <see cref="SubscriptionHandle"/> that cancels the subscription when disposed,
        /// eliminating the need to manually call <see cref="StopListening{TSubscriber}"/>.
        /// The return value may be discarded if manual lifecycle management is preferred.
        /// </para>
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="subscriber">The subscriber to start listening for.</param>
        /// <returns>
        /// A <see cref="SubscriptionHandle"/> whose <see cref="SubscriptionHandle.Dispose"/>
        /// calls <see cref="StopListening{TSubscriber}"/> for all events handled by this subscriber.
        /// </returns>
        public static SubscriptionHandle StartListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
            return new SubscriptionHandle(() => subscriber.StopListening());
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
        /// <para>
        /// Returns a <see cref="SubscriptionHandle"/> that cancels only this specific event
        /// subscription when disposed.
        /// </para>
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type to listen for.</typeparam>
        /// <param name="subscriber">The subscriber to start listening for.</param>
        /// <returns>
        /// A <see cref="SubscriptionHandle"/> whose <see cref="SubscriptionHandle.Dispose"/>
        /// calls <see cref="StopListening{TSubscriber, TGenEvent}"/> for this specific event type.
        /// </returns>
        public static SubscriptionHandle StartListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StartListening<TSubscriber, TGenEvent>(subscriber);
            return new SubscriptionHandle(() => subscriber.StopListening<TSubscriber, TGenEvent>());
        }

        /// <summary>
        /// Stops listening for a specific event type handled by this subscriber.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type to stop listening for.</typeparam>
        /// <param name="subscriber">The subscriber to stop listening for.</param>
        public static void StopListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StopListening<TSubscriber, TGenEvent>(subscriber);
        }
    }
}
