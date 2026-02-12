using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    /// <summary>
    /// Configuration for event publishing.
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
        /// Stack of publish configs for supporting nested publish calls.
        /// </summary>
        private static readonly Stack<PublishConfig<TGenEvent>> Stack = new(PoolCapacity);

        /// <summary>
        /// Pool of publish configs to avoid allocating new publish configs.
        /// </summary>
        private static readonly List<PublishConfig<TGenEvent>> Pool = new(PoolCapacity);

        private static PublishConfig<TGenEvent> _setting;
        private static PublishConfig<TGenEvent> _mainInstance;

        /// <summary>
        /// The config currently being set up. After completing configuration, call <see cref="Push"/> to push it onto the stack and obtain a new config from the pool.
        /// </summary>
        public static PublishConfig<TGenEvent> Setting
        {
            get
            {
                if (_setting == null)
                {
                    _setting = new PublishConfig<TGenEvent>();
                    _mainInstance = _setting;
                }
                return _setting;
            }
        }

        /// <summary>
        /// The config used during publish calls. Returns the top of the stack, or the current config being set up if the stack is empty.
        /// </summary>
        public static PublishConfig<TGenEvent> Current
        {
            get
            {
                if (Stack.Count > 0)
                    return Stack.Peek();
                if (_setting == null)
                {
                    _setting = new PublishConfig<TGenEvent>();
                    _mainInstance = _setting;
                }
                return _setting;
            }
        }

        /// <summary>
        /// Gets a publish config from the pool.
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
        /// Returns a publish config to the pool.
        /// </summary>
        /// <param name="config">The publish config to return to the pool.</param>
        private static void ReturnToPool(PublishConfig<TGenEvent> config)
        {
            if (config == _mainInstance)
                return;
            config.Clear();
            if (Pool.Count < PoolCapacity)
                Pool.Add(config);
        }

        /// <summary>
        /// Call after finishing configuration: pushes the current <see cref="Setting"/> onto the stack, and obtains a new config from the pool to assign to Setting.
        /// </summary>
        public static void Push()
        {
            if (_setting == null)
            {
                _setting = new PublishConfig<TGenEvent>();
                _mainInstance = _setting;
            }
            Stack.Push(_setting);
            _setting = GetFromPool();
        }

        /// <summary>
        /// Call after publishing ends: pops the stack and returns the config that was just used to the pool.
        /// </summary>
        public static void Pop()
        {
            var used = Stack.Pop();
            ReturnToPool(used);
            _setting = Stack.Count > 0 ? Stack.Peek() : _mainInstance;
            if (Stack.Count == 0)
                _setting.Clear();
        }

        /// <summary>
        /// Clears the publish config.
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