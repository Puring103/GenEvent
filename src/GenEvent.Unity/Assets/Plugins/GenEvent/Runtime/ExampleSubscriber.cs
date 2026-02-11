using GenEvent.Interface;

namespace GenEvent
{
    public class ExampleSubscriber
    {
        public int Counter;

        [OnEvent]
        public void OnExampleEvent(ExampleEvent ev)
        {
            Counter++;
        }

        [OnEvent(SubscriberPriority.High)]
        public bool OnExampleEvent2(ExampleEvent2 ev)
        {
            Counter += ev.Number;
            return Counter < 10;
        }
    }
}


