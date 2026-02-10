using System;
using System.Collections.Generic;

namespace GenEvent.Runtime
{
    public static class GameEventRegistry<TEvent>
        where TEvent : struct, IGameEvent
    {
        private static readonly List<ISubscriber<TEvent>> _subscribers = new();

        public static void Add<TSubscriber>(TSubscriber subscriber)
            where TSubscriber : class, ISubscriber<TEvent>
        {
            _subscribers.Add(subscriber);
        }

        public static void Remove<TSubscriber>(TSubscriber subscriber)
            where TSubscriber : class, ISubscriber<TEvent>
        {
            _subscribers.Remove(subscriber);
        }

        public static void Invoke(Object emitter, TEvent gameEvent)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Invoke(gameEvent);
            }
        }
    }
}