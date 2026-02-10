using System;
using System.Collections.Generic;
using GenEvent.Runtime;

public abstract partial class BaseSubscriberRegistry
{
    public static readonly Dictionary<Type, BaseSubscriberRegistry> Subscribers = new();

    public abstract void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class;

    public abstract void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class;

    public void StartListening<TSubscriber, TGenEvent>(TSubscriber self)
        where TGenEvent : struct, IGenEvent<TGenEvent>
        where TSubscriber : class
    {
        GenEventRegistry<TGenEvent, TSubscriber>.Register(self);
    }

    public void StopListening<TSubscriber, TGenEvent>(TSubscriber self)
        where TGenEvent : struct, IGenEvent<TGenEvent>
        where TSubscriber : class
    {
        GenEventRegistry<TGenEvent, TSubscriber>.UnRegister(self);
    }
}
