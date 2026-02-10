using System;
using System.Collections.Generic;

namespace GenEvent.Runtime
{
    public delegate void GameEventDelegate<in TEvent, in TSubscriber>(TEvent gameEvent, TSubscriber subscriber);


    public static class GameEventRegistry<TEvent, TSubscriber>
        where TEvent : struct, IGameEvent
    {

        private static readonly List<TSubscriber> _subscribers = new();
        private static event GameEventDelegate<TEvent, TSubscriber> _gameEvent;
        public static bool IsInitialized { get; private set; }

        public static void Initialize(GameEventDelegate<TEvent, TSubscriber> gameEventDelegate)
        {
            _gameEvent = gameEventDelegate;
            IsInitialized = true;
        }

        public static void Add(TSubscriber observer)
        {
            _subscribers.Add(observer);
        }

        public static void Remove(TSubscriber observer)
        {
            _subscribers.Remove(observer);
        }

        public static void Invoke(Object emitter, TEvent gameEvent)
        {
            foreach (var subscriber in _subscribers)
            {
                if (_gameEvent != null)
                    _gameEvent.Invoke(gameEvent, subscriber);
            }
        }
    }
}