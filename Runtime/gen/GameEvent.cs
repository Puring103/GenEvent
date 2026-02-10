using System;
using GenEvent.Runtime.example;

namespace GenEvent.Runtime.gen
{
    public static class IGameEventExtension
    {
        public static void Publish<TEvent>(this TEvent gameEvent, Object emitter)
            where TEvent : struct, IGameEvent
        {
            GameEventRegistry<TEvent, TestSubscriber>.Invoke(emitter, gameEvent);
        }
    }
}