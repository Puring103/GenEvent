using System;
using System.Collections.Generic;

public abstract partial class BaseEventPublisher
{
    public static readonly Dictionary<Type, BaseEventPublisher> Publishers = new();

    public abstract void Publish<TEvent>(TEvent @event, object emitter)
        where TEvent : struct, IGameEvent<TEvent>;
}
