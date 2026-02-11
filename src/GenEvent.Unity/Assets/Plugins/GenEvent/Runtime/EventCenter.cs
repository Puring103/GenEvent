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
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            var result = publisher.Publish(gameEvent);
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
        public bool Cancelable { get; private set; } = false;
        private List<Predicate<object>> SubscriberFilters { get; set; } = new(16);

        private static PublishConfig<TGenEvent> _instance;
        public static PublishConfig<TGenEvent> Instance
        {
            get
            {
                _instance ??= new PublishConfig<TGenEvent>();
                return _instance;
            }
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