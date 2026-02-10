using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

public partial interface IEventPublisher
{
    public static readonly Dictionary<Type, IEventPublisher> Publishers = new();

    public void Publish<TEvent>(TEvent @event, object emitter)
        where TEvent : struct, IGameEvent<TEvent>;
}
