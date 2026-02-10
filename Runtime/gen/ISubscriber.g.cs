using GenEvent.Runtime.example;

public partial interface ISubscriber
{
    static ISubscriber()
    {
        Subscribers[typeof(TestSubscriber)] = new TestSubscriberContainer();
    }
}
