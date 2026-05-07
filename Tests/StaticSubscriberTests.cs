using GenEvent;
using GenEvent.Interface;

namespace Tests;

[TestFixture]
public class StaticSubscriberTests
{
    [SetUp]
    public void SetUp()
    {
        GenEventBootstrap.Init();
        StaticOnlySubscriber.ReceiveCount = 0;
        StaticOnlySubscriber.LastValue = 0;
        StaticVoidSubscriber.ReceiveCount = 0;
        MixedStaticInstanceSubscriber.MixedStaticCount = 0;
        MixedStaticInstanceSubscriber.MixedStaticLastValue = 0;
        StaticCancelSubscriber.ReceiveCount = 0;
        StaticCancelSubscriber.ShouldCancel = false;
        InstanceSubscriberForStaticEvent.StaticCountAtTimeOfCall = 0;
    }

    [Test]
    public void StaticHandler_ReceivesEvent_WithoutStartListening()
    {
        new StaticTestEvent { Value = 42 }.Publish();

        Assert.That(StaticOnlySubscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(StaticOnlySubscriber.LastValue, Is.EqualTo(42));
    }

    [Test]
    public void StaticHandler_ReceivesMultipleEvents()
    {
        new StaticTestEvent { Value = 1 }.Publish();
        new StaticTestEvent { Value = 2 }.Publish();

        Assert.That(StaticOnlySubscriber.ReceiveCount, Is.EqualTo(2));
        Assert.That(StaticOnlySubscriber.LastValue, Is.EqualTo(2));
    }

    [Test]
    public void StaticVoidHandler_ReceivesEvent_PublishReturnsTrue()
    {
        var result = new StaticTestEvent { Value = 7 }.Publish();

        Assert.That(StaticVoidSubscriber.ReceiveCount, Is.EqualTo(1));
        Assert.That(result, Is.True);
    }

    [Test]
    public void StaticHandler_CalledBeforeInstanceHandler_SamePriority()
    {
        var instanceSub = new InstanceSubscriberForStaticEvent();
        instanceSub.StartListening();

        try
        {
            new StaticTestEvent { Value = 1 }.Publish();

            // StaticOnlySubscriber.ReceiveCount should already be 1 when instance handler runs
            Assert.That(InstanceSubscriberForStaticEvent.StaticCountAtTimeOfCall, Is.EqualTo(1),
                "Static handler must be called before instance handler");
            Assert.That(instanceSub.ReceiveCount, Is.EqualTo(1));
        }
        finally
        {
            instanceSub.StopListening();
        }
    }

    [Test]
    public void StaticHandler_Cancel_StopsInstanceHandlers()
    {
        StaticCancelSubscriber.ShouldCancel = true;
        var instanceSub = new InstanceSubscriberForStaticEvent();
        instanceSub.StartListening();

        try
        {
            var result = new StaticTestEvent { Value = 1 }.Cancelable().Publish();

            Assert.That(result, Is.False);
            Assert.That(StaticCancelSubscriber.ReceiveCount, Is.EqualTo(1));
            Assert.That(instanceSub.ReceiveCount, Is.EqualTo(0), "Instance handler must not run after static cancel");
        }
        finally
        {
            instanceSub.StopListening();
        }
    }

    [Test]
    public void MixedClass_StaticHandlerReceivesWithoutInstance_InstanceHandlerRequiresStartListening()
    {
        new StaticTestEvent { Value = 10 }.Publish();

        Assert.That(MixedStaticInstanceSubscriber.MixedStaticCount, Is.EqualTo(1),
            "Static handler in mixed class must fire without any instance registered");

        var instance = new MixedStaticInstanceSubscriber();
        new StaticTestEventB { Value = 20 }.Publish();

        Assert.That(instance.InstanceReceiveCount, Is.EqualTo(0),
            "Instance handler must not fire before StartListening");

        instance.StartListening();
        new StaticTestEventB { Value = 30 }.Publish();

        Assert.That(instance.InstanceReceiveCount, Is.EqualTo(1));
        Assert.That(instance.InstanceLastValue, Is.EqualTo(30));
        instance.StopListening();
    }

    [Test]
    public void MixedClass_StopListening_DoesNotAffectStaticHandler()
    {
        var instance = new MixedStaticInstanceSubscriber();
        instance.StartListening();

        new StaticTestEvent { Value = 1 }.Publish();
        Assert.That(MixedStaticInstanceSubscriber.MixedStaticCount, Is.EqualTo(1));

        instance.StopListening();

        new StaticTestEvent { Value = 2 }.Publish();
        Assert.That(MixedStaticInstanceSubscriber.MixedStaticCount, Is.EqualTo(2),
            "Static handler must still fire after instance StopListening");
    }

    [Test]
    public void StaticHandler_IsRegisteredExactlyOnce_AfterMultipleBootstrapInits()
    {
        // Bootstrap.Init is idempotent; calling it again must not double-register
        GenEventBootstrap.Init();
        GenEventBootstrap.Init();

        new StaticTestEvent { Value = 1 }.Publish();

        Assert.That(StaticOnlySubscriber.ReceiveCount, Is.EqualTo(1),
            "Static handler must not be registered more than once");
    }
}
