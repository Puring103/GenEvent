using System;
using System.Collections.Generic;
using GenEvent.Runtime.example;

public partial interface IPublisher
{
    static IPublisher()
    {
        Publishers[typeof(EventExample)] = new EventExampleInvoker();
    }
}
