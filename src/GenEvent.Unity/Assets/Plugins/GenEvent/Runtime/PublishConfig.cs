using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Configuration for event publishing.
    /// Fluent configuration is done on the static <see cref="Setting"/>.
    /// When <see cref="Publish"/> runs, the config is taken via <see cref="TakeForPublish"/> and the static Setting is replaced with a new instance from the pool;
    /// the config is passed through the publish call chain as a parameter. When publish ends, <see cref="ReturnUsedConfig"/> clears and returns the config to the pool.
    /// </summary>
    /// <typeparam name="TGenEvent">The event type.</typeparam>
    public class PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private const int PoolCapacity = 16;

        /// <summary>
        /// Indicates whether the event is cancelable.
        /// </summary>
        public bool Cancelable { get; private set; }

        /// <summary>
        /// List of subscriber filters, used to filter subscribers before publishing the event.
        /// </summary>
        private List<Predicate<object>> SubscriberFilters { get; } = new(16);

        /// <summary>
        /// Pool of publish configs to avoid allocating new publish configs.
        /// </summary>
        private static readonly List<PublishConfig<TGenEvent>> Pool = new(PoolCapacity);

        private static PublishConfig<TGenEvent> _setting;

        /// <summary>
        /// The single static config used for fluent configuration before Publish.
        /// Extension methods such as Cancelable, WithFilter, OnlyType operate on this object.
        /// </summary>
        public static PublishConfig<TGenEvent> Setting
        {
            get
            {
                if (_setting == null)
                    _setting = new PublishConfig<TGenEvent>();
                return _setting;
            }
        }

        /// <summary>
        /// Gets a publish config from the pool (already cleared).
        /// </summary>
        /// <returns>A publish config from the pool.</returns>
        private static PublishConfig<TGenEvent> GetFromPool()
        {
            if (Pool.Count > 0)
            {
                var i = Pool.Count - 1;
                var config = Pool[i];
                Pool.RemoveAt(i);
                config.Clear();
                return config;
            }
            return new PublishConfig<TGenEvent>();
        }

        /// <summary>
        /// Takes the current Setting for use in this Publish, and replaces the static Setting with a new instance from the pool.
        /// Call this at Publish entry; pass the returned config through the publish call chain.
        /// </summary>
        /// <returns>The config to use for this Publish.</returns>
        public static PublishConfig<TGenEvent> TakeForPublish()
        {
            if (_setting == null)
                _setting = new PublishConfig<TGenEvent>();
            var configToUse = _setting;
            _setting = GetFromPool();
            return configToUse;
        }

        /// <summary>
        /// Clears the used config and returns it to the pool. Call when Publish ends so the config can be reused and does not leak previous filter/cancel state.
        /// </summary>
        /// <param name="config">The config that was used for the completed Publish.</param>
        public static void ReturnUsedConfig(PublishConfig<TGenEvent> config)
        {
            if (config == null)
                return;
            config.Clear();
            if (Pool.Count < PoolCapacity)
                Pool.Add(config);
        }

        /// <summary>
        /// Clears the publish config (Cancelable and filters).
        /// </summary>
        private void Clear()
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
