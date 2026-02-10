using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {
        public static void Publish<TEvent>(this TEvent gameEvent, object subscriber)
            where TEvent : struct, IGameEvent<TEvent>
        {
            var publisher = BaseEventPublisher.Publishers[typeof(TEvent)];
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