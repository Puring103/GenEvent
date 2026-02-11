using GenEvent.Runtime.example;
using System.CodeDom.Compiler;
[GeneratedCode("GenEvent","V0.5")]
public abstract partial class BaseSubscriberRegistry
{
    static BaseSubscriberRegistry()
    {
        Subscribers[typeof(TestSubscriber)] = new TestSubscriberSubscriberRegistry();

    }
}
