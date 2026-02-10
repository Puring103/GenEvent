using GenEvent.Runtime.example;

public abstract partial class BaseSubscriberRegistry
{
    static BaseSubscriberRegistry()
    {
        Subscribers[typeof(TestSubscriber)] = new TestSubscriberRegistry();
    }
}
