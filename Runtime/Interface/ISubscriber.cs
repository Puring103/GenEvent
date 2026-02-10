using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;
using GenEvent.Runtime.gen;

namespace GenEvent.Runtime.Interface
{
    public interface ISubscriber
    {
        public static Dictionary<Type, ISubscriber> Subscribers = new()
        {
            [typeof(TestSubscriber)] = new TestSubscriberContainer()
        };

        public void StartListening<TSubscriber>(TSubscriber self);
        public void StopListening<TSubscriber>(TSubscriber self);
    }
}