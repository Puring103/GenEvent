using GenEvent.Runtime.example;
using GenEvent.Runtime.Interface;

namespace GenEvent.Runtime.example
{
    public partial struct EventExample: IInvokable<EventExample>
    {
        public void Invoke(EventExample @event, object emitter)
        {
            GameEventRegistry<EventExample, EventExample>.Invoke(@event);
        }
    }
}
