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
    public readonly struct SubscriptionHandle : IDisposable
    {
        private readonly object _subscriber;
        private readonly BaseSubscriberRegistry _registry;
        private readonly Type _eventType;

        internal SubscriptionHandle(object subscriber, BaseSubscriberRegistry registry)
        {
            _subscriber = subscriber;
            _registry = registry;
            _eventType = null;
        }

        internal SubscriptionHandle(object subscriber, BaseSubscriberRegistry registry, Type eventType)
        {
            _subscriber = subscriber;
            _registry = registry;
            _eventType = eventType;
        }

        /// <summary>
        /// Cancels the subscription. Equivalent to calling <c>StopListening</c> on the subscriber.
        /// Safe to call multiple times; subsequent calls are no-ops.
        /// </summary>
        public void Dispose()
        {
            if (_subscriber == null || _registry == null)
                return;

            if (_eventType == null)
            {
                _registry.StopListening(_subscriber);
                return;
            }

            _registry.StopListening(_subscriber, _eventType);
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
            if (!BaseSubscriberRegistry.Subscribers.TryGetValue(subscriber.GetType(), out var iSubscriber))
                throw GenEventRuntimeGuard.CreateMissingSubscriberRegistryException(subscriber.GetType(), nameof(StartListening));

            iSubscriber.StartListening(subscriber);
            return new SubscriptionHandle(subscriber, iSubscriber);
        }

        /// <summary>
        /// Attempts to start listening for all event types handled by this subscriber.
        /// Returns false when the current runtime type has no generated subscriber registry.
        /// Use this for shared lifecycle code where not every instance is necessarily a GenEvent subscriber.
        /// </summary>
        public static bool TryStartListening<TSubscriber>(this TSubscriber subscriber, out SubscriptionHandle handle)
            where TSubscriber : class
        {
            if (!BaseSubscriberRegistry.Subscribers.TryGetValue(subscriber.GetType(), out var iSubscriber))
            {
                handle = default;
                return false;
            }

            iSubscriber.StartListening(subscriber);
            handle = new SubscriptionHandle(subscriber, iSubscriber);
            return true;
        }

        /// <summary>
        /// Returns true when the current runtime type has a generated subscriber registry.
        /// </summary>
        public static bool HasSubscriberRegistry<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            return BaseSubscriberRegistry.Subscribers.ContainsKey(subscriber.GetType());
        }

        /// <summary>
        /// Stops listening for all event types handled by this subscriber.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="subscriber">The subscriber to stop listening for.</param>
        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (!BaseSubscriberRegistry.Subscribers.TryGetValue(subscriber.GetType(), out var iSubscriber))
                throw GenEventRuntimeGuard.CreateMissingSubscriberRegistryException(subscriber.GetType(), nameof(StopListening));

            iSubscriber.StopListening(subscriber);
        }

        /// <summary>
        /// Attempts to stop listening for all event types handled by this subscriber.
        /// Returns false when the current runtime type has no generated subscriber registry.
        /// </summary>
        public static bool TryStopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (!BaseSubscriberRegistry.Subscribers.TryGetValue(subscriber.GetType(), out var iSubscriber))
                return false;

            iSubscriber.StopListening(subscriber);
            return true;
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
            if (!BaseSubscriberRegistry.Subscribers.TryGetValue(subscriber.GetType(), out var iSubscriber))
                throw GenEventRuntimeGuard.CreateMissingSubscriberRegistryException(subscriber.GetType(), nameof(StartListening));

            return new SubscriptionHandle(subscriber, iSubscriber, typeof(TGenEvent));
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

        /// <summary>
        /// Returns the current subscriber count for a specific event/subscriber pair.
        /// </summary>
        public static int GetSubscriberCount<TGenEvent, TSubscriber>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            return GenEventRegistry<TGenEvent, TSubscriber>.SubscriberCount;
        }
    }
}
