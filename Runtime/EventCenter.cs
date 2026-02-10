using System.Collections.Generic;
using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {

        public static bool Publish<TGenEvent>(this TGenEvent gameEvent, object subscriber, bool cancelable = false)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            return publisher != null && publisher.Publish(gameEvent, subscriber, cancelable);
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
    }

    public class PublishConfig
    {
        public bool Cancelable { get; set; } = false;
        public List<ISubscriberFilter> SubscriberFilters { get; set; } = new(1);
    }

    public interface ISubscriberFilter
    {
        bool CanFilter(object subscriber);
    }
}