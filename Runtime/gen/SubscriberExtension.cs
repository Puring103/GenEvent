using GenEvent.Runtime.example;

namespace GenEvent.Runtime.gen
{
    public static class SubscriberExtension
    {
        public static void Bind(this TestSubscriber self)
        {
            if (!GameEventRegistry<EventExample, TestSubscriber>.IsInitialized)
            {
                GameEventRegistry<EventExample, TestSubscriber>.Initialize((gameEvent, subscriber1) =>
                {
                    subscriber1.OnEvent(gameEvent);
                });
            }

            GameEventRegistry<EventExample, TestSubscriber>.Add(self);
        }

        public static void Unbind(this TestSubscriber self)
        {
            GameEventRegistry<EventExample, TestSubscriber>.Remove(self);
        }
    }
}