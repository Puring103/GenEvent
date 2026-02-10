using System;
using System.Collections.Generic;
using GenEvent.Runtime.Interface;

namespace GenEvent.Runtime
{
    public delegate void GameEventDelegate<in TEvent, in TSubscriber>(TEvent gameEvent, TSubscriber subscriber);


    public static class GameEventRegistry<TEvent, TSubscriber>
        where TEvent : struct, IGameEvent<TEvent>
    {
        private static readonly List<TSubscriber> _subscribers = new();
        private static event GameEventDelegate<TEvent, TSubscriber> _gameEvent;
        public static bool IsInitialized => _gameEvent != null;

        public static void Initialize(GameEventDelegate<TEvent, TSubscriber> gameEventDelegate)
        {
            _gameEvent = gameEventDelegate;
        }

        public static void Register(TSubscriber observer)
        {
            _subscribers.Add(observer);
        }

        public static void UnRegister(TSubscriber observer)
        {
            _subscribers.Remove(observer);
        }

        public static void Invoke(TEvent gameEvent)
        {
            foreach (var subscriber in _subscribers)
            {
                _gameEvent?.Invoke(gameEvent, subscriber);
            }
        }
    }
}