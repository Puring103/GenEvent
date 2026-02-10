using System;
using System.Collections.Generic;

public abstract partial class BaseEventPublisher
{
    public static readonly Dictionary<Type, BaseEventPublisher> Publishers = new();

    public abstract bool Publish<TGenEvent>(TGenEvent @event, object emitter, bool cancelable)
        where TGenEvent : struct, IGenEvent<TGenEvent>;
}
