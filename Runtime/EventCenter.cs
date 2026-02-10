using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {
        public static void Publish<TGenEvent>(this TGenEvent gameEvent, object subscriber)
            where TGenEvent : struct, IGenEvent<TGenEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TGenEvent)];
            publisher?.Publish(gameEvent, subscriber);
        }

        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TestSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }

        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
            where TSubscriber : class
        {
            if (BaseSubscriberRegistry.Subscribers.TryGetValue(typeof(TestSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }
    }
}