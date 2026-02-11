using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using GenEvent.Runtime.example;
[GeneratedCode("GenEvent","V0.5")]
public abstract partial class BaseEventPublisher
{
    static BaseEventPublisher()
    {
        Publishers[typeof(ExampleEvent)] = new ExampleEventPublisher();

    }
}
