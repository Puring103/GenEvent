using GenEvent.Interface;

namespace GenEvent
{
    public struct ExampleEvent : IGenEvent<ExampleEvent>
    {
        public string Message { get; set; }
    }

    public struct ExampleEvent2 : IGenEvent<ExampleEvent2>
    {
        public int Number;
    }
}


