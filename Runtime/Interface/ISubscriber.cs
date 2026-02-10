using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;
using GenEvent.Runtime.gen;

namespace GenEvent.Runtime.Interface
{
    public abstract class ISubscriber
    {
        public static Dictionary<Type, ISubscriber> Subscribers = new()
        {
            [typeof(TestSubscriber)] = new TestSubscriberContainer()
        };

        public abstract void StartListening<TSubscriber>(TSubscriber self);
        public abstract void StopListening<TSubscriber>(TSubscriber self);
    }
}