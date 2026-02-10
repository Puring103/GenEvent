namespace GenEvent.Runtime.example
{
    public struct ExampleEvent : IGameEvent<ExampleEvent>
    {
        public string Message { get; set; }
    }

    public struct ExampleEvent2 : IGameEvent<ExampleEvent2>
    {
        public int number;
    }
}