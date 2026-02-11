using System;
using System.Collections.Generic;

namespace GenEvent.Runtime
{
    public delegate bool GenEventDelegate<in TGenEvent, in TSubscriber>(TGenEvent gameEvent, TSubscriber subscriber);

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

        public static bool Invoke(TGenEvent gameEvent)
        {
            var completed = true;

            foreach (var subscriber in _subscribers)
            {
                if(PublishConfig<TGenEvent>.Instance.IsFiltered(subscriber))
                    continue;
                
                var shouldContinue = _genEvent?.Invoke(gameEvent, subscriber) ?? true;

                if (!PublishConfig<TGenEvent>.Instance.Cancelable || shouldContinue) continue;
                completed = false;
                break;
            }

            return completed;
        }
    }
}