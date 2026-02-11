using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    public static class EventCenter
    {
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            var result = publisher.Publish(gameEvent, subscriber);
            PublishConfig<TGenEvent>.Instance.Clear();

            return result;
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

        public static void Cancelable<TGenEvent>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Instance.SetCancelable();
        }

        public static TGenEvent WithFilter<TGenEvent>(this TGenEvent gameEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
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
                if (PublishConfig<TGenEvent>.Instance.IsFiltered(subscriber))
                    continue;

                var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!PublishConfig<TGenEvent>.Instance.Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }
    }

    public class PublishConfig<TGenEvent>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private bool _cancelable = false;
        public bool Cancelable => _cancelable;
        private List<Predicate<object>> _subscriberFilters { get; set; } = new(16);

        private static PublishConfig<TGenEvent> instance;

        public static PublishConfig<TGenEvent> Instance
        {
            get
            {
                instance ??= new PublishConfig<TGenEvent>();
                return instance;
            }
        }

        public void Clear()
        {
            _cancelable = false;
            _subscriberFilters.Clear();
        }

        public void SetCancelable()
        {
            _cancelable = true;
        }

        public void AddFilter(Predicate<object> filter)
        {
            _subscriberFilters.Add(filter);
        }

        public bool IsFiltered(object subscriber)
        {
            for (int i = 0; i < _subscriberFilters.Count; i++)
            {
                if (_subscriberFilters[i](subscriber))
                {
                    return true;
                }
            }

            return false;
        }
    }
}