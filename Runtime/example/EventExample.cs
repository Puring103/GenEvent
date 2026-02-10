using GenEvent.Runtime.Interface;

namespace GenEvent.Runtime.example
{
    public partial struct EventExample : IGameEvent<EventExample>
    {
        public string Message { get; set; }
    }

    public partial struct EventExample2 : IGameEvent<EventExample2>
    {
        public int number;
    }
}