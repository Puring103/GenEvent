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

    [Test]
    public void PendingOperations_UnregisterThenRegister_NetResultIsRegistered()
    {
        var target = new SubscriberA();
        var controller = new StopOtherThenStartOtherDuringPublishSubscriber(target);

        target.StartListening();
        controller.StartListening();

        // During publish: stop target then start target (deferred in FIFO order)
        new TestEventA { Value = 1 }.Publish();
        // Pending applied: Unregister(target) then Register(target) → target still registered
        new TestEventA { Value = 2 }.Publish();

        // target received first publish (was registered), then received second publish (re-registered via pending)
        Assert.That(target.ReceiveCount, Is.EqualTo(2));
        Assert.That(controller.ReceiveCount, Is.EqualTo(2));
    }

    [Test]
    public void PendingOperations_RegisterThenUnregister_NetResultIsUnregistered()
    {
        var target = new SubscriberA();
        var controller = new StartOtherThenStopOtherDuringPublishSubscriber(target);

        // target NOT registered at start
        controller.StartListening();

        // During publish: start target then stop target (deferred in FIFO order)
        new TestEventA { Value = 1 }.Publish();
        // Pending applied: Register(target) then Unregister(target) → target NOT registered
        new TestEventA { Value = 2 }.Publish();

        // target never received anything: was not registered before first publish,
        // and pending start+stop nets to unregistered before second publish
        Assert.That(target.ReceiveCount, Is.EqualTo(0));
        Assert.That(controller.ReceiveCount, Is.EqualTo(2));
    }

    [Test]
    public void StopOther_AfterPublishEnds_OnlySubscriberPublish_DoesNotReachTarget()
    {
        var target = new CountingMutationTargetSubscriber();
        var stopper = new StopOtherDuringPublishSubscriber(target);

        stopper.StartListening();
        target.StartListening();

        // First publish: stopper defers StopListening(target); target still receives current publish
        new TestEventA { Value = 1 }.Publish();
        // Pending applied: target unregistered

        // Second publish targeted only at target — target is no longer registered, should not receive
        new TestEventA { Value = 2 }.OnlySubscriber(target).Publish();

        Assert.That(stopper.ReceiveCount, Is.EqualTo(1));
        Assert.That(target.ReceiveCount, Is.EqualTo(1));
    }
}
