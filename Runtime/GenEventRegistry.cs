using System;
using System.Collections.Generic;

namespace GenEvent.Runtime
{
    public delegate void GenEventDelegate<in TGenEvent, in TSubscriber>(TGenEvent gameEvent, TSubscriber subscriber);

    public static class GenEventRegistry<TGenEvent, TSubscriber>
        where TGenEvent : struct, IGenEvent<TGenEvent>
    {
        private static readonly List<TSubscriber> _subscribers = new();
        private static event GenEventDelegate<TGenEvent, TSubscriber> _genEvent;

        public static void Initialize(GenEventDelegate<TGenEvent, TSubscriber> genEventDelegate)
        {
            _genEvent = genEventDelegate;
        }

        public static void Register(TSubscriber observer)
        {
            _subscribers.Add(observer);
        }

        public static void UnRegister(TSubscriber observer)
        {
            _subscribers.Remove(observer);
        }

        public static void Invoke(TGenEvent gameEvent)
        {
            foreach (var subscriber in _subscribers)
            {
                _genEvent?.Invoke(gameEvent, subscriber);
            }
        }
    }
}