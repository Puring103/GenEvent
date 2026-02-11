using GenEvent;

namespace Tests
{
    [SetUpFixture]
    internal static class GenEventTestSetup
    {
        [OneTimeSetUp]
        public static void OneTimeSetup() => GenEventBootstrap.Init();
    }

    /// <summary>
    /// 基础测试：void 返回类型，一个订阅者订阅一个事件，另一个类发布事件，验证事件是否被调用
    /// </summary>
    public class BasicEventTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Subscriber_ReceivesEvent_WhenPublisherPublishes()
        {
            // 1. 创建订阅者并开始监听
            var subscriber = new SimpleSubscriber();
            subscriber.StartListening();

            // 2. 发布事件（由“另一个类”发布，这里用调用方作为 emitter）
            var evt = new SimpleEvent { Message = "Hello from publisher" };
            var published = evt.Publish(this);

            // 3. 验证事件被调用
            Assert.That(published, Is.True, "Publish 应返回 true");
            Assert.That(subscriber.EventCallCount, Is.EqualTo(1), "订阅者应被调用 exactly 1 次");
            Assert.That(subscriber.LastMessage, Is.EqualTo("Hello from publisher"), "消息应正确传递");

            // 4. 停止监听
            subscriber.StopListening();
        }

        [Test]
        public void Subscriber_NotCalled_WhenNotListening()
        {
            var subscriber = new SimpleSubscriber();
            // 不调用 StartListening

            var evt = new SimpleEvent { Message = "Should not be received" };
            evt.Publish(this);

            Assert.That(subscriber.EventCallCount, Is.EqualTo(0));
        }
    }
}
