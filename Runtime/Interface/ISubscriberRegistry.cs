using System;
using System.Collections.Generic;

public partial interface ISubscriberRegistry
{
    public static readonly Dictionary<Type, ISubscriberRegistry> Subscribers = new();

    public void StartListening<TSubscriber>(TSubscriber self);
    public void StopListening<TSubscriber>(TSubscriber self);
}
