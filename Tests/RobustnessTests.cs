using System.Collections.Generic;
using GenEvent;
using GenEvent.Interface;

namespace GenEvent.Tests;

/// <summary>
/// Robustness: uninitialized publish, null filter args, no subscribers, GC/stability.
/// </summary>
[TestFixture]
public class RobustnessTests
{
    [Test]
    public void Publish_WithoutInit_ThrowsKeyNotFoundException()
    {
        ClearBootstrapState();
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<KeyNotFoundException>(() => evt.Publish());
    }

    [Test]
    public void WithFilter_Null_ThrowsArgumentNullException()
    {
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<ArgumentNullException>(() => evt.WithFilter(null!));
    }

    [Test]
    public void ExcludeSubscriber_Null_ThrowsArgumentNullException()
    {
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<ArgumentNullException>(() => evt.ExcludeSubscriber(null!));
    }

    [Test]
    public void IncludeSubscriber_Null_ThrowsArgumentNullException()
    {
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<ArgumentNullException>(() => evt.IncludeSubscriber(null!));
    }

    [Test]
    public void ExcludeSubscribers_Null_ThrowsArgumentNullException()
    {
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<ArgumentNullException>(() => evt.ExcludeSubscribers(null!));
    }

    [Test]
    public void IncludeSubscribers_Null_ThrowsArgumentNullException()
    {
        var evt = new TestEventA { Value = 1 };
        Assert.Throws<ArgumentNullException>(() => evt.IncludeSubscribers(null!));
    }

    [Test]
    public void Publish_NoSubscribers_ReturnsTrue()
    {
        GenEventBootstrap.Init();
        var evt = new TestEventA { Value = 1 };
        var result = evt.Publish();
        Assert.That(result, Is.True);
    }

    [Test]
    public void SubscribeUnsubscribe_ManyTimes_NoLeakOrException()
    {
        GenEventBootstrap.Init();
        var subscriber = new SubscriberA();
        for (int i = 0; i < 1000; i++)
        {
            subscriber.StartListening();
            subscriber.StopListening();
        }
        subscriber.StartListening();
        new TestEventA { Value = 1 }.Publish();
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void Publish_Frequent_NoException()
    {
        GenEventBootstrap.Init();
        var subscriber = new SubscriberA();
        subscriber.StartListening();
        for (int i = 0; i < 500; i++)
        {
            new TestEventA { Value = i }.Publish();
        }
        Assert.That(subscriber.ReceiveCount, Is.EqualTo(500));
    }

    [Test]
    public void GC_AfterManySubscribeUnsubscribeAndPublish_NoLeakOrException()
    {
        GenEventBootstrap.Init();
        for (int round = 0; round < 50; round++)
        {
            var subscriber = new SubscriberA();
            subscriber.StartListening();
            for (int i = 0; i < 100; i++)
            {
                new TestEventA { Value = i }.Publish();
            }
            subscriber.StopListening();
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        // No exception and no assert failure; republish after GC to ensure state still valid
        var sub = new SubscriberA();
        sub.StartListening();
        new TestEventA { Value = 1 }.Publish();
        Assert.That(sub.ReceiveCount, Is.EqualTo(1));
    }

    private static void ClearBootstrapState()
    {
        BaseEventPublisher.Publishers.Clear();
        BaseSubscriberRegistry.Subscribers.Clear();
    }
}
