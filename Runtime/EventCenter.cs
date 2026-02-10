using GenEvent.Runtime.Interface;

namespace GenEvent.Runtime
{
    public static class EventCenter
    {
        public static void Publish<TEvent>(this TEvent gameEvent, object subscriber)
            where TEvent : struct, IGameEvent<TEvent>
        {
            (gameEvent as IInvokable<TEvent>)?.Invoke(gameEvent, subscriber);
        }

        public static void StartListening<TSubscriber>(this TSubscriber subscriber)
        {
            if (ISubscriber.Subscribers.TryGetValue(typeof(EventCenter), out var iSubscriber))
            {
                iSubscriber.StartListening(subscriber);
            }
        }
        
        public static void StopListening<TSubscriber>(this TSubscriber subscriber)
        {
            if (ISubscriber.Subscribers.TryGetValue(typeof(EventCenter), out var iSubscriber))
            {
                iSubscriber.StopListening(subscriber);
            }
        }
    }
}