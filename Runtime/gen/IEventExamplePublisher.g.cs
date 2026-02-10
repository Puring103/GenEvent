using GenEvent.Runtime;
using GenEvent.Runtime.example;

public struct EventExamplePublisher : IEventPublisher
{
    public void Publish<TEvent>(TEvent @event, object emitter)
        where TEvent : struct, IGameEvent<TEvent>
    {
        GameEventRegistry<TEvent, TestSubscriber>.Invoke(@event);
    }
}
