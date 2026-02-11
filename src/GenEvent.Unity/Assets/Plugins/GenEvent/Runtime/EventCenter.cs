using System;
using System.Collections.Generic;
using GenEvent.Interface;

namespace GenEvent;

public static class EventCenter
{
    extension<TSubscriber>(TSubscriber subscriber) where TSubscriber : class
    {
        public void StartListening()
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }

        public void StopListening()
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }

        public void StartListening<TGenEvent>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            BaseSubscriberRegistry.StartListening<TSubscriber, TGenEvent>(subscriber);
        }

        public void StopListening<TGenEvent>()
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            BaseSubscriberRegistry.StopListening<TSubscriber, TGenEvent>(subscriber);
        }
    }

    extension<TGenEvent>(TGenEvent gameEvent) where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        public bool Publish()
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
        
        public TGenEvent Cancelable()
        {
            PublishConfig<TGenEvent>.Instance.SetCancelable();
            return gameEvent;
        }

        public TGenEvent WithFilter(Predicate<object> filter)
        {
            if(filter== null) 
                throw new ArgumentNullException(nameof(filter));
            
            PublishConfig<TGenEvent>.Instance.AddFilter(filter);
            return gameEvent;
        }

        public TGenEvent ExcludeSubscriber(object subscriber)
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeSubscriber(subscriber));
            return gameEvent;
        }

        public TGenEvent IncludeSubscriber(object subscriber)
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.IncludeSubscriber(subscriber));
            return gameEvent;
        }

        public TGenEvent ExcludeSubscribers(HashSet<object> subscribers)
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeSubscribers(subscribers));
            return gameEvent;
        }

        public TGenEvent IncludeSubscribers(HashSet<object> subscribers)
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.IncludeSubscribers(subscribers));
            return gameEvent;
        }

        public TGenEvent OnlyType<TSubscriber>()
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.OnlyType<TSubscriber>());
            return gameEvent;
        }

        public TGenEvent ExcludeType<TSubscriber>()
        {
            PublishConfig<TGenEvent>.Instance.AddFilter(GenEventFilters.ExcludeType<TSubscriber>());
            return gameEvent;
        }
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