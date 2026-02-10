using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

public partial interface IEventPublisher
{
    static IEventPublisher()
    {
        Publishers[typeof(EventExample)] = new EventExamplePublisher();
    }
}
