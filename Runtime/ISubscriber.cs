namespace GenEvent.Runtime
{
    public interface ISubscriber<in TEvent>
        where TEvent : struct, IGameEvent
    {
        void Invoke(TEvent eventExample);
    }
}