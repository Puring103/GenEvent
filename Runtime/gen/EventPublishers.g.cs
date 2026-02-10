using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

public abstract partial class BaseEventPublisher
{
    static BaseEventPublisher()
    {
        Publishers[typeof(ExampleEvent)] = new ExampleEventPublisher();
    }
}
