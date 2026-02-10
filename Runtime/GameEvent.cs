using System;
using GenEvent.Runtime.example;

namespace GenEvent.Runtime
{
    public static class IGameEventExtension
    {
        public static void Publish<TEvent>(this TEvent gameEvent, Object emitter)
            where TEvent : struct, IGameEvent
        {
            GameEventRegistry<TEvent>.Invoke(emitter, gameEvent);
        }
    }
}