using System;
using System.Collections.Generic;

namespace GenEvent.Interface
{
    /// <summary>
    /// Base class for subscriber registry classes.
    /// All generated subscriber registry code will inherit from this class.
    /// </summary>
    public abstract class BaseSubscriberRegistry
    {
        public static readonly Dictionary<Type, BaseSubscriberRegistry> Subscribers = new();

        /// <summary>
        /// Starts listening for all event types handled by this subscriber class.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="self">The subscriber instance.</param>
        public abstract void StartListening<TSubscriber>(TSubscriber self)
            where TSubscriber : class;

        /// <summary>
        /// Stops listening for all event types handled by this subscriber class.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <param name="self">The subscriber instance.</param>
        public abstract void StopListening<TSubscriber>(TSubscriber self)
            where TSubscriber : class;

        /// <summary>
        /// Starts listening for a specific event type handled by this subscriber class.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="self">The subscriber instance.</param>
        public static void StartListening<TSubscriber, TGenEvent>(TSubscriber self)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            GenEventRegistry<TGenEvent, TSubscriber>.Register(self);
        }

        /// <summary>
        /// Stops listening for a specific event type handled by this subscriber class.
        /// </summary>
        /// <typeparam name="TSubscriber">The subscriber type.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="self">The subscriber instance.</param>
        public static void StopListening<TSubscriber, TGenEvent>(TSubscriber self)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            GenEventRegistry<TGenEvent, TSubscriber>.UnRegister(self);
        }
    }
}