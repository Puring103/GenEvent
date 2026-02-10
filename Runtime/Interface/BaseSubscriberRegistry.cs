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
    
    public void StartListening<TSubscriber, TEvent>(TSubscriber self) 
        where TEvent : struct, IGameEvent<TEvent>
        where TSubscriber: class
    {
        GameEventRegistry<TEvent, TSubscriber>.Register(self);
    }

    public void StopListening<TSubscriber, TEvent>(TSubscriber self)
        where TEvent : struct, IGameEvent<TEvent>
        where TSubscriber: class
    {
        GameEventRegistry<TEvent, TSubscriber>.UnRegister(self);
    }
}
