using GenEvent;
using GenEvent.Interface;

namespace GenEvent.Tests;

/// <summary>
/// Core flow tests: basic publish, lifecycle, multi-subscriber, PublishConfig clear.
/// </summary>
[TestFixture]
public class CoreFlowTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void BasicEventPublish_AndHandle_SingleSubscriber_ReceivesEvent()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();

        var evt = new TestEventA { Value = 42 };
        evt.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(subscriber.LastValue, Is.EqualTo(42));
    }

    [Test]
    public void StartListening_WithoutStop_ReceivesEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2));
    }

    [Test]
    public void StopListening_AfterStart_StopsReceivingEvents()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        subscriber.StopListening();
        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should not receive after StopListening");
    }

    [Test]
    public void MultipleStartStop_SameSubscriber_CanReceiveAgainAfterRestart()
    {
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        subscriber.StopListening();
        new TestEventA { Value = 2 }.Publish();
        subscriber.StartListening();
        new TestEventA { Value = 3 }.Publish();

        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2)); // First and third
        Assert.That(subscriber.LastValue, Is.EqualTo(3));
    }

    [Test]
    public void StartListeningPerEventType_OnlyAffectsThatEvent()
    {
        var subscriber = new MultiEventSubscriber();
        subscriber.StartListening<MultiEventSubscriber, TestEventA>();
        subscriber.StartListening<MultiEventSubscriber, TestEventB>();

        new TestEventA { }.Publish();
        new TestEventB { Data = "b" }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(1));
        Assert.That(subscriber.EventBCount, Is.EqualTo(1));

        subscriber.StopListening<MultiEventSubscriber, TestEventA>();
        new TestEventA { }.Publish();
        new TestEventB { Data = "b2" }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(1), "Should not receive TestEventA after StopListening for it");
        Assert.That(subscriber.EventBCount, Is.EqualTo(2));
    }

    [Test]
    public void MultipleSubscribers_SameEvent_AllReceive()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        new TestEventA { Value = 10 }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void DifferentEventTypes_DoNotInterfere()
    {
        var subA = new SubscriberA();
        var subC = new SubscriberC();
        subA.StartListening();
        subC.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventB { Data = "x" }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subC.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void SameSubscriberSubscribesMultipleEvents_EachTriggersCorrectly()
    {
        var subscriber = new MultiEventSubscriber();
        subscriber.StartListening();

        new TestEventA { }.Publish();
        new TestEventB { Data = "d" }.Publish();
        new TestEventA { }.Publish();

        Assert.That(subscriber.EventACount, Is.EqualTo(2));
        Assert.That(subscriber.EventBCount, Is.EqualTo(1));
    }

    [Test]
    public void PublishConfig_ClearedAfterPublish_SecondPublishUnaffectedByFirst()
    {
        var subA = new SubscriberA();
        var subB = new SubscriberB();
        subA.StartListening();
        subB.StartListening();

        // First publish with filter excluding subB
        var evt1 = new TestEventA { Value = 1 }.ExcludeSubscriber(subB);
        evt1.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(1));
        Assert.That(subB.ReceiveCount, Is.EqualTo(0), "subB excluded by filter");

        // Second publish without filter - both should receive
        new TestEventA { Value = 2 }.Publish();

        Assert.That(subA.ReceiveCount, Is.EqualTo(2));
        Assert.That(subB.ReceiveCount, Is.EqualTo(1), "Second publish should not have filter from first");
    }
}
