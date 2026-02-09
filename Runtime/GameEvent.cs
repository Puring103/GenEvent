using System;
using System.Collections.Generic;

namespace GenEvent.Runtime
{
    public delegate void GameEvent<in TObserver, in TEvent>(TObserver observer, TEvent gameEvent)
        where TObserver : notnull
        where TEvent : struct, IGameEvent;

    public static partial class GameEventRegistry<TObserver, TEvent>
        where TEvent : struct, IGameEvent
    {
        private static readonly List<GameEvent<TObserver, TEvent>> _subscribers = new();

        public static void Bind()
        {
        }

        public static void Unbind()
        {
        }

        public static void Publish(TObserver observer, TEvent gameEvent)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Invoke(observer, gameEvent);
            }
        }
    }
}