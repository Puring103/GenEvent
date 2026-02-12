using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent
{
    public static class PublisherHelper
    {
        public static bool Publish<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Push();
            try
            {
                var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
                return publisher.Publish(gameEvent);
            }
            finally
            {
                PublishConfig<TGenEvent>.Pop();
            }
        }

        public static TGenEvent Cancelable<TGenEvent>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.SetCancelable();
            return gameEvent;
        }

        public static TGenEvent WithFilter<TGenEvent>(this TGenEvent gameEvent, Predicate<object> filter)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            PublishConfig<TGenEvent>.Setting.AddFilter(filter);
            return gameEvent;
        }

        public static TGenEvent ExcludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeSubscriber(subscriber));
            return gameEvent;
        }

        public static TGenEvent IncludeSubscriber<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlySubscriber(subscriber));
            return gameEvent;
        }

        public static TGenEvent ExcludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeSubscribers(subscribers));
            return gameEvent;
        }

        public static TGenEvent IncludeSubscribers<TGenEvent>(this TGenEvent gameEvent, HashSet<object> subscribers)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlySubscribers(subscribers));
            return gameEvent;
        }

        public static TGenEvent OnlyType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.OnlyType<TSubscriber>());
            return gameEvent;
        }

        public static TGenEvent ExcludeType<TGenEvent, TSubscriber>(this TGenEvent gameEvent)
            where TGenEvent : struct, IGenEvent<TGenEvent>
            where TSubscriber : class
        {
            PublishConfig<TGenEvent>.Setting.AddFilter(GenEventFilters.ExcludeType<TSubscriber>());
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
                if (PublishConfig<TGenEvent>.Current.IsFiltered(subscriber))
                    continue;

                var shouldContinue = genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!PublishConfig<TGenEvent>.Current.Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }
    }
}