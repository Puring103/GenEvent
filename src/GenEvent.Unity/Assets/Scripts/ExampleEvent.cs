using GenEvent.Interface;

public struct ExampleEvent : IGenEvent<ExampleEvent>
{
    public string Message { get; set; }
}
    
public struct ExampleEvent2 : IGenEvent<ExampleEvent2>
{
    public int number;
}