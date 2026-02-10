using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class ExampleEventPublisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, object emitter, bool cancelable)
    {
        return GenEventRegistry<TGenEvent, TestSubscriber>.Invoke(@event, cancelable);
    }
}
