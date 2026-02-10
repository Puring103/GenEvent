using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class ExampleEventPublisher : BaseEventPublisher
{
    public override void Publish<TEvent>(TEvent @event, object emitter)
    {
        GameEventRegistry<TEvent, TestSubscriber>.Invoke(@event);
    }
}
