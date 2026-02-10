using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {
        public static void Publish<TEvent>(this TEvent gameEvent, object subscriber)
            where TEvent : struct, IGameEvent<TEvent>
        {
            var publisher= IPublisher.Publishers[typeof(TEvent)];
            publisher?.Publish(gameEvent, subscriber);
        }

        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
        {
            if (ISubscriber.Subscribers.TryGetValue(typeof(TestSubscriber), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }
        
        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
        {
            if (ISubscriber.Subscribers.TryGetValue(typeof(TestSubscriber), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }
    }
}