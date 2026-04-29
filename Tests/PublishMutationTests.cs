using GenEvent;

namespace Tests;

[TestFixture]
public class PublishMutationTests
{
    [SetUp]
    public void SetUp()
    {
        TestRuntimeState.Reset();
        GenEventBootstrap.Init();
    }

    [TearDown]
    public void TearDown()
    {
        TestRuntimeState.Reset();
    }

    [Test]
    public void StopListening_DuringPublish_TakesEffectOnNextPublish()
    {
        var selfStopping = new StopSelfDuringPublishSubscriber();

        selfStopping.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(selfStopping.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StopOther_DuringPublish_DoesNotRemoveOtherFromCurrentPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var stopper = new StopOtherDuringPublishSubscriber(target);

        stopper.StartListening();
        target.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(stopper.ReceiveCount, Is.EqualTo(2));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StopOther_DuringPublish_DoesNotRemoveOtherFromNestedPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var stopper = new StopOtherAndRepublishDuringPublishSubscriber(target);

        stopper.StartListening();
        target.StartListening();

        new TestEventA { Value = 1 }.Publish();

        Assert.That(stopper.ReceiveCount, Is.EqualTo(2));
        Assert.That(target.ReceiveCount, Is.EqualTo(2));
    }

    [Test]
    public void StartOther_DuringPublish_TakesEffectOnNextPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var starter = new StartOtherDuringPublishSubscriber(target);

        starter.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.Publish();

        Assert.That(starter.ReceiveCount, Is.EqualTo(2));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StartOther_DuringPublish_DoesNotMakeOnlySubscriberNestedPublishReceive()
    {
        var target = new CountingMutationTargetSubscriber();
        var starter = new StartOtherAndPublishOnlySubscriberDuringPublishSubscriber(target);

        starter.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.OnlySubscriber(target).Publish();

        Assert.That(starter.ReceiveCount, Is.EqualTo(1));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }

    [Test]
    public void StopOther_DuringPublish_DoesNotRemoveOtherFromOnlySubscriberNestedPublish()
    {
        var target = new CountingMutationTargetSubscriber();
        var stopper = new StopOtherAndPublishOnlySubscriberDuringPublishSubscriber(target);

        stopper.StartListening();
        target.StartListening();

        new TestEventA { Value = 1 }.Publish();
        new TestEventA { Value = 2 }.OnlySubscriber(target).Publish();

        Assert.That(stopper.ReceiveCount, Is.EqualTo(1));
        Assert.That(target.ReceiveCount, Is.EqualTo(2));
    }
}
