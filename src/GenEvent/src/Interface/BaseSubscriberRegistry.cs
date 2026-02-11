using System;
using System.Collections.Generic;

namespace GenEvent.Interface;

public abstract class BaseSubscriberRegistry
{
    public static readonly Dictionary<Type, BaseSubscriberRegistry> Subscribers = new();

    public abstract void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class;

    public abstract void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class;

    public static void StartListening<TSubscriber, TGenEvent>(TSubscriber self)
        where TGenEvent : struct, IGenEvent<TGenEvent>
        where TSubscriber : class
    {
        GenEventRegistry<TGenEvent, TSubscriber>.Register(self);
    }

    public static void StopListening<TSubscriber, TGenEvent>(TSubscriber self)
        where TGenEvent : struct, IGenEvent<TGenEvent>
        where TSubscriber : class
    {
        GenEventRegistry<TGenEvent, TSubscriber>.UnRegister(self);
    }
}