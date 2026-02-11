using System;
using System.Collections.Generic;

namespace GenEvent.Interface
{
    public abstract partial class BaseEventPublisher
    {
        public static readonly Dictionary<Type, BaseEventPublisher> Publishers = new();

        public abstract bool Publish<TGenEvent>(TGenEvent @event, object emitter)
            where TGenEvent : struct, IGenEvent<TGenEvent>;
    }
}
