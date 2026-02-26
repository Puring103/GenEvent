using GenEvent;

namespace Tests;

/// <summary>
/// SubscriptionHandle tests: Dispose cancels subscription, idempotent Dispose,
/// per-event-type handle, using-block lifecycle, backward compatibility with ignored return value.
/// </summary>
[TestFixture]
public class SubscriptionHandleTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void Handle_Dispose_StopsReceivingEvents()
    {
        var subscriber = new SubscriberA();
        var handle = subscriber.StartListening();

        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        handle.Dispose();
        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should not receive after Dispose");
    }

    [Test]
    public void Handle_DisposeMultipleTimes_IsIdempotent()
    {
        var subscriber = new SubscriberA();
        var handle = subscriber.StartListening();

        handle.Dispose();
        Assert.DoesNotThrow(() => handle.Dispose(), "Second Dispose should not throw");
        Assert.DoesNotThrow(() => handle.Dispose(), "Third Dispose should not throw");

        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(0), "Should never have received any event");
    }

    [Test]
    public void Handle_UsingBlock_AutoStopsOnScopeExit()
    {
        var subscriber = new SubscriberA();

        using (subscriber.StartListening())
        {
            new TestEventA { Value = 1 }.Publish();
            Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should receive inside using block");
        }

        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Should not receive after using block exits");
    }

    [Test]
    public void Handle_PerEventType_OnlyStopsThatEvent()
    {
        var subscriber = new MultiEventSubscriber();
        var handleA = subscriber.StartListening<MultiEventSubscriber, TestEventA>();
        var handleB = subscriber.StartListening<MultiEventSubscriber, TestEventB>();

        new TestEventA { }.Publish();
        new TestEventB { Data = "b1" }.Publish();
        Assert.That(subscriber.EventACount, Is.EqualTo(1));
        Assert.That(subscriber.EventBCount, Is.EqualTo(1));

        handleA.Dispose();
        new TestEventA { }.Publish();
        new TestEventB { Data = "b2" }.Publish();
        Assert.That(subscriber.EventACount, Is.EqualTo(1), "EventA stopped after handle Dispose");
        Assert.That(subscriber.EventBCount, Is.EqualTo(2), "EventB still active");

        handleB.Dispose();
        new TestEventA { }.Publish();
        new TestEventB { Data = "b3" }.Publish();
        Assert.That(subscriber.EventACount, Is.EqualTo(1), "EventA still stopped");
        Assert.That(subscriber.EventBCount, Is.EqualTo(2), "EventB stopped after second handle Dispose");
    }

    [Test]
    public void Handle_IgnoreReturnValue_BackwardCompatibleWithManualStop()
    {
        // Existing call sites that discard the return value continue to work unchanged
        var subscriber = new SubscriberA();
        subscriber.StartListening(); // return value intentionally discarded

        new TestEventA { Value = 10 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        subscriber.StopListening(); // manual stop still works
        new TestEventA { Value = 20 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Manual StopListening still effective");
    }

    [Test]
    public void Handle_AfterManualStopListening_DisposeIsNoOp()
    {
        var subscriber = new SubscriberA();
        var handle = subscriber.StartListening();

        subscriber.StopListening(); // manual stop first
        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(0));

        // Dispose on an already-stopped subscription should not throw
        Assert.DoesNotThrow(() => handle.Dispose());
        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(0), "Should still not receive after redundant Dispose");
    }

    [Test]
    public void Handle_Resubscribe_NewHandleIsIndependent()
    {
        var subscriber = new SubscriberA();

        var handle1 = subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));

        handle1.Dispose();
        new TestEventA { Value = 2 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1), "Stopped after handle1 Dispose");

        // Re-subscribe; second handle is independent of handle1
        var handle2 = subscriber.StartListening();
        new TestEventA { Value = 3 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2), "Receives again after re-subscribe");

        handle2.Dispose();
        new TestEventA { Value = 4 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(2), "Stopped after handle2 Dispose");
    }

    [Test]
    public void Handle_MultipleSubscribers_EachHandleIndependent()
    {
        var sub1 = new SubscriberA();
        var sub2 = new SubscriberA();
        var handle1 = sub1.StartListening();
        var handle2 = sub2.StartListening();

        new TestEventA { Value = 1 }.Publish();
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));

        handle1.Dispose();
        new TestEventA { Value = 2 }.Publish();
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1), "sub1 stopped");
        Assert.That(sub2.ReceiveCount, Is.EqualTo(2), "sub2 still active");

        handle2.Dispose();
    }
}
