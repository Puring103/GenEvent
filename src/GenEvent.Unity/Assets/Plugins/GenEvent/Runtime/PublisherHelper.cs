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
        /// Publishes an event using the default empty publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>True if all subscribers successfully handled the event; false if any subscriber cancelled propagation (event stopped before reaching all subscribers)</returns>
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            return PublishCore(gameEvent, default, nameof(Publish));
        }

        /// <summary>
        /// Publishes an event asynchronously using the default empty publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>Task that completes with true if all subscribers handled the event; false if any cancelled propagation.</returns>
        public static async Task<bool> PublishAsync<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            return await PublishAsyncCore(gameEvent, default, nameof(PublishAsync));
        }

        /// <summary>
        /// Publishes an already configured event.
        /// Reusing the same configured value publishes with the same configuration again.
        /// </summary>
        public static bool Publish<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            return PublishCore(configuredEvent.Event, configuredEvent.Config, nameof(Publish));
        }

        /// <summary>
        /// Publishes an already configured event asynchronously.
        /// Reusing the same configured value publishes with the same configuration again.
        /// </summary>
        public static async Task<bool> PublishAsync<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            return await PublishAsyncCore(configuredEvent.Event, configuredEvent.Config, nameof(PublishAsync));
        }

        /// <summary>
        /// Sets the publish config as cancelable for a new configured event.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> Cancelable<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetCancelable();
            return configuredEvent;
        }

        /// <summary>
        /// Sets the publish config as cancelable for an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> Cancelable<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            configuredEvent.Config.SetCancelable();
            return configuredEvent;
        }

        /// <summary>
        /// Adds a filter to a new configured event.
        /// Filter returns true if the subscriber should be filtered out.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="filter">The filter to add.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> WithFilter<TGenEvent>(this TGenEvent gameEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.AddFilter(filter);
            return configuredEvent;
        }

        /// <summary>
        /// Adds a filter to an already configured event.
        /// Filter returns true if the subscriber should be filtered out.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> WithFilter<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            configuredEvent.Config.AddFilter(filter);
            return configuredEvent;
        }

        /// <summary>
        /// Excludes a subscriber from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscriber">The subscriber to exclude.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> ExcludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetExcludeSubscriber(subscriber);
            return configuredEvent;
        }

        /// <summary>
        /// Excludes a subscriber from an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> ExcludeSubscriber<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            configuredEvent.Config.SetExcludeSubscriber(subscriber);
            return configuredEvent;
        }

        /// <summary>
        /// Excludes a list of subscribers from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscribers">The list of subscribers to exclude.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> ExcludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetExcludeSubscribers(subscribers);
            return configuredEvent;
        }

        /// <summary>
        /// Excludes a list of subscribers from an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> ExcludeSubscribers<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            configuredEvent.Config.SetExcludeSubscribers(subscribers);
            return configuredEvent;
        }

        /// <summary>
        /// Allows only a specific subscriber to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscriber">The subscriber to allow.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> OnlySubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetOnlySubscriber(subscriber);
            return configuredEvent;
        }

        /// <summary>
        /// Allows only a specific subscriber to pass through for an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> OnlySubscriber<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            configuredEvent.Config.SetOnlySubscriber(subscriber);
            return configuredEvent;
        }

        /// <summary>
        /// Allows only a list of subscribers to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="subscribers">The list of subscribers to allow.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> OnlySubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetOnlySubscribers(subscribers);
            return configuredEvent;
        }

        /// <summary>
        /// Allows only a list of subscribers to pass through for an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> OnlySubscribers<TGenEvent>(this ConfiguredEvent<TGenEvent> configuredEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            configuredEvent.Config.SetOnlySubscribers(subscribers);
            return configuredEvent;
        }

        /// <summary>
        /// Allows only subscribers of a specific type to pass through.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <typeparam name="TSubscriber">The type of subscriber to allow.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> OnlyType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetOnlyType(GenEventFilters.GetOnlyTypeRuleType<TSubscriber>());
            return configuredEvent;
        }

        /// <summary>
        /// Allows only subscribers of a specific type to pass through for an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> OnlyType<TGenEvent, TSubscriber>(this ConfiguredEvent<TGenEvent> configuredEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            configuredEvent.Config.SetOnlyType(GenEventFilters.GetOnlyTypeRuleType<TSubscriber>());
            return configuredEvent;
        }

        /// <summary>
        /// Excludes subscribers of a specific type from the publish config.
        /// </summary>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <typeparam name="TSubscriber">The type of subscriber to exclude.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <returns>The configured event.</returns>
        public static ConfiguredEvent<TGenEvent> ExcludeType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var configuredEvent = new ConfiguredEvent<TGenEvent>(gameEvent, default);
            configuredEvent.Config.SetExcludeType(GenEventFilters.GetExcludeTypeRuleType<TSubscriber>());
            return configuredEvent;
        }

        /// <summary>
        /// Excludes subscribers of a specific type from an already configured event.
        /// </summary>
        public static ConfiguredEvent<TGenEvent> ExcludeType<TGenEvent, TSubscriber>(this ConfiguredEvent<TGenEvent> configuredEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            configuredEvent.Config.SetExcludeType(GenEventFilters.GetExcludeTypeRuleType<TSubscriber>());
            return configuredEvent;
        }

        /// <summary>
        /// Returns true when a generated publisher exists for the event type.
        /// </summary>
        public static bool HasPublisher<TGenEvent>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            return BaseEventPublisher.Publishers.ContainsKey(typeof(TGenEvent));
        }

        private static bool PublishCore<TGenEvent>(TGenEvent gameEvent, PublishConfig<TGenEvent> config, string operationName)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (!BaseEventPublisher.Publishers.TryGetValue(typeof(TGenEvent), out var publisher))
                throw GenEventRuntimeGuard.CreateMissingPublisherException(typeof(TGenEvent), operationName);

            return publisher.Publish(gameEvent, config);
        }

        private static async Task<bool> PublishAsyncCore<TGenEvent>(TGenEvent gameEvent, PublishConfig<TGenEvent> config, string operationName)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (!BaseEventPublisher.Publishers.TryGetValue(typeof(TGenEvent), out var publisher))
                throw GenEventRuntimeGuard.CreateMissingPublisherException(typeof(TGenEvent), operationName);

            return await publisher.PublishAsync(gameEvent, config);
        }

        /// <summary>
        /// Invokes the event publishing for one subscriber type. Used by generated publisher code; config is the one passed from the Publish entry for this publish.
        /// </summary>
        /// <typeparam name="TSubscriber">The type of subscriber.</typeparam>
        /// <typeparam name="TGenEvent">The event type.</typeparam>
        /// <param name="gameEvent">The event to publish.</param>
        /// <param name="config">The publish config for this Publish (filters, Cancelable).</param>
        /// <returns>True if all subscribers successfully handled the event; false if any subscriber cancelled propagation (event stopped before reaching all subscribers)</returns>
        public static bool Invoke<TSubscriber, TGenEvent>(this TGenEvent gameEvent, PublishConfig<TGenEvent> config, IReadOnlyList<TSubscriber> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            if (config.IsDefault)
            {
                if (genEvent == null)
                {
                    return true;
                }

                for (int i = 0; i < subscribers.Count; i++)
                {
                    genEvent(gameEvent, subscribers[i]);
                }

                return true;
            }

            if (config.TryGetOnlySubscriber(out var onlySubscriber))
            {
                if (onlySubscriber is TSubscriber typedSubscriber &&
                    GenEventRegistry<TGenEvent, TSubscriber>.ContainsSubscriber(typedSubscriber))
                {
                    var shouldContinue = genEvent?.Invoke(gameEvent, typedSubscriber) ?? true;
                    return !config.Cancelable || shouldContinue;
                }

                return true;
            }

            if (config.TryGetExcludeSubscriber(out var excludedSubscriber))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (ReferenceEquals(subscriber, excludedSubscriber))
                    {
                        continue;
                    }

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }

                return completed;
            }

            if (config.TryGetOnlySubscribers(out var onlySubscribers))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (!onlySubscribers.Contains(subscriber))
                    {
                        continue;
                    }

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }

                return completed;
            }

            if (config.TryGetExcludeSubscribers(out var excludedSubscribers))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (excludedSubscribers.Contains(subscriber))
                    {
                        continue;
                    }

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }

                return completed;
            }

            if (config.TryGetOnlyType(out var onlyType))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (!onlyType.IsInstanceOfType(subscriber))
                    {
                        continue;
                    }

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }

                return completed;
            }

            if (config.TryGetExcludeType(out var excludedType))
            {
                for (int i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];
                    if (excludedType.IsInstanceOfType(subscriber))
                    {
                        continue;
                    }

                    var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                    if (!config.Cancelable || shouldContinue) continue;
                    completed = false;
                    break;
                }

                return completed;
            }

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
        public static async Task<bool> InvokeAsync<TSubscriber, TGenEvent>(this TGenEvent gameEvent, PublishConfig<TGenEvent> config, IReadOnlyList<TSubscriber> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var genEventAsync = GenEventRegistry<TGenEvent, TSubscriber>.GenEventAsync;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            if (config.IsDefault && genEventAsync == null)
            {
                if (genEvent == null)
                {
                    return true;
                }

                for (int i = 0; i < subscribers.Count; i++)
                {
                    genEvent(gameEvent, subscribers[i]);
                }

                return true;
            }

            if (config.TryGetOnlySubscriber(out var onlySubscriber))
            {
                if (onlySubscriber is not TSubscriber typedSubscriber ||
                    !GenEventRegistry<TGenEvent, TSubscriber>.ContainsSubscriber(typedSubscriber))
                {
                    return true;
                }

                if (genEventAsync != null)
                {
                    var shouldContinue = await genEventAsync(gameEvent, typedSubscriber);
                    return !config.Cancelable || shouldContinue;
                }

                var syncShouldContinue = genEvent?.Invoke(gameEvent, typedSubscriber) ?? true;
                return !config.Cancelable || syncShouldContinue;
            }

            if (config.TryGetExcludeSubscriber(out var excludedSubscriber))
            {
                if (genEventAsync != null)
                {
                    for (int i = 0; i < subscribers.Count; i++)
                    {
                        var subscriber = subscribers[i];
                        if (ReferenceEquals(subscriber, excludedSubscriber))
                        {
                            continue;
                        }

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
                        if (ReferenceEquals(subscriber, excludedSubscriber))
                        {
                            continue;
                        }

                        var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                        if (!config.Cancelable || shouldContinue) continue;
                        completed = false;
                        break;
                    }
                }

                return completed;
            }

            if (config.TryGetOnlySubscribers(out var onlySubscribers))
            {
                if (genEventAsync != null)
                {
                    for (int i = 0; i < subscribers.Count; i++)
                    {
                        var subscriber = subscribers[i];
                        if (!onlySubscribers.Contains(subscriber))
                        {
                            continue;
                        }

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
                        if (!onlySubscribers.Contains(subscriber))
                        {
                            continue;
                        }

                        var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                        if (!config.Cancelable || shouldContinue) continue;
                        completed = false;
                        break;
                    }
                }

                return completed;
            }

            if (config.TryGetExcludeSubscribers(out var excludedSubscribers))
            {
                if (genEventAsync != null)
                {
                    for (int i = 0; i < subscribers.Count; i++)
                    {
                        var subscriber = subscribers[i];
                        if (excludedSubscribers.Contains(subscriber))
                        {
                            continue;
                        }

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
                        if (excludedSubscribers.Contains(subscriber))
                        {
                            continue;
                        }

                        var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                        if (!config.Cancelable || shouldContinue) continue;
                        completed = false;
                        break;
                    }
                }

                return completed;
            }

            if (config.TryGetOnlyType(out var onlyType))
            {
                if (genEventAsync != null)
                {
                    for (int i = 0; i < subscribers.Count; i++)
                    {
                        var subscriber = subscribers[i];
                        if (!onlyType.IsInstanceOfType(subscriber))
                        {
                            continue;
                        }

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
                        if (!onlyType.IsInstanceOfType(subscriber))
                        {
                            continue;
                        }

                        var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                        if (!config.Cancelable || shouldContinue) continue;
                        completed = false;
                        break;
                    }
                }

                return completed;
            }

            if (config.TryGetExcludeType(out var excludedType))
            {
                if (genEventAsync != null)
                {
                    for (int i = 0; i < subscribers.Count; i++)
                    {
                        var subscriber = subscribers[i];
                        if (excludedType.IsInstanceOfType(subscriber))
                        {
                            continue;
                        }

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
                        if (excludedType.IsInstanceOfType(subscriber))
                        {
                            continue;
                        }

                        var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;
                        if (!config.Cancelable || shouldContinue) continue;
                        completed = false;
                        break;
                    }
                }

                return completed;
            }

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
