namespace GenEvent.Runtime.Interface
{
    public interface IInvokable<in TEvent>
        where TEvent : struct, IGameEvent<TEvent>
    {
        public void Invoke(TEvent @event, object emitter);
    }
}