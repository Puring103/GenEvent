using GenEvent;
using GenEvent.Interface;

namespace GenEvent.Tests;

/// <summary>
/// Cancel propagation and priority order tests.
/// </summary>
[TestFixture]
public class CancelAndPriorityTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void CancelablePublish_SubscriberReturnsFalse_StopsPropagationAndReturnsFalse()
    {
        var sub1 = new CancelSubscriber { ShouldCancel = false };
        var sub2 = new CancelSubscriber { ShouldCancel = true };
        var sub3 = new CancelSubscriber { ShouldCancel = false };
        sub1.StartListening();
        sub2.StartListening();
        sub3.StartListening();

        var evt = new TestEventA { Value = 1 };
        var result = evt.Cancelable().Publish();

        Assert.That(result, Is.False);
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub3.ReceiveCount, Is.EqualTo(0), "Should not be called after cancel");
    }

    [Test]
    public void CancelablePublish_AllReturnTrue_ReturnsTrue()
    {
        var sub1 = new CancelSubscriber { ShouldCancel = false };
        var sub2 = new CancelSubscriber { ShouldCancel = false };
        sub1.StartListening();
        sub2.StartListening();

        var evt = new TestEventA { Value = 1 };
        var result = evt.Cancelable().Publish();

        Assert.That(result, Is.True);
        Assert.That(sub1.ReceiveCount, Is.EqualTo(1));
        Assert.That(sub2.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void Priority_CallOrderIs_Primary_High_Medium_Low_End()
    {
        var primary = new PrimaryPrioritySubscriber();
        var high = new HighPrioritySubscriber();
        var medium = new MediumPrioritySubscriber();
        var low = new LowPrioritySubscriber();
        var end = new EndPrioritySubscriber();

        primary.StartListening();
        high.StartListening();
        medium.StartListening();
        low.StartListening();
        end.StartListening();

        new TestEventC { Value = 0 }.Publish();

        Assert.That(primary.CallOrder, Is.EqualTo(0));
        Assert.That(high.CallOrder, Is.EqualTo(1));
        Assert.That(medium.CallOrder, Is.EqualTo(2));
        Assert.That(low.CallOrder, Is.EqualTo(3));
        Assert.That(end.CallOrder, Is.EqualTo(4));
    }

    [Test]
    public void Priority_HighCancels_LowerPrioritiesNotExecuted()
    {
        var primary = new PrimaryPrioritySubscriber();
        var high = new HighPrioritySubscriber { CancelPropagation = true };
        var medium = new MediumPrioritySubscriber();
        var low = new LowPrioritySubscriber();
        var end = new EndPrioritySubscriber();

        primary.StartListening();
        high.StartListening();
        medium.StartListening();
        low.StartListening();
        end.StartListening();

        var evt = new TestEventC { Value = 0 };
        evt.Cancelable().Publish();

        Assert.That(primary.CallOrder, Is.EqualTo(0));
        Assert.That(high.CallOrder, Is.EqualTo(1));
        Assert.That(medium.CallOrder, Is.EqualTo(-1), "Should not run after High cancels");
        Assert.That(low.CallOrder, Is.EqualTo(-1));
        Assert.That(end.CallOrder, Is.EqualTo(-1));
    }
}
