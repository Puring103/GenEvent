using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Helper methods for publishing events.
    /// Using extension methods to provide a fluent interface for event publishing.
    /// </summary>
    public static class PublisherHelper
    {
        /// <summary>
        /// Publishes an event using the current fluent config. Takes that config for this publish and replaces the static Setting with a new one from the pool; when publish ends, the used config is cleared and returned to the pool.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>True if all subscribers successfully handled the event; false if any subscriber cancelled propagation (event stopped before reaching all subscribers)</returns>
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var config = PublishConfig<TGenEvent>.TakeForPublish();
            try
            {
                var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
                return publisher.Publish(gameEvent, config);
            }
            finally
            {
                PublishConfig<TGenEvent>.ReturnUsedConfig(config);
            }
        }

        /// <summary>
        /// Publishes an event asynchronously using the current fluent config.
        /// Same config lifecycle as Publish; use await and ensure ReturnUsedConfig in finally.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>Task that completes with true if all subscribers handled the event; false if any cancelled propagation.</returns>
        public static async Task<bool> PublishAsync<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var config = PublishConfig<TGenEvent>.TakeForPublish();
            try
            {
                var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
                return await publisher.PublishAsync(gameEvent, config);
            }
            finally
            {
                PublishConfig<TGenEvent>.ReturnUsedConfig(config);
            }
        }

        /// <summary>
        /// Sets the publish config as cancelable.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The event.</returns>
        public static TGenEvent Cancelable<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.SetCancelable();
            return gameEvent;
        }

        /// <summary>
        /// Adds a filter to the publish config.
        /// Filter returns true if the subscriber should be filtered out.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="filter">The filter to add.</param>
        /// <returns>The event.</returns>
        public static TGenEvent WithFilter<TGenEvent>(this TGenEvent gameEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            PublishConfig<TGenEvent>.Setting.AddFilter(filter);
            return gameEvent;
        }

        /// <summary>
        /// Excludes a subscriber from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscriber">The subscriber to exclude.</param>
        /// <returns>The event.</returns>
        public static TGenEvent ExcludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeSubscriber(subscriber));
            return gameEvent;
        }

        /// <summary>
        /// Excludes a list of subscribers from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscribers">The list of subscribers to exclude.</param>
        /// <returns>The event.</returns>
        public static TGenEvent ExcludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeSubscribers(subscribers));
            return gameEvent;
        }

        /// <summary>
        /// Allows only a specific subscriber to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscriber">The subscriber to allow.</param>
        /// <returns>The event.</returns>
        public static TGenEvent OnlySubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlySubscriber(subscriber));
            return gameEvent;
        }

        /// <summary>
        /// Allows only a list of subscribers to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscribers">The list of subscribers to allow.</param>
        /// <returns>The event.</returns>
        public static TGenEvent OnlySubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlySubscribers(subscribers));
            return gameEvent;
        }

        /// <summary>
        /// Allows only subscribers of a specific type to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <typeparam name="TSubscriber">The type of subscriber to allow.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The event.</returns>
        public static TGenEvent OnlyType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlyType<TSubscriber>());
            return gameEvent;
        }

        /// <summary>
        /// Excludes subscribers of a specific type from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <typeparam name="TSubscriber">The type of subscriber to exclude.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The event.</returns>
        public static TGenEvent ExcludeType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeType<TSubscriber>());
            return gameEvent;
        }

        /// <summary>
        /// Invokes the event publishing for one subscriber type. Used by generated publisher code; config is the one passed from the Publish entry for this publish.
        /// </summary>
        /// <typeparam name="TSubscriber">The type of subscriber.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="config">The publish config for this Publish (filters, Cancelable).</param>
        /// <returns>True if all subscribers successfully handled the event; false if any subscriber cancelled propagation (event stopped before reaching all subscribers)</returns>
        public static bool Invoke<TSubscriber, TGenEvent>(this TGenEvent gameEvent, PublishConfig<TGenEvent> config)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var subscribers = GenEventRegistry<TGenEvent, TSubscriber>.Subscribers;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            for (int i = 0; i < subscribers.Count; i++)
            {
                var subscriber = subscribers[i];
                if (config.IsFiltered(subscriber))
                    continue;

                var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!config.Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }

        /// <summary>
        /// Invokes the event publishing for one subscriber type asynchronously.
        /// If the registry has an async delegate, awaits it per subscriber; otherwise calls the sync delegate directly (no Task.FromResult).
        /// </summary>
        /// <typeparam name="TSubscriber">The type of subscriber.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="config">The publish config for this Publish (filters, Cancelable).</param>
        /// <returns>Task that completes with true if propagation should continue; false if cancelled.</returns>
        public static async Task<bool> InvokeAsync<TSubscriber, TGenEvent>(this TGenEvent gameEvent, PublishConfig<TGenEvent> config)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var subscribers = GenEventRegistry<TGenEvent, TSubscriber>.Subscribers;
            var genEventAsync = GenEventRegistry<TGenEvent, TSubscriber>.GenEventAsync;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            if (genEventAsync != null)
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (config.IsFiltered(subscriber))
                        continue;

                    var shouldContinue = await genEventAsync(gameEvent, subscriber);

                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }
            }
            else
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (config.IsFiltered(subscriber))
                        continue;

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }
            }

            return completed;
        }
    }
}