using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class TestSubscriberRegistry : SubscriberRegistry
{
    static TestSubscriberRegistry()
    {
        GameEventRegistry<EventExample, TestSubscriber>.Initialize((gameEvent, subscriber) =>
        {
            subscriber.OnEvent(gameEvent);
        });

        GameEventRegistry<EventExample2, TestSubscriber>.Initialize((gameEvent, subscriber) =>
        {
            subscriber.OnEvent3(gameEvent);
        });
    }

    public override void StartListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StartListening<TSubscriber, EventExample>(self);
        StartListening<TSubscriber, EventExample2>(self);
    }

    public override void StopListening<TSubscriber>(TSubscriber self)
        where TSubscriber : class
    {
        StopListening<TSubscriber, EventExample>(self);
        StopListening<TSubscriber, EventExample2>(self);
    }
}