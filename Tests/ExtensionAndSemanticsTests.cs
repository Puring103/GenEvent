using GenEvent;

namespace Tests;

/// <summary>
/// Extension and semantics: struct value semantics, re-publish same/different events.
/// </summary>
[TestFixture]
public class ExtensionAndSemanticsTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
    }

    [Test]
    public void StructEvent_ValueSemantics_ModificationInHandlerDoesNotAffectCaller()
    {
        var subscriber = new StructEventSubscriber();
        subscriber.StartListening();

        var evt = new StructEvent { Value = 100 };
        evt.Publish();

        Assert.That(subscriber.ReceivedValue, Is.EqualTo(100));
        Assert.That(evt.Value, Is.EqualTo(100), "Caller's copy should be unchanged (struct passed by value)");
    }

    [Test]
    public void RepublishSameEvent_FromHandler_PublishConfigIndependent_InnerDoesNotAffectOuter()
    {
        var repub = new RepublishSameEventSubscriber();
        var other = new SubscriberB();
        repub.StartListening();
        other.StartListening();

        // Outer: exclude 'other', so only repub receives
        var evt = new TestEventA { Value = 1 }.ExcludeSubscriber(other);
        evt.Publish();

        // repub receives outer (1st), then does inner Publish with no config
        // Inner: both receive. repub gets 2nd, other gets 1st
        Assert.That(repub.ReceiveCount, Is.EqualTo(2));
        Assert.That(other.ReceiveCount, Is.EqualTo(1), "Should receive inner event only (excluded from outer)");
    }

    [Test]
    public void RepublishDifferentEvent_FromHandler_EventConfigsIndependent()
    {
        var repub = new RepublishDifferentEventSubscriber();
        repub.StartListening();

        new TestEventA { Value = 1 }.Publish();

        Assert.That(repub.EventACount, Is.EqualTo(1));
        Assert.That(repub.EventBCount, Is.EqualTo(1), "EventB published from A handler should trigger B handler");
    }
}
