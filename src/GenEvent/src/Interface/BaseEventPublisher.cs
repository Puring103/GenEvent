using System;
using System.Collections.Generic;

namespace GenEvent.Interface
{
    /// <summary>
    /// Base class for event wrapper classes.
    /// All generated event publisher code will inherit from this class.
    /// </summary>
    public abstract class BaseEventPublisher
    {
        public static readonly Dictionary<Type, BaseEventPublisher> Publishers = new();

        /// <summary>
        /// Publishes an event to all its subscribers.
        /// </summary>
        /// <typeparam name="TGenEvent">Type of event to publish</typeparam>
        /// <param name="event">The event instance to publish</param>
        /// <returns>
        /// true if all subscribers successfully handled the event;
        /// false if any subscriber cancelled propagation (event stopped before reaching all subscribers)
        /// </returns>
        public abstract bool Publish<TGenEvent>(TGenEvent @event)
            where TGenEvent : struct, IGenEvent<TGenEvent>;
    }
}