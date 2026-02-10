using UnityEngine;
using GenEvent.Runtime;
using GenEvent.Runtime.example;
using GenEvent.Runtime.gen;

public class Publisher : MonoBehaviour
{
    public void PublishEvent()
    {
        var exampleEvent = new EventExample {Message = "Hello, World!"};
        exampleEvent.Publish(this);
    }
}