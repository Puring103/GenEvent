using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class TestSubscriberRegistry : ISubscriberRegistry
{
    public void StartListening<TSubscriber>(TSubscriber self)
    {
        if (!GameEventRegistry<EventExample, TestSubscriber>.IsInitialized)
        {
            GameEventRegistry<EventExample, TestSubscriber>.Initialize((gameEvent, subscriber) =>
            {
                subscriber.OnEvent(gameEvent);
            });
        }

        GameEventRegistry<EventExample, TestSubscriber>.Register(self as TestSubscriber);

        if (!GameEventRegistry<EventExample2, TestSubscriber>.IsInitialized)
        {
            GameEventRegistry<EventExample2, TestSubscriber>.Initialize((gameEvent, subscriber) =>
            {
                subscriber.OnEvent3(gameEvent);
            });
        }

        GameEventRegistry<EventExample2, TestSubscriber>.Register(self as TestSubscriber);
    }

    public void StopListening<TSubscriber>(TSubscriber self)
    {
        GameEventRegistry<EventExample, TestSubscriber>.UnRegister(self as TestSubscriber);
        GameEventRegistry<EventExample2, TestSubscriber>.UnRegister(self as TestSubscriber);
    }
}
