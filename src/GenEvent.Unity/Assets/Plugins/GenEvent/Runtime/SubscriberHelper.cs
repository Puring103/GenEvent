using GenEvent.Interface;

namespace GenEvent
{
    public static class SubscriberHelper
    {
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
}