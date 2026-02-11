using GenEvent.Runtime.example;
using GenEvent.Runtime;
using System.CodeDom.Compiler;
[GeneratedCode("GenEvent","V0.5")]
public class ExampleEventPublisher : BaseEventPublisher
{
    public override bool Publish<TGenEvent>(TGenEvent @event, object emitter)
    {
        bool completed = false;

        completed = @event.Invoke<TestSubscriber, TGenEvent>();
        if (!completed) return false;

        return true;
    }
}
