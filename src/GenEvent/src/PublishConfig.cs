using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Configuration for a single event publish.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    public class PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        /// <summary>
        /// Indicates whether the event is cancelable.
        /// </summary>
        public bool Cancelable { get; private set; }

        /// <summary>
        /// List of subscriber filters, used to filter subscribers before publishing the event.
        /// </summary>
        private List<Predicate<object>> SubscriberFilters { get; } = new(16);

        internal void Reset()
        {
            Cancelable = false;
            SubscriberFilters.Clear();
        }

        /// <summary>
        /// Sets the publish config as cancelable.
        /// </summary>
        public void SetCancelable()
        {
            Cancelable = true;
        }

        /// <summary>
        /// Adds a filter to the publish config.
        /// </summary>
        /// <param name="filter">The filter to add.</param>
        public void AddFilter(Predicate<object> filter)
        {
            SubscriberFilters.Add(filter);
        }

        /// <summary>
        /// Checks if a subscriber should be filtered.
        /// </summary>
        /// <param name="subscriber">The subscriber to check.</param>
        /// <returns>True if the subscriber is filtered, false otherwise.</returns>
        public bool IsFiltered(object subscriber)
        {
            for (int i = 0; i < SubscriberFilters.Count; i++)
            {
                if (SubscriberFilters[i](subscriber))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
