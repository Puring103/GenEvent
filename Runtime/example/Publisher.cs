using UnityEngine;
using GenEvent.Runtime;
using GenEvent.Runtime.example;

public class Publisher : MonoBehaviour
{
    public void PublishEvent()
    {
        var exampleEvent = new ExampleEvent {Message = "Hello, World!"};
        exampleEvent.Publish(this);
    }
}