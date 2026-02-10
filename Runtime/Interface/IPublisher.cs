using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

public partial interface IPublisher
{
    public static readonly Dictionary<Type, IPublisher> Publishers = new();

    public void Publish<TEvent>(TEvent @event, object emitter)
        where TEvent : struct, IGameEvent<TEvent>;
}
