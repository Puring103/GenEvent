using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

namespace GenEvent.Runtime.Interface
{
    public interface IPublisher
    {
        public static Dictionary<Type, IPublisher> Publishers = new()
        {
            [typeof(EventExample)]= new EventExampleInvoker()
        };

        public void Publish<TEvent>(TEvent @event, object emitter)
            where TEvent : struct, IGameEvent<TEvent>;
    }
}