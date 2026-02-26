using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GenEvent;

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
        /// <param name="config">The publish config for this publish (filters, Cancelable), passed from the Publish entry.</param>
        /// <returns>
        /// true if all subscribers successfully handled the event;
        /// false if any subscriber cancelled propagation (event stopped before reaching all subscribers)
        /// </returns>
        public abstract bool Publish<TGenEvent>(TGenEvent @event, PublishConfig<TGenEvent> config)
            where TGenEvent : struct, IGenEvent<TGenEvent>;

        /// <summary>
        /// Publishes an event to all its subscribers asynchronously.
        /// </summary>
        /// <typeparam name="TGenEvent">Type of event to publish</typeparam>
        /// <param name="event">The event instance to publish</param>
        /// <param name="config">The publish config for this publish (filters, Cancelable), passed from the PublishAsync entry.</param>
        /// <returns>Task that completes with true if all subscribers handled the event; false if any cancelled propagation.</returns>
        public abstract Task<bool> PublishAsync<TGenEvent>(TGenEvent @event, PublishConfig<TGenEvent> config)
            where TGenEvent : struct, IGenEvent<TGenEvent>;
    }
}