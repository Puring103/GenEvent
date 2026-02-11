using GenEvent.Runtime.example;
using GenEvent.Runtime;
using System.CodeDom.Compiler;
[GeneratedCode("GenEvent","V0.5")]
public class TestSubscriberSubscriberRegistry : BaseSubscriberRegistry
{
    static TestSubscriberSubscriberRegistry()
    {
        GenEventRegistry<ExampleEvent, TestSubscriber>.Initialize((gameEvent, subscriber) =>
        {
            subscriber.OnEvent(gameEvent);
            return true;
        });

    }

    public override void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StartListening<TSubscriber, ExampleEvent>(self);

    }

    public override void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StopListening<TSubscriber, ExampleEvent>(self);

    }
}
