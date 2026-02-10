using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class TestSubscriberRegistry : BaseSubscriberRegistry
{
    static TestSubscriberRegistry()
    {
        GenEventRegistry<ExampleEvent, TestSubscriber>.Initialize((gameEvent, subscriber) =>
        {
            subscriber.OnEvent(gameEvent);
            return true;
        });

        GenEventRegistry<ExampleEvent2, TestSubscriber>.Initialize((gameEvent, subscriber) =>
        {
            return subscriber.OnEvent3(gameEvent);
        });
    }

    public override void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StartListening<TSubscriber, ExampleEvent>(self);
        StartListening<TSubscriber, ExampleEvent2>(self);
    }

    public override void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StopListening<TSubscriber, ExampleEvent>(self);
        StopListening<TSubscriber, ExampleEvent2>(self);
    }
}