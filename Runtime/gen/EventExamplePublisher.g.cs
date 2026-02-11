using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class ExampleEventPublisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, object emitter)
    {
        bool completed = false;

        completed = @event.Invoke<TestSubscriber, TGenEvent>();
        if (!completed) return false;

        // completed = @event.Invoke<TestSubscriber, TGenEvent>();
        // if (!completed) return false;

        return true;
    }
}