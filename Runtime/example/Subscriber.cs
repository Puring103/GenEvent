using UnityEngine;

namespace GenEvent.Runtime.example
{
    public class Subscriber : MonoBehaviour
    {
        private void Start()
        {
            this.Bind();
        }

        private void OnDestroy()
        {
            this.Unbind();
        }

        [OnGameEvent]
        public void OnEvent(EventExample eventExample)
        {
            Debug.Log(eventExample.Message);
        }
    }

    public class Subscriber_gen : ISubscriber<EventExample>
    {
        private readonly Subscriber subscriber;

        public Subscriber_gen(Subscriber subscriber)
        {
            this.subscriber = subscriber;
        }

        public void Invoke(EventExample eventExample)
        {
            subscriber.OnEvent(eventExample);
        }
    }

    public static class SubscriberExtension
    {
        public static void Bind(this Subscriber subscriber)
        {
            GameEventRegistry<EventExample>.Add(new Subscriber_gen(subscriber));
        }

        public static void Unbind(this Subscriber subscriber)
        {
            GameEventRegistry<EventExample>.Remove(new Subscriber_gen(subscriber));
        }
    }
}