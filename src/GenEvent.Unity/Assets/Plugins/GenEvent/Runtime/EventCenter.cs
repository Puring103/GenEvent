using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    public static class EventCenter
    {
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.PushLayer();
            try
            {
                var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
                return publisher.Publish(gameEvent);
            }
            finally
            {
                PublishConfig<TGenEvent>.PopLayer();
            }
        }

        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }

        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }

        public static void StartListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StartListening<TSubscriber, TGenEvent>(subscriber);
        }

        public static void StopListening<TSubscriber, TGenEvent>(this TSubscriber subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            BaseSubscriberRegistry.StopListening<TSubscriber, TGenEvent>(subscriber);
        }

        public static TGenEvent Cancelable<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.SetCancelable();
            return gameEvent;
        }

        public static TGenEvent WithFilter<TGenEvent>(this TGenEvent gameEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if(filter== null) 
                throw new ArgumentNullException(nameof(filter));
            
            PublishConfig<TGenEvent>.Instance.AddFilter(filter);
            return gameEvent;
        }

        public static TGenEvent ExcludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeSubscriber(subscriber));
            return gameEvent;
        }

        public static TGenEvent IncludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.IncludeSubscriber(subscriber));
            return gameEvent;
        }

        public static TGenEvent ExcludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeSubscribers(subscribers));
            return gameEvent;
        }

        public static TGenEvent IncludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.IncludeSubscribers(subscribers));
            return gameEvent;
        }

        public static TGenEvent OnlyType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.OnlyType<TSubscriber>());
            return gameEvent;
        }

        public static TGenEvent ExcludeType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeType<TSubscriber>());
            return gameEvent;
        }

        public static bool Invoke<TSubscriber, TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            var completed = true;
            var subscribers = GenEventRegistry<TGenEvent, TSubscriber>.Subscribers;
            var genEvent = GenEventRegistry<TGenEvent, TSubscriber>.GenEvent;

            for (int i = 0; i < subscribers.Count; i++)
            {
                var subscriber = subscribers[i];
                if (PublishConfig<TGenEvent>.GetCurrentConfig().IsFiltered(subscriber))
                    continue;

                var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!PublishConfig<TGenEvent>.GetCurrentConfig().Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }
    }

    public class PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private const int PoolCapacity = 16;

        public bool Cancelable { get; private set; } = false;
        private List<Predicate<object>> SubscriberFilters { get; set; } = new(16);

        private static PublishConfig<TGenEvent> _instance;
        private static PublishConfig<TGenEvent> _mainInstance;
        private static readonly Stack<PublishConfig<TGenEvent>> _stack = new();
        private static readonly List<PublishConfig<TGenEvent>> _pool = new(PoolCapacity);

        public static PublishConfig<TGenEvent> Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PublishConfig<TGenEvent>();
                    _mainInstance = _instance;
                }
                return _instance;
            }
        }

        /// <summary>Config used for the current Publish (stack top when in a Publish, otherwise Instance).</summary>
        public static PublishConfig<TGenEvent> GetCurrentConfig()
        {
            if (_stack.Count > 0)
                return _stack.Peek();
            if (_instance == null)
            {
                _instance = new PublishConfig<TGenEvent>();
                _mainInstance = _instance;
            }
            return _instance;
        }

        private static PublishConfig<TGenEvent> GetFromPool()
        {
            if (_pool.Count > 0)
            {
                var i = _pool.Count - 1;
                var config = _pool[i];
                _pool.RemoveAt(i);
                config.Clear();
                return config;
            }
            return new PublishConfig<TGenEvent>();
        }

        private static void ReturnToPool(PublishConfig<TGenEvent> config)
        {
            if (config == _mainInstance)
                return;
            config.Clear();
            if (_pool.Count < PoolCapacity)
                _pool.Add(config);
        }

        /// <summary>Call at Publish() entry: push current Instance, then replace Instance with a pooled config.</summary>
        internal static void PushLayer()
        {
            if (_instance == null)
            {
                _instance = new PublishConfig<TGenEvent>();
                _mainInstance = _instance;
            }
            _stack.Push(_instance);
            _instance = GetFromPool();
        }

        /// <summary>Call at Publish() exit: pop layer, return current Instance to pool, restore Instance; clear if outermost.</summary>
        internal static void PopLayer()
        {
            var popped = _stack.Pop();
            ReturnToPool(_instance);
            _instance = popped;
            if (_stack.Count == 0)
                _instance.Clear();
        }

        public void Clear()
        {
            Cancelable = false;
            SubscriberFilters.Clear();
        }

        public void SetCancelable()
        {
            Cancelable = true;
        }

        public void AddFilter(Predicate<object> filter)
        {
            SubscriberFilters.Add(filter);
        }

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