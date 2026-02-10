using GenEvent.Runtime.example;

public abstract partial class SubscriberRegistry
{
    static SubscriberRegistry()
    {
        Subscribers[typeof(TestSubscriber)] = new TestSubscriberRegistry();
    }
}
