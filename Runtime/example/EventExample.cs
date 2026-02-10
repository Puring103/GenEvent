using GenEvent.Runtime.Interface;

namespace GenEvent.Runtime.example
{
    public struct EventExample : IGameEvent<EventExample>
    {
        public string Message { get; set; }
    }

    public struct EventExample2 : IGameEvent<EventExample2>
    {
        public int number;
    }
}