using GenEvent.Runtime.example;

public partial interface ISubscriberRegistry
{
    static ISubscriberRegistry()
    {
        Subscribers[typeof(TestSubscriber)] = new TestSubscriberRegistry();
    }
}
