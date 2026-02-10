using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class ExampleEventPublisher : BaseEventPublisher
{
    public override void Publish<TGenEvent>(TGenEvent @event, object emitter)
    {
        GenEventRegistry<TGenEvent, TestSubscriber>.Invoke(@event);
    }
}
