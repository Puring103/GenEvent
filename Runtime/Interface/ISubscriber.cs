using System;
using System.Collections.Generic;

public partial interface ISubscriber
{
    public static readonly Dictionary<Type, ISubscriber> Subscribers = new();

    public void StartListening<TSubscriber>(TSubscriber self);
    public void StopListening<TSubscriber>(TSubscriber self);
}
